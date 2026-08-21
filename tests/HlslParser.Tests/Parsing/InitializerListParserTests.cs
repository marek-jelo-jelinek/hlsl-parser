using System.Linq;
using HlslParser.Diagnostics;
using HlslParser.Parsing;
using HlslParser.Syntax;
using NUnit.Framework;

namespace HlslParser.Tests.Parsing
{
    /// <summary>Covers brace/aggregate initializer lists (<c>= { expr, expr, ... }</c>) on
    /// variable declarators and parameter default values — a real HLSL construct (e.g.
    /// <c>static const float2 offsets[4] = { float2(0,0), ... };</c>) that
    /// <c>TryParseInitializerExpression</c> didn't previously recognize at all.</summary>
    [TestFixture]
    public class InitializerListParserTests
    {
        private static InitializerNode ParseDeclaratorInitializer(string declarationText, out HlslParseResult result)
        {
            result = Hlsl.Parse(declarationText, "test.hlsl");
            var unit = (CompilationUnitNode)result.Root;
            var global = (GlobalVariableDeclarationNode)unit.Declarations[0];
            return global.Declarators[0].Initializer;
        }

        [Test]
        public void FlatScalarListParsesAsInitializerListExpression()
        {
            var initializer = ParseDeclaratorInitializer("float3 v = {1, 2, 3};", out var result);

            Assert.IsFalse(result.HasErrors, string.Join("\n", result.Diagnostics));
            var list = (InitializerListExpressionNode)initializer.Expression;
            Assert.AreEqual(HlslNodeKind.InitializerListExpression, list.Kind);
            Assert.AreEqual(3, list.Elements.Count);

            for (var i = 0; i < 3; i++)
            {
                var literal = (LiteralExpressionNode)list.Elements[i];
                Assert.AreEqual((ulong)(i + 1), literal.IntegerValue);
            }
        }

        [Test]
        public void RealWorldConstructorCallListParsesCleanly()
        {
            const string source = "static const float2 offsets[4] = { float2(0,0), float2(1,0), float2(0,1), float2(1,1) };";
            var initializer = ParseDeclaratorInitializer(source, out var result);

            Assert.IsFalse(result.HasErrors, string.Join("\n", result.Diagnostics));
            var list = (InitializerListExpressionNode)initializer.Expression;
            Assert.AreEqual(4, list.Elements.Count);

            foreach (var element in list.Elements)
            {
                var call = (InvocationExpressionNode)element;
                Assert.AreEqual("float2", ((IdentifierExpressionNode)call.Callee).Name);
                Assert.AreEqual(2, call.Arguments.Count);
            }
        }

        [Test]
        public void NestedBraceInitializerParsesAsNestedInitializerLists()
        {
            var initializer = ParseDeclaratorInitializer("float2x2 m = {{1,0},{0,1}};", out var result);

            Assert.IsFalse(result.HasErrors, string.Join("\n", result.Diagnostics));
            var outer = (InitializerListExpressionNode)initializer.Expression;
            Assert.AreEqual(2, outer.Elements.Count);

            var firstRow = (InitializerListExpressionNode)outer.Elements[0];
            Assert.AreEqual(2, firstRow.Elements.Count);
            Assert.AreEqual(1ul, ((LiteralExpressionNode)firstRow.Elements[0]).IntegerValue);
            Assert.AreEqual(0ul, ((LiteralExpressionNode)firstRow.Elements[1]).IntegerValue);

            var secondRow = (InitializerListExpressionNode)outer.Elements[1];
            Assert.AreEqual(0ul, ((LiteralExpressionNode)secondRow.Elements[0]).IntegerValue);
            Assert.AreEqual(1ul, ((LiteralExpressionNode)secondRow.Elements[1]).IntegerValue);
        }

