using Tesearis.HlslParser.Diagnostics;
using Tesearis.HlslParser.Lexing;
using Tesearis.HlslParser.Preprocessing;
using Tesearis.HlslParser.Text;
using NUnit.Framework;

namespace Tesearis.HlslParser.Tests.Preprocessing
{
    /// <summary>
    /// Covers <c>MacroTable</c>'s <c>#define</c>/<c>#undef</c>/redefinition bookkeeping —
    /// exercised through the public <see cref="Preprocessor"/> surface, since the table itself
    /// is an internal implementation detail (matching how this project's other internal helper
    /// types, e.g. <c>PunctuationText</c>/<c>StringEscapes</c> in <c>Lexing/</c>, are covered
    /// indirectly through their public entry point rather than via <c>InternalsVisibleTo</c>).
    /// </summary>
    [TestFixture]
    public class MacroTableTests
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

        [Test]
        public void IdenticalRedefinitionIsSilentlyAccepted()
        {
            var tokens = PreprocessWithDiagnostics("#define A 1\n#define A 1\nA", out var sink);

            Assert.AreEqual(0, sink.Diagnostics.Count);
            Assert.AreEqual(HlslTokenKind.IntegerLiteral, tokens[0].Kind);
            Assert.AreEqual(1ul, tokens[0].IntegerValue);
        }

        [Test]
        public void DifferingRedefinitionReportsWarningAndNewDefinitionWins()
        {
            var tokens = PreprocessWithDiagnostics("#define A 1\n#define A 2\nA", out var sink);

            Assert.AreEqual(1, sink.Diagnostics.Count);
            Assert.AreEqual(DiagnosticSeverity.Warning, sink.Diagnostics[0].Severity);
            Assert.AreEqual(DiagnosticIds.MacroRedefinition, sink.Diagnostics[0].Id);

            Assert.AreEqual(HlslTokenKind.IntegerLiteral, tokens[0].Kind);
            Assert.AreEqual(2ul, tokens[0].IntegerValue);
        }

        [Test]
        public void UndefRemovesADefinedMacro()
        {
            var tokens = Preprocess("#define A 1\n#undef A\nA");

            Assert.AreEqual(HlslTokenKind.Identifier, tokens[0].Kind);
            Assert.AreEqual("A", tokens[0].Text);
        }

        [Test]
        public void UndefOfAnUndefinedNameIsSilentlyAccepted()
        {
            Token[] tokens = null;
            DiagnosticSink sink = null;
            Assert.DoesNotThrow(() => tokens = PreprocessWithDiagnostics("#undef NEVER_DEFINED\nfoo", out sink));

            Assert.AreEqual(0, sink.Diagnostics.Count);
            Assert.AreEqual("foo", tokens[0].Text);
        }
    }
}
