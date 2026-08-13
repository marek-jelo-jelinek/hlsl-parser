using System.Linq;
using HlslParser.Diagnostics;
using HlslParser.Parsing;
using HlslParser.Syntax;
using NUnit.Framework;

namespace HlslParser.Tests.Parsing
{
    /// <summary>Statement-grammar coverage (blocks, if/else, for/while/do-while, switch/case,
    /// return/discard/break/continue, local declarations) plus recovery over malformed
    /// statements.</summary>
    [TestFixture]
    public class StatementParserTests
    {
        private static BlockStatementNode ParseBody(string bodyText, out HlslParseResult result)
        {
            result = Hlsl.Parse("void Foo() { " + bodyText + " }", "test.hlsl");
            var fn = (FunctionDeclarationNode)((CompilationUnitNode)result.Root).Declarations[0];
            return (BlockStatementNode)fn.Body;
        }

        private static BlockStatementNode ParseBody(string bodyText) => ParseBody(bodyText, out _);
        
        [Test]
        public void NestedBlocksParse()
        {
            var block = ParseBody("{ float x; }", out var result);
            Assert.IsFalse(result.HasErrors);
            Assert.AreEqual(1, block.Statements.Count);
            Assert.IsInstanceOf<BlockStatementNode>(block.Statements[0]);
        }

        [Test]
        public void EmptyStatementParses()
        {
            var block = ParseBody(";", out var result);
            Assert.IsFalse(result.HasErrors);
            Assert.IsInstanceOf<EmptyStatementNode>(block.Statements[0]);
        }

        [Test]
        public void LocalDeclarationWithInitializerParsesRealExpression()
        {
            var block = ParseBody("float3 p = a + b;", out var result);
            Assert.IsFalse(result.HasErrors);
            var declaration = (DeclarationStatementNode)block.Statements[0];
            Assert.AreEqual("float3", declaration.Type.Name);
            var initializer = declaration.Declarators[0].Initializer;
            Assert.IsInstanceOf<BinaryExpressionNode>(initializer.Expression);
        }

        [Test]
        public void ConstructorCallExpressionStatementIsNotMistakenForADeclaration()
        {
            var block = ParseBody("float4(1, 2, 3, 4);", out var result);
            Assert.IsFalse(result.HasErrors);
            var statement = (ExpressionStatementNode)block.Statements[0];
            Assert.IsInstanceOf<InvocationExpressionNode>(statement.Expression);
        }

        [Test]
        public void PlainFunctionCallExpressionStatementIsNotMistakenForADeclaration()
        {
            var block = ParseBody("foo();", out var result);
            Assert.IsFalse(result.HasErrors);
            var statement = (ExpressionStatementNode)block.Statements[0];
            Assert.IsInstanceOf<InvocationExpressionNode>(statement.Expression);
        }

        [Test]
        public void UserTypeLedLocalDeclarationParses()
        {
            var block = ParseBody("MyStruct s;", out var result);
            Assert.IsFalse(result.HasErrors);
            var declaration = (DeclarationStatementNode)block.Statements[0];
            Assert.AreEqual("MyStruct", declaration.Type.Name);
            Assert.AreEqual("s", declaration.Declarators[0].Name);
        }
        
        [Test]
        public void IfWithoutElseParses()
        {
            var block = ParseBody("if (a) b();", out var result);
            Assert.IsFalse(result.HasErrors);
            var ifStatement = (IfStatementNode)block.Statements[0];
            Assert.IsNull(ifStatement.Else);
            Assert.IsInstanceOf<ExpressionStatementNode>(ifStatement.Then);
        }

        [Test]
        public void IfWithElseParses()
        {
            var block = ParseBody("if (a) b(); else c();", out var result);
            Assert.IsFalse(result.HasErrors);
            var ifStatement = (IfStatementNode)block.Statements[0];
            Assert.IsNotNull(ifStatement.Else);
        }

        [Test]
        public void IfWithBlockBranchesParses()
        {
            var block = ParseBody("if (a) { b(); } else { c(); }", out var result);
            Assert.IsFalse(result.HasErrors);
            var ifStatement = (IfStatementNode)block.Statements[0];
            Assert.IsInstanceOf<BlockStatementNode>(ifStatement.Then);
            Assert.IsInstanceOf<BlockStatementNode>(ifStatement.Else);
        }
        
        [Test]
        public void ForWithAllClausesParses()
        {
            var block = ParseBody("for (int i = 0; i < 10; i++) { sum += i; }", out var result);
            Assert.IsFalse(result.HasErrors);
            var forStatement = (ForStatementNode)block.Statements[0];
            Assert.IsInstanceOf<DeclarationStatementNode>(forStatement.Initializer);
            Assert.IsInstanceOf<BinaryExpressionNode>(forStatement.Condition);
            Assert.IsInstanceOf<UnaryExpressionNode>(forStatement.Incrementor);
        }

        [Test]
        public void ForWithAllClausesOmittedParses()
        {
            var block = ParseBody("for (;;) { break; }", out var result);
            Assert.IsFalse(result.HasErrors);
            var forStatement = (ForStatementNode)block.Statements[0];
            Assert.IsNull(forStatement.Initializer);
            Assert.IsNull(forStatement.Condition);
            Assert.IsNull(forStatement.Incrementor);
        }

