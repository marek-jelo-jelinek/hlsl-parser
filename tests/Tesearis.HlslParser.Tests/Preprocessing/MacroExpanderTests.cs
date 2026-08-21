using System.Linq;
using System.Text;
using Tesearis.HlslParser.Diagnostics;
using Tesearis.HlslParser.Lexing;
using Tesearis.HlslParser.Preprocessing;
using Tesearis.HlslParser.Text;
using NUnit.Framework;

namespace Tesearis.HlslParser.Tests.Preprocessing
{
    /// <summary>Covers object-like/function-like macro expansion, the hide-set recursion guard,
    /// and <c>#</c> stringize / <c>##</c> token-paste — exercised through the public
    /// <see cref="Preprocessor"/> surface (the expansion engine itself is an internal type).</summary>
    [TestFixture]
    public class MacroExpanderTests
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
        public void ObjectLikeMacroExpandsToItsMultiTokenReplacement()
        {
            var tokens = Preprocess("#define FOO 1 + 2\nFOO");

            Assert.AreEqual(HlslTokenKind.IntegerLiteral, tokens[0].Kind);
            Assert.AreEqual(HlslTokenKind.Plus, tokens[1].Kind);
            Assert.AreEqual(HlslTokenKind.IntegerLiteral, tokens[2].Kind);
            Assert.AreEqual(HlslTokenKind.EndOfFile, tokens[3].Kind);
        }
        
