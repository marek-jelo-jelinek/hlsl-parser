using System.Collections.Generic;
using Tesearis.HlslParser.Diagnostics;
using Tesearis.HlslParser.Lexing;
using Tesearis.HlslParser.Preprocessing;
using Tesearis.HlslParser.Text;
using NUnit.Framework;

namespace Tesearis.HlslParser.Tests.Preprocessing
{
    /// <summary>Covers purely-syntactic <c>#include</c> recognition — never opened, never
    /// resolved, never spliced into the token stream.</summary>
    [TestFixture]
    public class IncludeDirectiveTests
    {
        private static IReadOnlyList<IncludeDirective> PreprocessAndGetIncludes(string text, out DiagnosticSink diagnostics)
        {
            var source = new SourceText(text, "test.hlsl");
            diagnostics = new DiagnosticSink(source);
            var tokens = new Lexer(source, diagnostics).Tokenize();
            var preprocessor = new Preprocessor(source, diagnostics);
            preprocessor.Process(tokens);
            return preprocessor.Includes;
        }

        [Test]
        public void QuotedPathIsRecognized()
        {
            var includes = PreprocessAndGetIncludes("#include \"foo.cginc\"", out var diagnostics);

            Assert.AreEqual(0, diagnostics.Diagnostics.Count);
            Assert.AreEqual(1, includes.Count);
            Assert.AreEqual("foo.cginc", includes[0].Path);
            Assert.AreEqual(IncludeKind.Quoted, includes[0].Kind);
        }

        [Test]
        public void AngleBracketedPathIsRecognized()
        {
            var includes = PreprocessAndGetIncludes("#include <foo/bar.hlsli>", out var diagnostics);

            Assert.AreEqual(0, diagnostics.Diagnostics.Count);
            Assert.AreEqual(1, includes.Count);
            Assert.AreEqual("foo/bar.hlsli", includes[0].Path);
            Assert.AreEqual(IncludeKind.AngleBracketed, includes[0].Kind);
        }

        [Test]
        public void PathContainingSlashIsPreservedVerbatim()
        {
            var includes = PreprocessAndGetIncludes("#include \"Packages/com.unity/UnityCG.cginc\"", out _);

            Assert.AreEqual("Packages/com.unity/UnityCG.cginc", includes[0].Path);
        }

        [Test]
        public void MissingPathReportsMalformedInclude()
        {
            IReadOnlyList<IncludeDirective> includes = null;
            DiagnosticSink sink = null;
            Assert.DoesNotThrow(() => includes = PreprocessAndGetIncludes("#include\nfoo", out sink));

            Assert.AreEqual(0, includes.Count);
            Assert.AreEqual(1, sink.Diagnostics.Count);
            Assert.AreEqual(DiagnosticIds.MalformedInclude, sink.Diagnostics[0].Id);
        }

        [Test]
        public void UnterminatedAngleBracketedPathReportsMalformedInclude()
        {
            IReadOnlyList<IncludeDirective> includes = null;
            DiagnosticSink sink = null;
            Assert.DoesNotThrow(() => includes = PreprocessAndGetIncludes("#include <foo", out sink));

            Assert.AreEqual(0, includes.Count);
            Assert.AreEqual(1, sink.Diagnostics.Count);
            Assert.AreEqual(DiagnosticIds.MalformedInclude, sink.Diagnostics[0].Id);
        }

        [Test]
        public void UnrecognizablePathShapeReportsMalformedInclude()
        {
            IReadOnlyList<IncludeDirective> includes = null;
            DiagnosticSink sink = null;
            Assert.DoesNotThrow(() => includes = PreprocessAndGetIncludes("#include FOO\n", out sink));

            Assert.AreEqual(0, includes.Count);
            Assert.AreEqual(1, sink.Diagnostics.Count);
            Assert.AreEqual(DiagnosticIds.MalformedInclude, sink.Diagnostics[0].Id);
        }

        [Test]
        public void IncludeInsideDeadBranchProducesNoDirectiveAndNoDiagnostic()
        {
            IReadOnlyList<IncludeDirective> includes = null;
            DiagnosticSink sink = null;
            Assert.DoesNotThrow(() => includes = PreprocessAndGetIncludes("#if 0\n#include \"x.cginc\"\n#endif", out sink));

            Assert.AreEqual(0, includes.Count);
            Assert.AreEqual(0, sink.Diagnostics.Count);
        }
    }
}
