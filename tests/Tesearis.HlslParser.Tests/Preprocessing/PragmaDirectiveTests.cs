using System.Collections.Generic;
using System.Linq;
using Tesearis.HlslParser.Diagnostics;
using Tesearis.HlslParser.Lexing;
using Tesearis.HlslParser.Parsing;
using Tesearis.HlslParser.Preprocessing;
using Tesearis.HlslParser.Syntax;
using Tesearis.HlslParser.Text;
using NUnit.Framework;

namespace Tesearis.HlslParser.Tests.Preprocessing
{
    [TestFixture]
    public class PragmaDirectiveTests
    {
        private static IReadOnlyList<PragmaDirectiveNode> PreprocessAndGetPragmas(string text, out DiagnosticSink diagnostics)
        {
            var source = new SourceText(text, "test.hlsl");
            diagnostics = new DiagnosticSink(source);
            var tokens = new Lexer(source, diagnostics).Tokenize();
            var preprocessor = new Preprocessor(source, diagnostics);
            preprocessor.Process(tokens);
            return preprocessor.Pragmas;
        }

        [Test]
        public void ComputeShaderPragmasAreCaptured()
        {
            const string source = @"
#pragma kernel CSMain
#pragma multi_compile _ FEATURE_A
[numthreads(8,8,1)]
void CSMain() {}
";
            var result = Hlsl.Parse(source);
            Assert.IsFalse(result.HasErrors);

            var unit = (CompilationUnitNode)result.Root;
            Assert.AreEqual(2, unit.Pragmas.Count);

            var kernel = unit.Pragmas[0];
            Assert.AreEqual("kernel", kernel.Name);
            Assert.AreEqual(1, kernel.Arguments.Count);
            Assert.AreEqual("CSMain", kernel.Arguments[0]);
            Assert.AreEqual(2, kernel.Line);
            Assert.AreEqual("kernel CSMain", kernel.RawText);

            var multiCompile = unit.Pragmas[1];
            Assert.AreEqual("multi_compile", multiCompile.Name);
            CollectionAssert.AreEqual(new[] { "_", "FEATURE_A" }, multiCompile.Arguments);
            Assert.AreEqual(2, multiCompile.ArgumentSpans.Count);
            Assert.AreEqual(3, multiCompile.Line);
            Assert.AreEqual("multi_compile _ FEATURE_A", multiCompile.RawText);
        }

        [Test]
        public void RayTracingPragmasAreCaptured()
        {
            const string source = @"#pragma max_recursion_depth 5
#pragma raytracing test";

            var pragmas = PreprocessAndGetPragmas(source, out var diagnostics);
            Assert.AreEqual(0, diagnostics.Diagnostics.Count);
            Assert.AreEqual(2, pragmas.Count);

            Assert.AreEqual("max_recursion_depth", pragmas[0].Name);
            Assert.AreEqual(1, pragmas[0].Arguments.Count);
            Assert.AreEqual("5", pragmas[0].Arguments[0]);
            Assert.AreEqual(1, pragmas[0].Line);

            Assert.AreEqual("raytracing", pragmas[1].Name);
            Assert.AreEqual(1, pragmas[1].Arguments.Count);
            Assert.AreEqual("test", pragmas[1].Arguments[0]);
            Assert.AreEqual(2, pragmas[1].Line);
        }

        [Test]
        public void TargetAndProfilePragmasCaptured()
        {
            const string source = "#pragma target 4.5\n#pragma only_renderers d3d11 vulkan metal";
            var pragmas = PreprocessAndGetPragmas(source, out var diagnostics);

            Assert.AreEqual(0, diagnostics.Diagnostics.Count);
            Assert.AreEqual(2, pragmas.Count);

            Assert.AreEqual("target", pragmas[0].Name);
            CollectionAssert.AreEqual(new[] { "4.5" }, pragmas[0].Arguments);

            Assert.AreEqual("only_renderers", pragmas[1].Name);
            CollectionAssert.AreEqual(new[] { "d3d11", "vulkan", "metal" }, pragmas[1].Arguments);
        }

        [Test]
        public void PragmaWithPunctuationTokensPreservesArguments()
        {
            const string source = "#pragma warning(disable: 4000)";
            var pragmas = PreprocessAndGetPragmas(source, out var diagnostics);

            Assert.AreEqual(0, diagnostics.Diagnostics.Count);
            Assert.AreEqual(1, pragmas.Count);
            Assert.AreEqual("warning", pragmas[0].Name);
            CollectionAssert.AreEqual(new[] { "(", "disable", ":", "4000", ")" }, pragmas[0].Arguments);
        }

        [Test]
        public void PragmaInsideDeadBranchIsNotEmitted()
        {
            const string source = @"
#if 0
#pragma kernel DeadKernel
#endif
#pragma kernel LiveKernel
";
            var pragmas = PreprocessAndGetPragmas(source, out var diagnostics);

            Assert.AreEqual(0, diagnostics.Diagnostics.Count);
            Assert.AreEqual(1, pragmas.Count);
            Assert.AreEqual("LiveKernel", pragmas[0].Arguments[0]);
        }

        [Test]
        public void EmptyPragmaDirectiveHandledGracefully()
        {
            const string source = "#pragma\nvoid main() {}";
            var result = Hlsl.Parse(source);

            Assert.IsFalse(result.HasErrors);
            var unit = (CompilationUnitNode)result.Root;
            Assert.AreEqual(1, unit.Pragmas.Count);
            Assert.AreEqual(string.Empty, unit.Pragmas[0].Name);
            Assert.AreEqual(0, unit.Pragmas[0].Arguments.Count);
        }

        [Test]
        public void PragmasAreIncludedInCompilationUnitChildren()
        {
            const string source = "#pragma kernel CSMain\nvoid CSMain() {}";
            var result = Hlsl.Parse(source);
            var unit = (CompilationUnitNode)result.Root;

            var children = unit.Children.ToList();
            Assert.AreEqual(2, children.Count);
            Assert.IsInstanceOf<PragmaDirectiveNode>(children[0]);
            Assert.IsInstanceOf<FunctionDeclarationNode>(children[1]);

            var descendants = unit.DescendantsAndSelf().ToList();
            Assert.IsTrue(descendants.OfType<PragmaDirectiveNode>().Any());
        }

        private sealed class PragmaRecordingVisitor : HlslVisitor
        {
            public readonly List<string> Names = new();

            public override void VisitPragmaDirective(PragmaDirectiveNode node)
            {
                Names.Add(node.Name);
                base.VisitPragmaDirective(node);
            }
        }

        [Test]
        public void VisitorVisitsPragmaDirectiveNode()
        {
            const string source = "#pragma kernel CSMain\n#pragma target 5.0\nvoid CSMain() {}";
            var result = Hlsl.Parse(source);
            var visitor = new PragmaRecordingVisitor();

            visitor.Visit(result.Root);
            CollectionAssert.AreEqual(new[] { "kernel", "target" }, visitor.Names);
        }
    }
}
