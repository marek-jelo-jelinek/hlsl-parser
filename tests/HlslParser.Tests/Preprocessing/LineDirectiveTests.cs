using System.Linq;
using HlslParser.Diagnostics;
using HlslParser.Lexing;
using HlslParser.Parsing;
using HlslParser.Preprocessing;
using HlslParser.Syntax;
using HlslParser.Text;
using NUnit.Framework;

namespace HlslParser.Tests.Preprocessing
{
    [TestFixture]
    public class LineDirectiveTests
    {
        [Test]
        public void LineDirectiveRenumbersNextLine()
        {
            const string source = "// line 1\n#line 100\nfloat x;\nfloat y;";
            var result = Hlsl.Parse(source, "main.hlsl");

            Assert.IsFalse(result.HasErrors);
            var unit = (CompilationUnitNode)result.Root;
            Assert.AreEqual(2, unit.Declarations.Count);

            var firstVar = unit.Declarations[0];
            var secondVar = unit.Declarations[1];

            Assert.AreEqual(100, result.Source.GetLinePosition(firstVar.Span.Start).Line);
            Assert.AreEqual(101, result.Source.GetLinePosition(secondVar.Span.Start).Line);
        }

        [Test]
        public void LineDirectiveWithFileNameUpdatesSourceAndDiagnostics()
        {
            const string source = "#line 42 \"custom.hlsl\"\nfloat ;"; // intentional syntax error
            var result = Hlsl.Parse(source, "main.hlsl");

            Assert.IsTrue(result.HasErrors);
            var diagnostic = result.Diagnostics.First(d => d.Severity == DiagnosticSeverity.Error);

            Assert.AreEqual("custom.hlsl", diagnostic.FileName);
            Assert.AreEqual(42, diagnostic.Position.Line);
            Assert.IsTrue(diagnostic.ToString().StartsWith("custom.hlsl(42,"));
        }

        [Test]
        public void LineDefaultRestoresOriginalFileNameAndLineNumbering()
        {
            const string source = @"// 1
#line 100 ""custom.hlsl""
float x;
#line default
float y;
";
            var result = Hlsl.Parse(source, "main.hlsl");
            Assert.IsFalse(result.HasErrors);

            var unit = (CompilationUnitNode)result.Root;
            var decl1 = unit.Declarations[0];
            var decl2 = unit.Declarations[1];

            Assert.AreEqual("custom.hlsl", result.Source.GetFileName(decl1.Span.Start));
            Assert.AreEqual(100, result.Source.GetLinePosition(decl1.Span.Start).Line);

            Assert.AreEqual("main.hlsl", result.Source.GetFileName(decl2.Span.Start));
            Assert.AreEqual(5, result.Source.GetLinePosition(decl2.Span.Start).Line);
        }

        [Test]
        public void LineHiddenIsAcceptedWithoutErrors()
        {
            const string source = "#line hidden\nfloat x;";
            var result = Hlsl.Parse(source);

            Assert.IsFalse(result.HasErrors);
            var unit = (CompilationUnitNode)result.Root;
            Assert.AreEqual(1, unit.Declarations.Count);
        }

        [Test]
        public void LineDirectiveInsideDeadBranchIsIgnored()
        {
            const string source = @"// 1
#if 0
#line 500 ""dead.hlsl""
#endif
float x;
";
            var result = Hlsl.Parse(source, "main.hlsl");
            Assert.IsFalse(result.HasErrors);

            var unit = (CompilationUnitNode)result.Root;
            var decl = unit.Declarations[0];

            Assert.AreEqual("main.hlsl", result.Source.GetFileName(decl.Span.Start));
            Assert.AreEqual(5, result.Source.GetLinePosition(decl.Span.Start).Line);
        }

        [Test]
        public void MissingLineNumberReportsMalformedLineDirective()
        {
            const string source = "#line\nfloat x;";
            var result = Hlsl.Parse(source);

            Assert.IsTrue(result.Diagnostics.Any(d => d.Id == DiagnosticIds.MalformedLineDirective));
        }

        [Test]
        public void UnrecognizedLineTokenReportsMalformedLineDirective()
        {
            const string source = "#line invalid\nfloat x;";
            var result = Hlsl.Parse(source);

            Assert.IsTrue(result.Diagnostics.Any(d => d.Id == DiagnosticIds.MalformedLineDirective));
        }

        [Test]
        public void NonStringFileNameReportsMalformedLineDirective()
        {
            const string source = "#line 100 invalid_file\nfloat x;";
            var result = Hlsl.Parse(source);

            Assert.IsTrue(result.Diagnostics.Any(d => d.Id == DiagnosticIds.MalformedLineDirective));
        }
    }
}
