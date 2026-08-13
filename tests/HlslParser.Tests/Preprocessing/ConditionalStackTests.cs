using System.Linq;
using HlslParser.Diagnostics;
using HlslParser.Lexing;
using HlslParser.Preprocessing;
using HlslParser.Text;
using NUnit.Framework;

namespace HlslParser.Tests.Preprocessing
{
    /// <summary>Covers nested <c>#if</c>/<c>#ifdef</c>/<c>#ifndef</c>/<c>#elif</c>/<c>#else</c>/
    /// <c>#endif</c> tracking — exercised through the public <see cref="Preprocessor"/> surface
    /// (the stack itself is an internal type).</summary>
    [TestFixture]
    public class ConditionalStackTests
    {
        private static Token[] Preprocess(string text)
        {
            var source = new SourceText(text, "test.hlsl");
            var tokens = new Lexer(source, new DiagnosticSink(source)).Tokenize();
            return new Preprocessor(source, new DiagnosticSink(source)).Process(tokens).ToArray();
        }

        private static Token[] PreprocessWithDiagnostics(string text, out DiagnosticSink diagnostics)
        {
            var source = new SourceText(text, "test.hlsl");
            diagnostics = new DiagnosticSink(source);
            var tokens = new Lexer(source, diagnostics).Tokenize();
            return new Preprocessor(source, diagnostics).Process(tokens).ToArray();
        }

        private static bool HasIdentifier(Token[] tokens, string name)
        {
            return tokens.Any(t => t.Kind == HlslTokenKind.Identifier && t.Text == name);
        }

        [Test]
        public void NestedIfsBothLiveSurviveToOutput()
        {
            var tokens = Preprocess("#if 1\n#if 1\nINNER\n#endif\n#endif");
            Assert.IsTrue(HasIdentifier(tokens, "INNER"));
        }

        [Test]
        public void ElseTogglesBetweenBranches()
        {
            var tokens = Preprocess("#if 0\nA\n#else\nB\n#endif");
            Assert.IsFalse(HasIdentifier(tokens, "A"));
            Assert.IsTrue(HasIdentifier(tokens, "B"));
        }

        [Test]
        public void ElifChainShortCircuitsAfterFirstTrueBranch()
        {
            var tokens = Preprocess("#if 0\nA\n#elif 1\nB\n#elif 1\nC\n#endif");
            Assert.IsFalse(HasIdentifier(tokens, "A"));
            Assert.IsTrue(HasIdentifier(tokens, "B"));
            Assert.IsFalse(HasIdentifier(tokens, "C"));
        }

        [Test]
        public void DeadOuterBranchSuppressesInnerMalformedConditionWithNoDiagnostic()
        {
            DiagnosticSink sink = null;
            Token[] tokens = null;
            Assert.DoesNotThrow(() => tokens = PreprocessWithDiagnostics("#if 0\n#if )\nX\n#endif\n#endif", out sink));

            Assert.AreEqual(0, sink.Diagnostics.Count);
            Assert.IsFalse(HasIdentifier(tokens, "X"));
        }

        [Test]
        public void UnbalancedElifReportsError()
        {
            DiagnosticSink sink = null;
            Assert.DoesNotThrow(() => PreprocessWithDiagnostics("#elif 1\n", out sink));

            Assert.AreEqual(1, sink.Diagnostics.Count);
            Assert.AreEqual(DiagnosticIds.UnbalancedElif, sink.Diagnostics[0].Id);
        }

        [Test]
        public void UnbalancedElseReportsError()
        {
            DiagnosticSink sink = null;
            Assert.DoesNotThrow(() => PreprocessWithDiagnostics("#else\n", out sink));

            Assert.AreEqual(1, sink.Diagnostics.Count);
            Assert.AreEqual(DiagnosticIds.UnbalancedElse, sink.Diagnostics[0].Id);
        }

        [Test]
        public void UnbalancedEndIfReportsError()
        {
            DiagnosticSink sink = null;
            Assert.DoesNotThrow(() => PreprocessWithDiagnostics("#endif\n", out sink));

            Assert.AreEqual(1, sink.Diagnostics.Count);
            Assert.AreEqual(DiagnosticIds.UnbalancedEndIf, sink.Diagnostics[0].Id);
        }

        [Test]
        public void ElifAfterElseReportsError()
        {
            DiagnosticSink sink = null;
            Assert.DoesNotThrow(() => PreprocessWithDiagnostics("#if 0\nA\n#else\nB\n#elif 1\nC\n#endif", out sink));

            Assert.AreEqual(1, sink.Diagnostics.Count);
            Assert.AreEqual(DiagnosticIds.ElifOrElseAfterElse, sink.Diagnostics[0].Id);
        }

        [Test]
        public void ElseAfterElseReportsError()
        {
            DiagnosticSink sink = null;
            Assert.DoesNotThrow(() => PreprocessWithDiagnostics("#if 0\nA\n#else\nB\n#else\nC\n#endif", out sink));

            Assert.AreEqual(1, sink.Diagnostics.Count);
            Assert.AreEqual(DiagnosticIds.ElifOrElseAfterElse, sink.Diagnostics[0].Id);
        }

        [Test]
        public void UnterminatedConditionalAtEndOfFileReportsErrorTracedToOpeningIf()
        {
            DiagnosticSink sink = null;
            Assert.DoesNotThrow(() => PreprocessWithDiagnostics("#if 1\nA\n", out sink));

            Assert.AreEqual(1, sink.Diagnostics.Count);
            Assert.AreEqual(DiagnosticIds.UnterminatedConditional, sink.Diagnostics[0].Id);
            Assert.AreEqual(1, sink.Diagnostics[0].Position.Line);
        }
    }
}