        [Test]
        public void ForWithExpressionInitializerParses()
        {
            var block = ParseBody("for (i = 0; i < 10; i = i + 1) { }", out var result);
            Assert.IsFalse(result.HasErrors);
            var forStatement = (ForStatementNode)block.Statements[0];
            Assert.IsInstanceOf<ExpressionStatementNode>(forStatement.Initializer);
        }
        
        [Test]
        public void WhileParses()
        {
            var block = ParseBody("while (a < 10) { a++; }", out var result);
            Assert.IsFalse(result.HasErrors);
            Assert.IsInstanceOf<WhileStatementNode>(block.Statements[0]);
        }

        [Test]
        public void DoWhileParses()
        {
            var block = ParseBody("do { a++; } while (a < 10);", out var result);
            Assert.IsFalse(result.HasErrors);
            var doStatement = (DoStatementNode)block.Statements[0];
            Assert.IsInstanceOf<BlockStatementNode>(doStatement.Body);
        }
        
        [Test]
        public void SwitchWithMultipleSectionsAndDefaultParses()
        {
            var block = ParseBody("switch (x) { case 1: a(); break; case 2: case 3: b(); break; default: c(); break; }", out var result);
            Assert.IsFalse(result.HasErrors);
            var switchStatement = (SwitchStatementNode)block.Statements[0];
            Assert.AreEqual(3, switchStatement.Sections.Count);

            var firstSection = (SwitchSectionNode)switchStatement.Sections[0];
            Assert.AreEqual(1, firstSection.Labels.Count);
            Assert.IsFalse(firstSection.Labels[0].IsDefault);

            var secondSection = (SwitchSectionNode)switchStatement.Sections[1];
            Assert.AreEqual(2, secondSection.Labels.Count); // stacked "case 2: case 3:"

            var thirdSection = (SwitchSectionNode)switchStatement.Sections[2];
            Assert.IsTrue(thirdSection.Labels[0].IsDefault);
        }
        
        [Test]
        public void BareReturnParses()
        {
            var block = ParseBody("return;", out var result);
            Assert.IsFalse(result.HasErrors);
            var returnStatement = (ReturnStatementNode)block.Statements[0];
            Assert.IsNull(returnStatement.Expression);
        }

        [Test]
        public void ReturnWithExpressionParses()
        {
            var block = ParseBody("return a + b;", out var result);
            Assert.IsFalse(result.HasErrors);
            var returnStatement = (ReturnStatementNode)block.Statements[0];
            Assert.IsInstanceOf<BinaryExpressionNode>(returnStatement.Expression);
        }

        [Test]
        public void DiscardParses()
        {
            var block = ParseBody("discard;", out var result);
            Assert.IsFalse(result.HasErrors);
            Assert.IsInstanceOf<DiscardStatementNode>(block.Statements[0]);
        }

        [Test]
        public void BreakAndContinueParse()
        {
            var block = ParseBody("for (;;) { if (a) break; else continue; }", out var result);
            Assert.IsFalse(result.HasErrors);
            var forStatement = (ForStatementNode)block.Statements[0];
            var inner = (BlockStatementNode)forStatement.Body;
            var ifStatement = (IfStatementNode)inner.Statements[0];
            Assert.IsInstanceOf<BreakStatementNode>(ifStatement.Then);
            Assert.IsInstanceOf<ContinueStatementNode>(ifStatement.Else);
        }
        
        [Test]
        public void MissingSemicolonAfterExpressionStatementRecoversToNextStatement()
        {
            var block = ParseBody("a() b();", out var result);
            Assert.IsTrue(result.HasErrors);
            // The ';'-less first statement still parses (Expect just reports and returns Missing
            // without consuming), so the very next token starts the second statement.
            Assert.GreaterOrEqual(block.Statements.Count, 1);
        }

        [Test]
        public void GarbageTokenInBlockBecomesErrorNodeAndSubsequentStatementsStillParse()
        {
            var block = ParseBody("@@@ a();", out var result);
            Assert.IsTrue(result.HasErrors);
            Assert.IsInstanceOf<ErrorNode>(block.Statements[0]);
            Assert.IsInstanceOf<ExpressionStatementNode>(block.Statements[1]);
        }

        [Test]
        public void UnterminatedBlockAtEofReportsUnterminatedBlockNoException()
        {
            HlslParseResult result = null;
            Assert.DoesNotThrow(() => result = Hlsl.Parse("void Foo() { float x = 1;", "test.hlsl"));
            Assert.IsTrue(result.HasErrors);
            Assert.IsTrue(result.Diagnostics.Any(d => d.Id == DiagnosticIds.UnterminatedBlock));
        }

        [Test]
        public void MalformedSwitchLabelMissingColonReportsMalformedSwitchLabel()
        {
            var result = Hlsl.Parse("void Foo() { switch (x) { case 1 a(); } }", "test.hlsl");
            Assert.IsTrue(result.Diagnostics.Any(d => d.Id == DiagnosticIds.MalformedSwitchLabel));
        }

        [Test]
        public void UnrecognizedSwitchSectionTokenBecomesErrorNode()
        {
            var block = ParseBody("switch (x) { @@@ case 1: a(); break; }", out var result);
            Assert.IsTrue(result.HasErrors);
            var switchStatement = (SwitchStatementNode)block.Statements[0];
            Assert.IsInstanceOf<ErrorNode>(switchStatement.Sections[0]);
            Assert.IsInstanceOf<SwitchSectionNode>(switchStatement.Sections[1]);
        }
    }
}
