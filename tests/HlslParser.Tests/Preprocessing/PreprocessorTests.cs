using System;
using System.Linq;
using HlslParser.Diagnostics;
using HlslParser.Lexing;
using HlslParser.Preprocessing;
using HlslParser.Text;
using NUnit.Framework;

namespace HlslParser.Tests.Preprocessing
{
    /// <summary>End-to-end integration tests over the public <see cref="Preprocessor"/> entry
    /// point, proving the <see cref="SourceText.BaseOffset"/> seam survives macro expansion/
    /// conditional exclusion.</summary>
    [TestFixture]
    public class PreprocessorTests
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
        public void ObjectLikeDefineAndUndefRoundTrip()
        {
            var tokens = Preprocess("#define WIDTH 1920\nWIDTH\n#undef WIDTH\nWIDTH");

            Assert.AreEqual(HlslTokenKind.IntegerLiteral, tokens[0].Kind);
            Assert.AreEqual(1920ul, tokens[0].IntegerValue);
            Assert.AreEqual(HlslTokenKind.Identifier, tokens[1].Kind);
            Assert.AreEqual("WIDTH", tokens[1].Text);
        }

        [Test]
        public void FunctionLikeMacroUsedInsideAnExpression()
        {
            var tokens = Preprocess("#define MAX(a, b) ((a) > (b) ? (a) : (b))\nfloat x = MAX(1, 2);");
            var expected = Preprocess("float x = ((1) > (2) ? (1) : (2));");

            Assert.AreEqual(expected.Length, tokens.Length);
            for (var i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i].Kind, tokens[i].Kind, "token " + i);
        }

        [TestCase(202140, true)]
        [TestCase(202100, false)]
        public void UnityVersionGatingSelectsExpectedBranch(int unityVersion, bool expectNewPath)
        {
            var text = "#define UNITY_VERSION " + unityVersion + "\n" +
                       "#if UNITY_VERSION >= 202120\n" +
                       "NEW_PATH\n" +
                       "#else\n" +
                       "OLD_PATH\n" +
                       "#endif";

            var tokens = Preprocess(text);

            Assert.AreEqual(expectNewPath, HasIdentifier(tokens, "NEW_PATH"));
            Assert.AreEqual(!expectNewPath, HasIdentifier(tokens, "OLD_PATH"));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DefineBasedFeatureGatingSelectsExpectedBranch(bool defineFeature)
        {
            var text = (defineFeature ? "#define FEATURE_X\n" : "") +
                       "#ifdef FEATURE_X\n" +
                       "WITH_FEATURE\n" +
                       "#else\n" +
                       "WITHOUT_FEATURE\n" +
                       "#endif";

            var tokens = Preprocess(text);

            Assert.AreEqual(defineFeature, HasIdentifier(tokens, "WITH_FEATURE"));
            Assert.AreEqual(!defineFeature, HasIdentifier(tokens, "WITHOUT_FEATURE"));
        }

        [Test]
        public void NestedConditionalsCombineWithMacroExpansionInsideLiveBranch()
        {
            var tokens = Preprocess(
                "#define VALUE 42\n" +
                "#if 1\n" +
                "#if 1\n" +
                "VALUE\n" +
                "#endif\n" +
                "#endif");

            Assert.AreEqual(HlslTokenKind.IntegerLiteral, tokens[0].Kind);
            Assert.AreEqual(42ul, tokens[0].IntegerValue);
        }

        [Test]
        public void MalformedMacroDefinitionReportsDiagnosticWithoutThrowing()
        {
            DiagnosticSink sink = null;
            Assert.DoesNotThrow(() => PreprocessWithDiagnostics("#define\nfoo", out sink));

            Assert.AreEqual(1, sink.Diagnostics.Count);
            Assert.AreEqual(DiagnosticIds.MalformedMacroDefinition, sink.Diagnostics[0].Id);
            Assert.AreEqual(DiagnosticSeverity.Error, sink.Diagnostics[0].Severity);
        }

        [Test]
        public void BaseOffsetSurvivesMacroExpansionAndConditionalExclusion()
        {
            const int baseOffset = 500;
            var body = "#define VALUE 7\n#if 1\nVALUE\n#else\nDEAD\n#endif\n@";
            var source = new SourceText(body, "Shader.shader", baseOffset);
            var diagnostics = new DiagnosticSink(source);
            var tokens = new Lexer(source, diagnostics).Tokenize();
            var result = new Preprocessor(source, diagnostics).Process(tokens);

            foreach (var token in result)
            {
                if (token.Kind == HlslTokenKind.EndOfFile) continue;
                Assert.GreaterOrEqual(token.Span.Start, baseOffset, "token " + token + " should carry an absolute span");
            }

            Assert.AreEqual(1, diagnostics.Diagnostics.Count); // the trailing '@' is unrecognized
            Assert.GreaterOrEqual(diagnostics.Diagnostics[0].Span.Start, baseOffset);

            var expanded = result.First(t => t.Kind == HlslTokenKind.IntegerLiteral);
            Assert.AreEqual(7ul, expanded.IntegerValue);
        }

        [Test]
        public void ConstructorThrowsOnNullSource()
        {
            Assert.Throws<ArgumentNullException>(() => new Preprocessor(null, null));
        }

        [Test]
        public void ProcessThrowsOnNullTokenList()
        {
            var source = new SourceText("", "test.hlsl");
            var preprocessor = new Preprocessor(source, new DiagnosticSink(source));
            Assert.Throws<ArgumentNullException>(() => preprocessor.Process(null));
        }

        [Test]
        public void ComputeShaderFeatureGatingSnippetPreprocessesCleanly()
        {
            const string snippet = @"
#define THREAD_GROUP_SIZE 64
#define SQUARE(x) ((x) * (x))

#pragma kernel CSMain

#if defined(USE_HALF_PRECISION)
    #define ScalarType half
#else
    #define ScalarType float
#endif

RWStructuredBuffer<ScalarType> _Buffer;

[numthreads(THREAD_GROUP_SIZE, 1, 1)]
void CSMain(uint3 id : SV_DispatchThreadID)
{
    _Buffer[id.x] = SQUARE(_Buffer[id.x]);
}
";
            DiagnosticSink sink = null;
            Token[] tokens = null;
            Assert.DoesNotThrow(() => tokens = PreprocessWithDiagnostics(snippet, out sink));

            // #pragma is an unrecognized directive at this phase — advisory Info only.
            Assert.IsTrue(sink.Diagnostics.All(d => d.Severity != DiagnosticSeverity.Error), "no Error diagnostics expected");
            Assert.IsTrue(tokens.Any(t => t.Kind == HlslTokenKind.Keyword && t.Text == "float"), "ScalarType should have expanded to 'float'");
            Assert.IsFalse(tokens.Any(t => t.Kind == HlslTokenKind.Identifier && t.Text == "ScalarType"), "ScalarType itself should not survive");
            Assert.IsFalse(tokens.Any(t => t.Kind == HlslTokenKind.Hash), "no directive tokens should survive into the output");
        }
    }
}