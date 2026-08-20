using System.Linq;
using HlslParser.Diagnostics;
using HlslParser.Parsing;
using NUnit.Framework;

namespace HlslParser.Tests.Preprocessing
{
    [TestFixture]
    public class ErrorAndWarningDirectiveTests
    {
        [Test]
        public void ErrorDirectiveEmitsErrorDiagnostic()
        {
            const string source = "#error Shader model 5.0 required\nvoid main() {}";
            var result = Hlsl.Parse(source);

            Assert.IsTrue(result.HasErrors);
            var diagnostic = result.Diagnostics.FirstOrDefault(d => d.Id == DiagnosticIds.PreprocessorErrorDirective);

            Assert.IsNotNull(diagnostic);
            Assert.AreEqual(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.AreEqual("Shader model 5.0 required", diagnostic.Message);
        }

        [Test]
        public void WarningDirectiveEmitsWarningDiagnostic()
        {
            const string source = "#warning Feature X is deprecated\nvoid main() {}";
            var result = Hlsl.Parse(source);

            Assert.IsFalse(result.HasErrors);
            var diagnostic = result.Diagnostics.FirstOrDefault(d => d.Id == DiagnosticIds.PreprocessorWarningDirective);

            Assert.IsNotNull(diagnostic);
            Assert.AreEqual(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.AreEqual("Feature X is deprecated", diagnostic.Message);
        }

        [Test]
        public void ErrorAndWarningInsideDeadBranchAreSuppressed()
        {
            const string source = @"
#if 0
#error This should not fire
#warning This should not fire either
#endif
void main() {}
";
            var result = Hlsl.Parse(source);

            Assert.IsFalse(result.HasErrors);
            Assert.AreEqual(0, result.Diagnostics.Count);
        }

        [Test]
        public void EmptyErrorAndWarningDirectivesHandledGracefully()
        {
            const string source = "#warning\n#error\nvoid main() {}";
            var result = Hlsl.Parse(source);

            var warning = result.Diagnostics.FirstOrDefault(d => d.Id == DiagnosticIds.PreprocessorWarningDirective);
            var error = result.Diagnostics.FirstOrDefault(d => d.Id == DiagnosticIds.PreprocessorErrorDirective);

            Assert.IsNotNull(warning);
            Assert.AreEqual(string.Empty, warning.Message);

            Assert.IsNotNull(error);
            Assert.AreEqual(string.Empty, error.Message);
        }
    }
}
