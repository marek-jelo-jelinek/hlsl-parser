using System;
using System.Linq;
using HlslParser.Diagnostics;
using HlslParser.Parsing;
using HlslParser.Syntax;
using NUnit.Framework;

namespace HlslParser.Tests.Parsing
{
    /// <summary>End-to-end tests over the public <see cref="Hlsl"/> entry point: never-null-root
    /// contract, dialect defaulting, the <c>ParseEmbedded</c> BaseOffset seam, and proof the
    /// preprocessor genuinely runs before the parser.</summary>
    [TestFixture]
    public class HlslTests
    {
        [Test]
        public void ParseEmptyStringYieldsNonNullEmptyCompilationUnit()
        {
            var result = Hlsl.Parse("");
            Assert.IsNotNull(result.Root);
            var unit = (CompilationUnitNode)result.Root;
            Assert.AreEqual(0, unit.Declarations.Count);
            Assert.IsFalse(result.HasErrors);
        }

        [Test]
        public void ParseWhitespaceOnlyYieldsNonNullEmptyCompilationUnit()
        {
            var result = Hlsl.Parse("   \n\t  \n");
            Assert.IsNotNull(result.Root);
            Assert.AreEqual(0, ((CompilationUnitNode)result.Root).Declarations.Count);
        }

        [Test]
        public void ParseGarbageYieldsNonNullPartialTreePlusDiagnostics()
        {
            var result = Hlsl.Parse("@@@ &&& $$$");
            Assert.IsNotNull(result.Root);
            Assert.IsTrue(result.HasErrors);
        }

        [Test]
        public void ParseNullTextIsTreatedAsEmpty()
        {
            var result = Hlsl.Parse((string)null);
            Assert.IsNotNull(result.Root);
            Assert.AreEqual(0, ((CompilationUnitNode)result.Root).Declarations.Count);
        }
        
        [Test]
        public void FileNameDefaultsToUnknownMarker()
        {
            var result = Hlsl.Parse("struct S { float4 x; };");
            Assert.AreEqual("<unknown>", result.Source.FileName);
        }

        [Test]
        public void FileNameIsThreadedThrough()
        {
            var result = Hlsl.Parse("struct S { float4 x; };", "shader.hlsl");
            Assert.AreEqual("shader.hlsl", result.Source.FileName);
        }
        
        [Test]
        public void ParseEmbeddedAppliesBaseOffsetToNodeSpans()
        {
            const string body = "float4 x;";
            const int baseOffset = 100;

            var result = Hlsl.ParseEmbedded(body, baseOffset, "outer.shader");

            Assert.AreEqual(baseOffset, result.Source.BaseOffset);
            var declaration = ((CompilationUnitNode)result.Root).Declarations[0];
            Assert.AreEqual(baseOffset, declaration.Span.Start);
        }

        [Test]
        public void ParseEmbeddedAppliesBaseOffsetToDiagnosticPositions()
        {
            const string body = "float4 ;"; // missing declarator name -> a diagnostic at the ';'
            const int baseOffset = 50;

            var result = Hlsl.ParseEmbedded(body, baseOffset, "outer.shader");

            Assert.IsTrue(result.HasErrors);
            var diagnostic = result.Diagnostics.First(d => d.Severity == DiagnosticSeverity.Error);
            Assert.GreaterOrEqual(diagnostic.Span.Start, baseOffset);
        }

        [Test]
        public void ParseEmbeddedAppliesBaseLineToDiagnosticLineNumbers()
        {
            // Simulates a HLSLPROGRAM block starting on line 7 (0-based line index 6) of some
            // outer .shader file — a diagnostic on the body's own 2nd line (0-based line 1) must
            // report the OUTER file's line 8, not the body-local line 2. This is the exact defect
            // BaseLine exists to catch: BaseOffset alone gets the absolute character offset right,
            // but without BaseLine, LinePosition silently stays local to the embedded body.
            const string body = "float4 ok;\nfloat4 ;"; // 2nd line: missing declarator name
            const int baseOffset = 1000;
            const int baseLine = 6;

            var result = Hlsl.ParseEmbedded(body, baseOffset, "outer.shader", baseLine);

            Assert.IsTrue(result.HasErrors);
            var diagnostic = result.Diagnostics.First(d => d.Severity == DiagnosticSeverity.Error);
            Assert.AreEqual(8, diagnostic.Position.Line); // baseLine (6) + local line index (1) + 1
        }

        [Test]
        public void ParseEmbeddedTreatsNullBodyAsEmpty()
        {
            var result = Hlsl.ParseEmbedded(null, 10, "outer.shader");
            Assert.IsNotNull(result.Root);
            Assert.AreEqual(0, ((CompilationUnitNode)result.Root).Declarations.Count);
        }
        
        [Test]
        public void PreprocessorRunsBeforeParsing()
        {
            var result = Hlsl.Parse("#define WIDTH 8\n[numthreads(WIDTH,1,1)]\nvoid CSMain() {}\n");

            Assert.IsFalse(result.HasErrors);
            var function = (FunctionDeclarationNode)((CompilationUnitNode)result.Root).Declarations[0];
            var argument = function.Attributes[0].Arguments[0];
            Assert.AreEqual("8", argument.RawText);
        }
        
        [Test]
        public void ComputeShaderWithPragmasParsesCleanly()
        {
            const string code = @"#pragma kernel CSMain
#pragma multi_compile _ FEATURE_A

[numthreads(8,8,1)]
void CSMain() {}
";
            var result = Hlsl.Parse(code, "compute.hlsl");

            Assert.IsFalse(result.HasErrors);
            Assert.AreEqual(2, result.Pragmas.Count);
            Assert.AreEqual("kernel", result.Pragmas[0].Name);
            Assert.AreEqual("CSMain", result.Pragmas[0].Arguments[0]);
            Assert.AreEqual("multi_compile", result.Pragmas[1].Name);
            CollectionAssert.AreEqual(new[] { "_", "FEATURE_A" }, result.Pragmas[1].Arguments);

            var unit = (CompilationUnitNode)result.Root;
            Assert.AreEqual(1, unit.Declarations.Count);
            Assert.IsInstanceOf<FunctionDeclarationNode>(unit.Declarations[0]);
        }

        [Test]
        public void ParseSourceTextThrowsOnNull()
        {
            Assert.Throws<ArgumentNullException>(() => Hlsl.Parse(null));
        }
    }
}