        [Test]
        public void FunctionLikeMacroExpandsWithArgumentSubstitution()
        {
            var tokens = Preprocess("#define ADD(a, b) ((a) + (b))\nADD(1, 2)");
            var expected = Preprocess("((1) + (2))");

            Assert.AreEqual(expected.Length, tokens.Length);
            for (var i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i].Kind, tokens[i].Kind, "token " + i);
        }

        [Test]
        public void CommaInsideNestedParensDoesNotSplitArguments()
        {
            var tokens = Preprocess("#define ID(x) x\nID((1,2))");

            Assert.AreEqual(HlslTokenKind.OpenParen, tokens[0].Kind);
            Assert.AreEqual(HlslTokenKind.IntegerLiteral, tokens[1].Kind);
            Assert.AreEqual(1ul, tokens[1].IntegerValue);
            Assert.AreEqual(HlslTokenKind.Comma, tokens[2].Kind);
            Assert.AreEqual(HlslTokenKind.IntegerLiteral, tokens[3].Kind);
            Assert.AreEqual(2ul, tokens[3].IntegerValue);
            Assert.AreEqual(HlslTokenKind.CloseParen, tokens[4].Kind);
            Assert.AreEqual(HlslTokenKind.EndOfFile, tokens[5].Kind);
        }

        [Test]
        public void FunctionLikeMacroNameNotFollowedByOpenParenStaysUnexpanded()
        {
            var tokens = Preprocess("#define F(x) x\nF + 1");

            Assert.AreEqual(HlslTokenKind.Identifier, tokens[0].Kind);
            Assert.AreEqual("F", tokens[0].Text);
            Assert.AreEqual(HlslTokenKind.Plus, tokens[1].Kind);
        }

        [Test]
        public void ZeroParameterMacroCalledWithEmptyParensIsNotAnArgumentCountMismatch()
        {
            DiagnosticSink sink = null;
            Token[] tokens = null;
            Assert.DoesNotThrow(() => tokens = PreprocessWithDiagnostics("#define F() 1\nF()", out sink));

            Assert.AreEqual(0, sink.Diagnostics.Count);
            Assert.AreEqual(HlslTokenKind.IntegerLiteral, tokens[0].Kind);
            Assert.AreEqual(1ul, tokens[0].IntegerValue);
        }

        [TestCase("#define F(a, b) a + b\nF(1)", 1)]
        [TestCase("#define F(a, b) a + b\nF(1, 2, 3)", 1)]
        public void ArgumentCountMismatchReportsErrorAndLeavesCallUnexpanded(string text, int expectedDiagnosticCount)
        {
            DiagnosticSink sink = null;
            Assert.DoesNotThrow(() => PreprocessWithDiagnostics(text, out sink));

            Assert.AreEqual(expectedDiagnosticCount, sink.Diagnostics.Count);
            Assert.AreEqual(DiagnosticIds.MacroArgumentCountMismatch, sink.Diagnostics[0].Id);
            Assert.AreEqual(DiagnosticSeverity.Error, sink.Diagnostics[0].Severity);
        }

        [Test]
        public void UnterminatedInvocationReportsErrorAndPreservesOriginalTokens()
        {
            DiagnosticSink sink = null;
            Token[] tokens = null;
            Assert.DoesNotThrow(() => tokens = PreprocessWithDiagnostics("#define F(x) x\nF(1", out sink));

            Assert.AreEqual(1, sink.Diagnostics.Count);
            Assert.AreEqual(DiagnosticIds.UnterminatedMacroInvocation, sink.Diagnostics[0].Id);

            Assert.AreEqual(HlslTokenKind.Identifier, tokens[0].Kind);
            Assert.AreEqual("F", tokens[0].Text);
            Assert.AreEqual(HlslTokenKind.OpenParen, tokens[1].Kind);
            Assert.AreEqual(HlslTokenKind.IntegerLiteral, tokens[2].Kind);
        }
        
        [Test]
        public void SelfReferentialObjectLikeMacroTerminatesWithoutHanging()
        {
            Token[] tokens = null;
            Assert.DoesNotThrow(() => tokens = Preprocess("#define A A\nA"));

            Assert.AreEqual(HlslTokenKind.Identifier, tokens[0].Kind);
            Assert.AreEqual("A", tokens[0].Text);
            Assert.AreEqual(HlslTokenKind.EndOfFile, tokens[1].Kind);
        }

        [Test]
        public void MutuallyRecursiveMacrosTerminateWithoutHanging()
        {
            Token[] tokens = null;
            Assert.DoesNotThrow(() => tokens = Preprocess("#define A B\n#define B A\nA"));

            Assert.AreEqual(HlslTokenKind.Identifier, tokens[0].Kind);
            Assert.AreEqual("A", tokens[0].Text);
        }

        [Test]
        public void DeepNonSelfReferentialChainTripsExpansionDepthLimit()
        {
            const int chainLength = 100; // comfortably beyond MaxExpansionDepth
            var builder = new StringBuilder();
            for (var i = 0; i < chainLength; i++)
                builder.Append("#define M").Append(i).Append(" M").Append(i + 1).Append('\n');
            builder.Append("#define M").Append(chainLength).Append(" 0\n");
            builder.Append("M0");

            DiagnosticSink sink = null;
            Assert.DoesNotThrow(() => PreprocessWithDiagnostics(builder.ToString(), out sink));

            Assert.IsTrue(sink.Diagnostics.Any(d => d.Id == DiagnosticIds.RecursiveMacroExpansionLimitExceeded));
            Assert.IsTrue(sink.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Warning));
        }
        
        [Test]
        public void StringizeCollapsesInternalWhitespaceToSingleSpaces()
        {
            var tokens = Preprocess("#define STR(x) #x\nSTR(1 + 2)");

            Assert.AreEqual(HlslTokenKind.StringLiteral, tokens[0].Kind);
            Assert.AreEqual("1 + 2", tokens[0].Value);
        }

        [Test]
        public void StringizeInsertsNoSpaceBetweenAdjacentTokens()
        {
            var tokens = Preprocess("#define STR(x) #x\nSTR(1+2)");

            Assert.AreEqual(HlslTokenKind.StringLiteral, tokens[0].Kind);
            Assert.AreEqual("1+2", tokens[0].Value);
        }

        [Test]
        public void StringizeEscapesQuotesAndBackslashesInAStringLiteralArgument()
        {
            var tokens = Preprocess("#define STR(x) #x\nSTR(\"hi\")");

            Assert.AreEqual(HlslTokenKind.StringLiteral, tokens[0].Kind);
            Assert.AreEqual("\"hi\"", tokens[0].Value);
            Assert.AreEqual("\"\\\"hi\\\"\"", tokens[0].Text);
        }

        [Test]
        public void HashNotFollowedByParameterReportsMalformedStringizeOperator()
        {
            DiagnosticSink sink = null;
            Assert.DoesNotThrow(() => PreprocessWithDiagnostics("#define BAD(x) # + x\n", out sink));

            Assert.AreEqual(1, sink.Diagnostics.Count);
            Assert.AreEqual(DiagnosticIds.MalformedStringizeOperator, sink.Diagnostics[0].Id);
        }

        [Test]
        public void HashInsideObjectLikeMacroReportsMalformedStringizeOperator()
        {
            DiagnosticSink sink = null;
            Assert.DoesNotThrow(() => PreprocessWithDiagnostics("#define BAD #x\n", out sink));

            Assert.AreEqual(1, sink.Diagnostics.Count);
            Assert.AreEqual(DiagnosticIds.MalformedStringizeOperator, sink.Diagnostics[0].Id);
        }
        
        [Test]
        public void PasteCombinesTwoIdentifierArgumentsIntoOne()
        {
            var tokens = Preprocess("#define CAT(a, b) a##b\nCAT(foo, bar)");

            Assert.AreEqual(HlslTokenKind.Identifier, tokens[0].Kind);
            Assert.AreEqual("foobar", tokens[0].Text);
            Assert.AreEqual(HlslTokenKind.EndOfFile, tokens[1].Kind);
        }

        [Test]
        public void PasteCombinesTwoNumericLiteralsIntoOne()
        {
            var tokens = Preprocess("#define CAT(a, b) a##b\nCAT(1, 2)");

            Assert.AreEqual(HlslTokenKind.IntegerLiteral, tokens[0].Kind);
            Assert.AreEqual(12ul, tokens[0].IntegerValue);
        }

        [Test]
        public void ChainedPastesAssociateLeftToRight()
        {
            var tokens = Preprocess("#define CAT3(a, b, c) a##b##c\nCAT3(fo, ob, ar)");

            Assert.AreEqual(HlslTokenKind.Identifier, tokens[0].Kind);
            Assert.AreEqual("foobar", tokens[0].Text);
        }

        [Test]
        public void PasteResultThatFormsANewMacroNameGetsRescannedAndExpanded()
        {
            var tokens = Preprocess("#define AB 42\n#define CAT(a, b) a##b\nCAT(A, B)");

            Assert.AreEqual(HlslTokenKind.IntegerLiteral, tokens[0].Kind);
            Assert.AreEqual(42ul, tokens[0].IntegerValue);
        }

        [Test]
        public void InvalidPasteReportsErrorAndFallsBackToUnpastedAdjacentTokens()
        {
            DiagnosticSink sink = null;
            Token[] tokens = null;
            Assert.DoesNotThrow(() => tokens = PreprocessWithDiagnostics("#define CAT(a, b) a##b\nCAT(+, *)", out sink));

            Assert.AreEqual(1, sink.Diagnostics.Count);
            Assert.AreEqual(DiagnosticIds.MalformedTokenPaste, sink.Diagnostics[0].Id);

            Assert.AreEqual(HlslTokenKind.Plus, tokens[0].Kind);
            Assert.AreEqual(HlslTokenKind.Star, tokens[1].Kind);
        }

        [TestCase("#define BAD(a) ##a\n")]
        [TestCase("#define BAD(a) a##\n")]
        public void HashHashAtStartOrEndOfReplacementListReportsMalformedTokenPaste(string text)
        {
            DiagnosticSink sink = null;
            Assert.DoesNotThrow(() => PreprocessWithDiagnostics(text, out sink));

            Assert.AreEqual(1, sink.Diagnostics.Count);
            Assert.AreEqual(DiagnosticIds.MalformedTokenPaste, sink.Diagnostics[0].Id);
        }
    }
}
