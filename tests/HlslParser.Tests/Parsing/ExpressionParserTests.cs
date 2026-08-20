using HlslParser.Lexing;
using HlslParser.Parsing;
using HlslParser.Syntax;
using NUnit.Framework;

namespace HlslParser.Tests.Parsing
{
    /// <summary>Precedence/associativity coverage for the expression ladder (<c>a + b * c</c>,
    /// nested ternaries, <c>foo().xyz</c>, a C-style cast), plus recovery over malformed
    /// expressions. Driven through <see cref="Hlsl.Parse"/> by wrapping each expression in a
    /// minimal function body.</summary>
    [TestFixture]
    public class ExpressionParserTests
    {
        private static HlslNode ParseExpression(string exprText, out HlslParseResult result)
        {
            result = Hlsl.Parse("void Foo() { " + exprText + "; }", "test.hlsl");
            var fn = (FunctionDeclarationNode)((CompilationUnitNode)result.Root).Declarations[0];
            var block = (BlockStatementNode)fn.Body;
            var statement = (ExpressionStatementNode)block.Statements[0];
            return statement.Expression;
        }

        private static HlslNode ParseExpression(string exprText) => ParseExpression(exprText, out _);
        
        [Test]
        public void MultiplicativeBindsTighterThanAdditive()
        {
            var expr = (BinaryExpressionNode)ParseExpression("a + b * c", out var result);

            Assert.IsFalse(result.HasErrors);
            Assert.AreEqual(HlslTokenKind.Plus, expr.OperatorKind);
            Assert.AreEqual("a", ((IdentifierExpressionNode)expr.Left).Name);
            var right = (BinaryExpressionNode)expr.Right;
            Assert.AreEqual(HlslTokenKind.Star, right.OperatorKind);
            Assert.AreEqual("b", ((IdentifierExpressionNode)right.Left).Name);
            Assert.AreEqual("c", ((IdentifierExpressionNode)right.Right).Name);
        }

        [Test]
        public void NestedTernariesAreRightAssociative()
        {
            // a ? b : c ? d : e  ==  a ? b : (c ? d : e)
            var expr = (ConditionalExpressionNode)ParseExpression("a ? b : c ? d : e", out var result);

            Assert.IsFalse(result.HasErrors);
            Assert.AreEqual("a", ((IdentifierExpressionNode)expr.Condition).Name);
            Assert.AreEqual("b", ((IdentifierExpressionNode)expr.WhenTrue).Name);
            var nested = (ConditionalExpressionNode)expr.WhenFalse;
            Assert.AreEqual("c", ((IdentifierExpressionNode)nested.Condition).Name);
            Assert.AreEqual("d", ((IdentifierExpressionNode)nested.WhenTrue).Name);
            Assert.AreEqual("e", ((IdentifierExpressionNode)nested.WhenFalse).Name);
        }

        [Test]
        public void CallThenMemberSwizzleChainsAsPostfix()
        {
            var expr = (MemberAccessExpressionNode)ParseExpression("foo().xyz", out var result);

            Assert.IsFalse(result.HasErrors);
            Assert.AreEqual("xyz", expr.MemberName);
            var invocation = (InvocationExpressionNode)expr.Target;
            Assert.AreEqual("foo", ((IdentifierExpressionNode)invocation.Callee).Name);
            Assert.AreEqual(0, invocation.Arguments.Count);
        }

        [Test]
        public void CStyleCastOnBuiltinTypeParsesAsCastExpression()
        {
            var expr = (CastExpressionNode)ParseExpression("(float)x", out var result);

            Assert.IsFalse(result.HasErrors);
            Assert.AreEqual("float", expr.TargetType.Name);
            Assert.AreEqual("x", ((IdentifierExpressionNode)expr.Operand).Name);
        }