        [Test]
        public void StructArrayInitializerParsesAsNestedInitializerLists()
        {
            const string source = @"struct MyStruct { float a; float b; };
MyStruct arr[2] = { {1,2}, {3,4} };";
            var result = Hlsl.Parse(source, "test.hlsl");
            Assert.IsFalse(result.HasErrors, string.Join("\n", result.Diagnostics));

            var unit = (CompilationUnitNode)result.Root;
            var global = (GlobalVariableDeclarationNode)unit.Declarations[1];
            var list = (InitializerListExpressionNode)global.Declarators[0].Initializer.Expression;

            Assert.AreEqual(2, list.Elements.Count);
            Assert.IsInstanceOf<InitializerListExpressionNode>(list.Elements[0]);
            Assert.IsInstanceOf<InitializerListExpressionNode>(list.Elements[1]);
        }

        [Test]
        public void TrailingCommaIsAccepted()
        {
            var initializer = ParseDeclaratorInitializer("float3 v = {1, 2, 3,};", out var result);

            Assert.IsFalse(result.HasErrors, string.Join("\n", result.Diagnostics));
            var list = (InitializerListExpressionNode)initializer.Expression;
            Assert.AreEqual(3, list.Elements.Count);
        }

        [Test]
        public void UnterminatedListAtEndOfFileReportsUnexpectedEndOfFileAndRecovers()
        {
            // Running out of input entirely reports HL0217 (UnexpectedEndOfFile) — the same
            // shared EOF diagnostic every other Expect() call reports at end of file, not the
            // more specific MalformedInitializerList (see the non-EOF case below for that).
            HlslParseResult result = null;
            Assert.DoesNotThrow(() => result = Hlsl.Parse("float3 v = {1, 2", "test.hlsl"));

            Assert.IsTrue(result.HasErrors);
            Assert.IsTrue(result.Diagnostics.Any(d => d.Id == DiagnosticIds.UnexpectedEndOfFile));

            // Recovery still produced a partial tree, not a throw.
            var unit = (CompilationUnitNode)result.Root;
            Assert.AreEqual(1, unit.Declarations.Count);
            var global = (GlobalVariableDeclarationNode)unit.Declarations[0];
            var list = (InitializerListExpressionNode)global.Declarators[0].Initializer.Expression;
            Assert.AreEqual(2, list.Elements.Count); // "1" and "2" were still recovered
        }

        [Test]
        public void MissingCloseBraceBeforeMoreTokensReportsMalformedInitializerListAndRecovers()
        {
            HlslParseResult result = null;
            Assert.DoesNotThrow(() => result = Hlsl.Parse("float3 v = {1, 2 float3 w;", "test.hlsl"));

            Assert.IsTrue(result.HasErrors);
            Assert.IsTrue(result.Diagnostics.Any(d => d.Id == DiagnosticIds.MalformedInitializerList));

            // Recovery kept going and still found the next declaration.
            var unit = (CompilationUnitNode)result.Root;
            Assert.IsTrue(unit.Declarations.OfType<GlobalVariableDeclarationNode>().Any(d => d.Declarators[0].Name == "w"));
        }

        [Test]
        public void LocalVariableDeclarationSupportsBraceInitializer()
        {
            const string source = "void f() { float3 v = {1, 2, 3}; }";
            var result = Hlsl.Parse(source, "test.hlsl");

            Assert.IsFalse(result.HasErrors, string.Join("\n", result.Diagnostics));
            var unit = (CompilationUnitNode)result.Root;
            var fn = (FunctionDeclarationNode)unit.Declarations[0];
            var block = (BlockStatementNode)fn.Body;
            var localDecl = (DeclarationStatementNode)block.Statements[0];

            var list = (InitializerListExpressionNode)localDecl.Declarators[0].Initializer.Expression;
            Assert.AreEqual(3, list.Elements.Count);
        }

        [Test]
        public void ParameterDefaultValueSupportsBraceInitializer()
        {
            const string source = "void f(float3 v = {0,0,0}) { }";
            var result = Hlsl.Parse(source, "test.hlsl");

            Assert.IsFalse(result.HasErrors, string.Join("\n", result.Diagnostics));
            var unit = (CompilationUnitNode)result.Root;
            var fn = (FunctionDeclarationNode)unit.Declarations[0];
            var param = (ParameterNode)fn.Parameters[0];

            var list = (InitializerListExpressionNode)param.DefaultValue.Expression;
            Assert.AreEqual(3, list.Elements.Count);
        }
    }
}