        [TestCase("(fixed4)value", new string[0], "fixed4", HlslKeywordCategory.VectorType)]
        [TestCase("(half3)value", new string[0], "half3", HlslKeywordCategory.VectorType)]
        [TestCase("(unorm float4)value", new[] { "unorm" }, "float4", HlslKeywordCategory.VectorType)]
        [TestCase("(snorm half3)value", new[] { "snorm" }, "half3", HlslKeywordCategory.VectorType)]
        public void CStyleCastWithPrecisionAndModifiersParsesAsCastExpression(string source, string[] expectedModifiers, string expectedTypeName, HlslKeywordCategory expectedCategory)
        {
            var expr = (CastExpressionNode)ParseExpression(source, out var result);

            Assert.IsFalse(result.HasErrors);
            CollectionAssert.AreEqual(expectedModifiers, expr.Modifiers);
            Assert.AreEqual(expectedTypeName, expr.TargetType.Name);
            Assert.AreEqual(expectedCategory, expr.TargetType.Category);
            Assert.AreEqual("value", ((IdentifierExpressionNode)expr.Operand).Name);
        }

        [Test]
        public void CStyleCastOnGenericVectorParsesAsCastExpression()
        {
            var expr = (CastExpressionNode)ParseExpression("(vector<float, 4>)value", out var result);

            Assert.IsFalse(result.HasErrors);
            Assert.AreEqual("vector", expr.TargetType.Name);
            Assert.AreEqual(2, expr.TargetType.TypeArguments.Count);
            Assert.AreEqual("float", expr.TargetType.TypeArguments[0].Name);
            Assert.AreEqual("4", expr.TargetType.TypeArguments[1].Name);
            Assert.AreEqual("value", ((IdentifierExpressionNode)expr.Operand).Name);
        }

        [Test]
        public void ParenthesizedGroupingIsNotMistakenForACast()
        {
            var multiplication = (BinaryExpressionNode)ParseExpression("(a + b) * c", out var result);

            Assert.IsFalse(result.HasErrors);
            Assert.AreEqual(HlslTokenKind.Star, multiplication.OperatorKind);
            var parenthesized = (ParenthesizedExpressionNode)multiplication.Left;
            var inner = (BinaryExpressionNode)parenthesized.Expression;
            Assert.AreEqual(HlslTokenKind.Plus, inner.OperatorKind);
        }
        
        [Test]
        public void UnaryBindsTighterThanMultiplicative()
        {
            var expr = (BinaryExpressionNode)ParseExpression("-a * b");
            var left = (UnaryExpressionNode)expr.Left;
            Assert.AreEqual(HlslTokenKind.Minus, left.OperatorKind);
            Assert.IsFalse(left.IsPostfix);
        }

        [Test]
        public void RelationalBindsTighterThanEquality()
        {
            var expr = (BinaryExpressionNode)ParseExpression("a < b == c > d");
            Assert.AreEqual(HlslTokenKind.EqualsEquals, expr.OperatorKind);
            Assert.AreEqual(HlslTokenKind.LessThan, ((BinaryExpressionNode)expr.Left).OperatorKind);
            Assert.AreEqual(HlslTokenKind.GreaterThan, ((BinaryExpressionNode)expr.Right).OperatorKind);
        }

        [Test]
        public void LogicalAndBindsTighterThanLogicalOr()
        {
            var expr = (BinaryExpressionNode)ParseExpression("a || b && c");
            Assert.AreEqual(HlslTokenKind.PipePipe, expr.OperatorKind);
            Assert.AreEqual(HlslTokenKind.AmpersandAmpersand, ((BinaryExpressionNode)expr.Right).OperatorKind);
        }

        [Test]
        public void BitwiseOperatorsNestByPrecedenceAndOrXorAnd()
        {
            var expr = (BinaryExpressionNode)ParseExpression("a | b ^ c & d");
            Assert.AreEqual(HlslTokenKind.Pipe, expr.OperatorKind);
            var xor = (BinaryExpressionNode)expr.Right;
            Assert.AreEqual(HlslTokenKind.Caret, xor.OperatorKind);
            Assert.AreEqual(HlslTokenKind.Ampersand, ((BinaryExpressionNode)xor.Right).OperatorKind);
        }

        [Test]
        public void ShiftBindsTighterThanRelational()
        {
            var expr = (BinaryExpressionNode)ParseExpression("a << b < c");
            Assert.AreEqual(HlslTokenKind.LessThan, expr.OperatorKind);
            Assert.AreEqual(HlslTokenKind.LessThanLessThan, ((BinaryExpressionNode)expr.Left).OperatorKind);
        }

        [Test]
        public void TernaryBindsLooserThanLogicalButTighterThanAssignment()
        {
            var expr = (AssignmentExpressionNode)ParseExpression("x = a || b ? c : d");
            var conditional = (ConditionalExpressionNode)expr.Value;
            Assert.AreEqual(HlslTokenKind.PipePipe, ((BinaryExpressionNode)conditional.Condition).OperatorKind);
        }

        [Test]
        public void AssignmentIsRightAssociative()
        {
            var expr = (AssignmentExpressionNode)ParseExpression("a = b = c");
            Assert.AreEqual("a", ((IdentifierExpressionNode)expr.Target).Name);
            var nested = (AssignmentExpressionNode)expr.Value;
            Assert.AreEqual("b", ((IdentifierExpressionNode)nested.Target).Name);
            Assert.AreEqual("c", ((IdentifierExpressionNode)nested.Value).Name);
        }

        [Test]
        public void PrefixUnaryIsRightAssociative()
        {
            var expr = (UnaryExpressionNode)ParseExpression("--x");
            Assert.AreEqual(HlslTokenKind.MinusMinus, expr.OperatorKind);
            Assert.IsFalse(expr.IsPostfix);
        }

        [Test]
        public void PostfixIncrementAppliesAfterPrimary()
        {
            var expr = (UnaryExpressionNode)ParseExpression("x++");
            Assert.AreEqual(HlslTokenKind.PlusPlus, expr.OperatorKind);
            Assert.IsTrue(expr.IsPostfix);
            Assert.AreEqual("x", ((IdentifierExpressionNode)expr.Operand).Name);
        }

        [Test]
        public void ElementAccessThenMemberAccessChain()
        {
            var expr = (MemberAccessExpressionNode)ParseExpression("arr[0].x");
            Assert.AreEqual("x", expr.MemberName);
            var element = (ElementAccessExpressionNode)expr.Target;
            Assert.AreEqual("arr", ((IdentifierExpressionNode)element.Target).Name);
        }

        [Test]
        public void ConstructorCallUsesTypeKeywordAsCallee()
        {
            var expr = (InvocationExpressionNode)ParseExpression("float4(1, 2, 3, 4)");
            Assert.AreEqual("float4", ((IdentifierExpressionNode)expr.Callee).Name);
            Assert.AreEqual(4, expr.Arguments.Count);
        }

        [Test]
        public void NestedCallArgumentCommasDoNotBreakOuterArgumentList()
        {
            var expr = (InvocationExpressionNode)ParseExpression("max(float3(1, 2, 3), other)");
            Assert.AreEqual(2, expr.Arguments.Count);
            Assert.IsInstanceOf<InvocationExpressionNode>(expr.Arguments[0]);
        }

        [Test]
        public void TrueFalseParseAsBooleanLiterals()
        {
            var trueExpr = (LiteralExpressionNode)ParseExpression("true");
            var falseExpr = (LiteralExpressionNode)ParseExpression("false");
            Assert.IsTrue(trueExpr.IsBooleanLiteral);
            Assert.IsTrue(trueExpr.BooleanValue);
            Assert.IsTrue(falseExpr.IsBooleanLiteral);
            Assert.IsFalse(falseExpr.BooleanValue);
        }
        
        [Test]
        public void MissingOperandReportsExpectedExpressionButKeepsParsingSubsequentStatements()
        {
            var result = Hlsl.Parse("void Foo() { x = ; y = 1; }", "test.hlsl");
            Assert.IsTrue(result.HasErrors);
            Assert.IsTrue(System.Linq.Enumerable.Any(result.Diagnostics, d => d.Id == HlslParser.Diagnostics.DiagnosticIds.ExpectedExpression));

            var fn = (FunctionDeclarationNode)((CompilationUnitNode)result.Root).Declarations[0];
            var block = (BlockStatementNode)fn.Body;
            Assert.AreEqual(2, block.Statements.Count);
            var second = (ExpressionStatementNode)block.Statements[1];
            var assignment = (AssignmentExpressionNode)second.Expression;
            Assert.AreEqual("y", ((IdentifierExpressionNode)assignment.Target).Name);
        }
    }
}
