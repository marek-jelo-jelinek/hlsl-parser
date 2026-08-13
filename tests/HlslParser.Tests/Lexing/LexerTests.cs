using System;
using System.Collections.Generic;
using System.Linq;
using HlslParser.Diagnostics;
using HlslParser.Lexing;
using HlslParser.Text;
using NUnit.Framework;

namespace HlslParser.Tests.Lexing
{
    [TestFixture]
    public class LexerTests
    {
        private static Token[] Lex(string text)
        {
            var source = new SourceText(text, "test.hlsl");
            var lexer = new Lexer(source, new DiagnosticSink(source));
            return lexer.Tokenize().ToArray();
        }

        private static Token[] LexWithSource(string text, out SourceText source)
        {
            source = new SourceText(text, "test.hlsl");
            var lexer = new Lexer(source, new DiagnosticSink(source));
            return lexer.Tokenize().ToArray();
        }

        private static Token[] LexWithDiagnostics(string text, out DiagnosticSink diagnostics)
        {
            var source = new SourceText(text, "test.hlsl");
            diagnostics = new DiagnosticSink(source);
            var lexer = new Lexer(source, diagnostics);
            return lexer.Tokenize().ToArray();
        }

        private static Token[] LexWithBaseOffset(string text, int baseOffset, out SourceText source, out DiagnosticSink diagnostics)
        {
            source = new SourceText(text, "test.hlsl", baseOffset);
            diagnostics = new DiagnosticSink(source);
            var lexer = new Lexer(source, diagnostics);
            return lexer.Tokenize().ToArray();
        }
        
        private static IEnumerable<string> SystematicTypeKeywords()
        {
            string[] baseTypes = { "float", "int", "uint", "bool", "half", "double" };
            foreach (var b in baseTypes)
            {
                for (var n = 1; n <= 4; n++) yield return b + n;
                for (var r = 1; r <= 4; r++) for (var c = 1; c <= 4; c++) yield return b + r + "x" + c;
            }
        }

        private static readonly string[] OtherKeywords =
        {
            "void", "bool", "int", "uint", "dword", "half", "float", "double", "string",
            "Texture2D", "RWStructuredBuffer", "SamplerState", "sampler2D", "cbuffer", "ConstantBuffer",
            "struct", "typedef",
            "if", "else", "for", "while", "do", "switch", "case", "default", "break", "continue", "return",
            "discard",
            "static", "const", "uniform", "in", "out", "inout", "groupshared", "row_major"
        };

        private static IEnumerable<string> AllKeywords()
        {
            return SystematicTypeKeywords().Concat(OtherKeywords);
        }

        [TestCaseSource(nameof(AllKeywords))]
        public void LexesKeywordsAsKeywordTokenWithExpectedText(string text)
        {
            var tokens = Lex(text);
            Assert.AreEqual(HlslTokenKind.Keyword, tokens[0].Kind);
            Assert.AreEqual(text, tokens[0].Text);
            Assert.AreEqual(HlslTokenKind.EndOfFile, tokens[1].Kind);
        }
        
        [TestCase("_foo")]
        [TestCase("foo123")]
        [TestCase("_1")]
        [TestCase("mixedCase")]
        [TestCase("x")]
        public void LexesIdentifiers(string text)
        {
            var tokens = Lex(text);
            Assert.AreEqual(HlslTokenKind.Identifier, tokens[0].Kind);
            Assert.AreEqual(text, tokens[0].Text);
        }
        
        [TestCase("0", false, 0ul, NumericLiteralSuffix.None)]
        [TestCase("123", false, 123ul, NumericLiteralSuffix.None)]
        [TestCase("123u", false, 123ul, NumericLiteralSuffix.Unsigned)]
        [TestCase("123U", false, 123ul, NumericLiteralSuffix.Unsigned)]
        [TestCase("123l", false, 123ul, NumericLiteralSuffix.Long)]
        [TestCase("123L", false, 123ul, NumericLiteralSuffix.Long)]
        [TestCase("0x1F", true, 31ul, NumericLiteralSuffix.None)]
        [TestCase("0X1f", true, 31ul, NumericLiteralSuffix.None)]
        [TestCase("0xFFu", true, 255ul, NumericLiteralSuffix.Unsigned)]
        public void LexesIntegerLiterals(string text, bool isHex, ulong expectedValue, NumericLiteralSuffix suffix)
        {
            var tokens = Lex(text);
            Assert.AreEqual(HlslTokenKind.IntegerLiteral, tokens[0].Kind);
            Assert.AreEqual(isHex, tokens[0].IsHex);
            Assert.AreEqual(expectedValue, tokens[0].IntegerValue);
            Assert.AreEqual(suffix, tokens[0].NumericSuffix);
            Assert.AreEqual(text, tokens[0].Text);
        }

        [TestCase("1.5", 1.5, NumericLiteralSuffix.None)]
        [TestCase("1.", 1.0, NumericLiteralSuffix.None)]
        [TestCase(".5", 0.5, NumericLiteralSuffix.None)]
        [TestCase("1e3", 1000.0, NumericLiteralSuffix.None)]
        [TestCase("1E-3", 0.001, NumericLiteralSuffix.None)]
        [TestCase("1.5e+2", 150.0, NumericLiteralSuffix.None)]
        [TestCase("1.0f", 1.0, NumericLiteralSuffix.Float)]
        [TestCase("1.0F", 1.0, NumericLiteralSuffix.Float)]
        [TestCase("1.0h", 1.0, NumericLiteralSuffix.Half)]
        [TestCase("1.0H", 1.0, NumericLiteralSuffix.Half)]
        public void LexesFloatLiterals(string text, double expectedValue, NumericLiteralSuffix suffix)
        {
            var tokens = Lex(text);
            Assert.AreEqual(HlslTokenKind.FloatLiteral, tokens[0].Kind);
            Assert.AreEqual(expectedValue, tokens[0].FloatValue, 1e-9);
            Assert.AreEqual(suffix, tokens[0].NumericSuffix);
            Assert.AreEqual(text, tokens[0].Text);
        }

        [Test]
        public void MinusIsNeverFoldedIntoFollowingNumericLiteral()
        {
            var tokens = Lex("-5");
            Assert.AreEqual(3, tokens.Length); // Minus, IntegerLiteral, EOF
            Assert.AreEqual(HlslTokenKind.Minus, tokens[0].Kind);
            Assert.AreEqual(HlslTokenKind.IntegerLiteral, tokens[1].Kind);
            Assert.AreEqual(5ul, tokens[1].IntegerValue);
        }
        
        [Test]
        public void LexesSimpleStringLiteral()
        {
            var tokens = Lex("\"hello\"");
            Assert.AreEqual(HlslTokenKind.StringLiteral, tokens[0].Kind);
            Assert.AreEqual("hello", tokens[0].Value);
        }

        [Test]
        public void EscapedQuoteDoesNotTerminateString()
        {
            var tokens = Lex("\"a\\\"b\"");
            Assert.AreEqual(HlslTokenKind.StringLiteral, tokens[0].Kind);
            Assert.AreEqual("a\"b", tokens[0].Value);
        }

        [Test]
        public void UnterminatedStringReportsErrorButDoesNotThrow()
        {
            Token[] tokens = null;
            DiagnosticSink sink = null;
            Assert.DoesNotThrow(() => tokens = LexWithDiagnostics("\"line1", out sink));

            Assert.AreEqual(HlslTokenKind.StringLiteral, tokens[0].Kind);
            Assert.AreEqual(1, sink.Diagnostics.Count);
            Assert.AreEqual(DiagnosticIds.UnterminatedString, sink.Diagnostics[0].Id);
            Assert.AreEqual(DiagnosticSeverity.Error, sink.Diagnostics[0].Severity);
        }
        
        [TestCase("{", HlslTokenKind.OpenBrace)]
        [TestCase("}", HlslTokenKind.CloseBrace)]
        [TestCase("(", HlslTokenKind.OpenParen)]
        [TestCase(")", HlslTokenKind.CloseParen)]
        [TestCase("[", HlslTokenKind.OpenBracket)]
        [TestCase("]", HlslTokenKind.CloseBracket)]
        [TestCase(";", HlslTokenKind.Semicolon)]
        [TestCase(",", HlslTokenKind.Comma)]
        [TestCase(".", HlslTokenKind.Dot)]
        [TestCase("?", HlslTokenKind.Question)]
        [TestCase(":", HlslTokenKind.Colon)]
        [TestCase("~", HlslTokenKind.Tilde)]
        [TestCase("=", HlslTokenKind.Equals)]
        [TestCase("==", HlslTokenKind.EqualsEquals)]
        [TestCase("!", HlslTokenKind.Exclamation)]
        [TestCase("!=", HlslTokenKind.ExclamationEquals)]
        [TestCase("+", HlslTokenKind.Plus)]
        [TestCase("++", HlslTokenKind.PlusPlus)]
        [TestCase("+=", HlslTokenKind.PlusEquals)]
        [TestCase("-", HlslTokenKind.Minus)]
        [TestCase("--", HlslTokenKind.MinusMinus)]
        [TestCase("-=", HlslTokenKind.MinusEquals)]
        [TestCase("*", HlslTokenKind.Star)]
        [TestCase("*=", HlslTokenKind.StarEquals)]
        [TestCase("/", HlslTokenKind.Slash)]
        [TestCase("/=", HlslTokenKind.SlashEquals)]
        [TestCase("%", HlslTokenKind.Percent)]
        [TestCase("%=", HlslTokenKind.PercentEquals)]
        [TestCase("&", HlslTokenKind.Ampersand)]
        [TestCase("&&", HlslTokenKind.AmpersandAmpersand)]
        [TestCase("&=", HlslTokenKind.AmpersandEquals)]
        [TestCase("|", HlslTokenKind.Pipe)]
        [TestCase("||", HlslTokenKind.PipePipe)]
        [TestCase("|=", HlslTokenKind.PipeEquals)]
        [TestCase("^", HlslTokenKind.Caret)]
        [TestCase("^=", HlslTokenKind.CaretEquals)]
        [TestCase("<", HlslTokenKind.LessThan)]
        [TestCase("<=", HlslTokenKind.LessThanEquals)]
        [TestCase("<<", HlslTokenKind.LessThanLessThan)]
        [TestCase("<<=", HlslTokenKind.LessThanLessThanEquals)]
        [TestCase(">", HlslTokenKind.GreaterThan)]
        [TestCase(">=", HlslTokenKind.GreaterThanEquals)]
        [TestCase(">>", HlslTokenKind.GreaterThanGreaterThan)]
        [TestCase(">>=", HlslTokenKind.GreaterThanGreaterThanEquals)]
        [TestCase("#", HlslTokenKind.Hash)]
        [TestCase("##", HlslTokenKind.HashHash)]
        public void LexesPunctuationAsSingleLongestMatchToken(string text, HlslTokenKind expectedKind)
        {
            var tokens = Lex(text);
            Assert.AreEqual(2, tokens.Length, "expected exactly one non-EOF token");
            Assert.AreEqual(expectedKind, tokens[0].Kind);
            Assert.AreEqual(text, tokens[0].Text);
            Assert.AreEqual(HlslTokenKind.EndOfFile, tokens[1].Kind);
        }
        
        [Test]
        public void LineCommentProducesNoTokens()
        {
            var tokens = Lex("// comment\nfoo");
            Assert.AreEqual(HlslTokenKind.Identifier, tokens[0].Kind);
            Assert.AreEqual("foo", tokens[0].Text);
            Assert.AreEqual(2, tokens[0].Line);
        }

        [Test]
        public void BlockCommentProducesNoTokens()
        {
            var tokens = Lex("/* multi\nline */bar");
            Assert.AreEqual(HlslTokenKind.Identifier, tokens[0].Kind);
            Assert.AreEqual("bar", tokens[0].Text);
            Assert.AreEqual(2, tokens[0].Line);
        }

        [Test]
        public void UnterminatedBlockCommentReportsWarningAndDoesNotThrow()
        {
            Token[] tokens = null;
            DiagnosticSink sink = null;
            Assert.DoesNotThrow(() => tokens = LexWithDiagnostics("/* unterminated", out sink));

            Assert.AreEqual(HlslTokenKind.EndOfFile, tokens[0].Kind);
            Assert.AreEqual(1, sink.Diagnostics.Count);
            Assert.AreEqual(DiagnosticSeverity.Warning, sink.Diagnostics[0].Severity);
            Assert.AreEqual(DiagnosticIds.UnterminatedBlockComment, sink.Diagnostics[0].Id);
        }
        
        [TestCase("a\\\nb")]
        [TestCase("a\\\r\nb")]
        [TestCase("a\\\rb")]
        public void BackslashNewlineSplicesTwoIdentifiersApart(string text)
        {
            Token[] tokens = null;
            DiagnosticSink sink = null;
            Assert.DoesNotThrow(() => tokens = LexWithDiagnostics(text, out sink));

            Assert.AreEqual(HlslTokenKind.Identifier, tokens[0].Kind);
            Assert.AreEqual("a", tokens[0].Text);
            Assert.AreEqual(HlslTokenKind.Identifier, tokens[1].Kind);
            Assert.AreEqual("b", tokens[1].Text);
            Assert.IsFalse(tokens[1].IsAtStartOfLine);
            Assert.AreEqual(0, sink.Diagnostics.Count);
        }

        [Test]
        public void ConsecutiveBackslashNewlinesSpliceThroughMultiplePhysicalLines()
        {
            var tokens = Lex("a\\\n\\\nb");
            Assert.AreEqual(HlslTokenKind.Identifier, tokens[0].Kind);
            Assert.AreEqual(HlslTokenKind.Identifier, tokens[1].Kind);
            Assert.AreEqual("b", tokens[1].Text);
            Assert.IsFalse(tokens[1].IsAtStartOfLine);
        }

        [Test]
        public void StrayBackslashNotFollowedByLineBreakIsUnchangedUnknownToken()
        {
            Token[] tokens = null;
            DiagnosticSink sink = null;
            Assert.DoesNotThrow(() => tokens = LexWithDiagnostics("a \\ b", out sink));

            Assert.AreEqual(HlslTokenKind.Identifier, tokens[0].Kind);
            Assert.AreEqual(HlslTokenKind.Unknown, tokens[1].Kind);
            Assert.AreEqual(HlslTokenKind.Identifier, tokens[2].Kind);
            Assert.AreEqual(1, sink.Diagnostics.Count);
            Assert.AreEqual(DiagnosticIds.UnrecognizedCharacter, sink.Diagnostics[0].Id);
        }

        [Test]
        public void BackslashAtEndOfFileIsUnknownTokenAndTokenizeStillTerminates()
        {
            Token[] tokens = null;
            DiagnosticSink sink = null;
            Assert.DoesNotThrow(() => tokens = LexWithDiagnostics("a \\", out sink));

            Assert.AreEqual(HlslTokenKind.Identifier, tokens[0].Kind);
            Assert.AreEqual(HlslTokenKind.Unknown, tokens[1].Kind);
            Assert.AreEqual(HlslTokenKind.EndOfFile, tokens[2].Kind);
            Assert.AreEqual(1, sink.Diagnostics.Count);
        }

        [Test]
        public void SplicedMacroDefinitionLineTokenizesSameAsUnsplicedOneLiner()
        {
            var spliced = Lex("#define FOO(a, b) \\\n  ((a) + (b))");
            var oneLiner = Lex("#define FOO(a, b)   ((a) + (b))");

            Assert.AreEqual(oneLiner.Length, spliced.Length);
            for (var i = 0; i < oneLiner.Length; i++)
            {
                Assert.AreEqual(oneLiner[i].Kind, spliced[i].Kind, "token " + i);
            }

            // Every span still points at its true physical source character — nothing rewritten.
            var source = new SourceText("#define FOO(a, b) \\\n  ((a) + (b))", "test.hlsl");
            foreach (var token in spliced)
            {
                if (token.Kind == HlslTokenKind.EndOfFile) continue;
                Assert.AreEqual(source.GetText(token.Span), token.Text, "token " + token);
            }
        }

        [Test]
        public void IsAtStartOfLineTruthTable()
        {
            // First token in file.
            Assert.IsTrue(Lex("foo")[0].IsAtStartOfLine);

            // After an ordinary newline.
            var afterNewline = Lex("a\nb");
            Assert.IsTrue(afterNewline[0].IsAtStartOfLine); // 'a' — first token in the file
            Assert.IsTrue(afterNewline[1].IsAtStartOfLine); // 'b' — after a real newline

            // After a spliced newline.
            Assert.IsFalse(Lex("a\\\nb")[1].IsAtStartOfLine);

            // Two tokens on the same physical line.
            Assert.IsFalse(Lex("a b")[1].IsAtStartOfLine);

            // After a standalone '//' comment line.
            Assert.IsTrue(Lex("// comment\nfoo")[0].IsAtStartOfLine);

            // After a multi-line '/* */' block comment.
            Assert.IsTrue(Lex("/* multi\nline */bar")[0].IsAtStartOfLine);
        }
        
        [TestCase("@")]
        [TestCase("$")]
        [TestCase("`")]
        public void UnrecognizedCharacterProducesUnknownTokenAndErrorButDoesNotThrow(string text)
        {
            Token[] tokens = null;
            DiagnosticSink sink = null;
            Assert.DoesNotThrow(() => tokens = LexWithDiagnostics(text, out sink));

            Assert.AreEqual(HlslTokenKind.Unknown, tokens[0].Kind);
            Assert.AreEqual(1, sink.Diagnostics.Count);
            Assert.AreEqual(DiagnosticSeverity.Error, sink.Diagnostics[0].Severity);
            Assert.AreEqual(DiagnosticIds.UnrecognizedCharacter, sink.Diagnostics[0].Id);
        }

        [Test]
        public void GarbageSoupNeverThrows()
        {
            Assert.DoesNotThrow(() => Lex("@#$%^&*()_+ float4 \"unterminated \n /* also unterminated"));
        }
        
        [Test]
        public void TokenizeAlwaysEndsWithEmptyEndOfFileToken()
        {
            var tokens = Lex("float4 x;");
            var eof = tokens[^1];
            Assert.AreEqual(HlslTokenKind.EndOfFile, eof.Kind);
            Assert.IsTrue(eof.Span.IsEmpty);
        }

        [Test]
        public void TokenTextRoundTripsThroughSourceTextGetText()
        {
            var tokens = LexWithSource("float4 _Color = float4(1,0,0,1); // comment\nstruct S { float x; };", out var source);
            foreach (var token in tokens)
            {
                if (token.Kind == HlslTokenKind.EndOfFile) continue;
                Assert.AreEqual(source.GetText(token.Span), token.Text, "token " + token);
            }
        }

        [Test]
        public void PunctuationTokensShareACachedInstanceAcrossIndependentLexRuns()
        {
            var a = Lex("{")[0];
            var b = Lex("{")[0];
            Assert.AreSame(a.Text, b.Text);
        }

        [Test]
        public void KeywordTokensShareACachedInstanceAcrossIndependentLexRuns()
        {
            var a = Lex("float4")[0];
            var b = Lex("float4")[0];
            Assert.AreSame(a.Text, b.Text);
        }

        [Test]
        public void LineAndColumnTrackAcrossMixedLineEndings()
        {
            var tokens = Lex("a\nb\r\nc");
            Assert.AreEqual(1, tokens[0].Line);
            Assert.AreEqual(1, tokens[0].Column);
            Assert.AreEqual(2, tokens[1].Line);
            Assert.AreEqual(1, tokens[1].Column);
            Assert.AreEqual(3, tokens[2].Line);
            Assert.AreEqual(1, tokens[2].Column);
        }

        [Test]
        public void BaseOffsetShiftsTokenSpansAndDiagnosticPositions()
        {
            var tokens = LexWithBaseOffset("float4 @", 100, out var source, out var diagnostics);

            Assert.AreEqual(100, tokens[0].Span.Start); // "float4"
            Assert.AreEqual("float4", source.GetText(tokens[0].Span));

            Assert.AreEqual(1, diagnostics.Diagnostics.Count);
            Assert.AreEqual(107, diagnostics.Diagnostics[0].Span.Start); // '@' is at local index 7
            Assert.AreEqual(new LinePosition(1, 8), diagnostics.Diagnostics[0].Position);
        }

        [Test]
        public void ConstructorThrowsOnNullSource()
        {
            Assert.Throws<ArgumentNullException>(() => new Lexer(null, null));
        }
        
        [Test]
        public void ComputeKernelSignatureSnippetTokenizesCleanly()
        {
            const string snippet = @"RWStructuredBuffer<float4> _Result : register(u0);

[numthreads(8,8,1)]
void CSMain(uint3 id : SV_DispatchThreadID)
{
    float4 value = float4(id.x, id.y, id.z, 1.0f);
    _Result[id.x] = value * 2.0h - 1;
}";

            var tokens = LexWithDiagnostics(snippet, out var diagnostics);

            Assert.AreEqual(0, diagnostics.Diagnostics.Count);
            Assert.IsFalse(tokens.Any(t => t.Kind == HlslTokenKind.Unknown));

            Assert.AreEqual(HlslTokenKind.Keyword, tokens[0].Kind); // RWStructuredBuffer
            Assert.AreEqual("RWStructuredBuffer", tokens[0].Text);
            Assert.AreEqual(HlslTokenKind.LessThan, tokens[1].Kind);
            Assert.AreEqual(HlslTokenKind.Keyword, tokens[2].Kind); // float4
            Assert.AreEqual(HlslTokenKind.GreaterThan, tokens[3].Kind);
            Assert.AreEqual(HlslTokenKind.Identifier, tokens[4].Kind); // _Result
            Assert.AreEqual(HlslTokenKind.Colon, tokens[5].Kind);
            Assert.AreEqual(HlslTokenKind.Identifier, tokens[6].Kind); // register: not special-cased
            Assert.AreEqual("register", tokens[6].Text);

            Assert.IsTrue(tokens.Any(t => t.Kind == HlslTokenKind.Identifier && t.Text == "numthreads"));
            Assert.IsTrue(tokens.Any(t => t.Kind == HlslTokenKind.Keyword && t.Text == "void"));
            Assert.IsTrue(tokens.Any(t => t.Kind == HlslTokenKind.Identifier && t.Text == "CSMain"));
            Assert.IsTrue(tokens.Any(t => t.Kind == HlslTokenKind.Keyword && t.Text == "uint3"));

            var suffixedFloat = tokens.First(t =>
                t.Kind == HlslTokenKind.FloatLiteral && t.NumericSuffix == NumericLiteralSuffix.Float);
            Assert.AreEqual(1.0, suffixedFloat.FloatValue);

            var suffixedHalf = tokens.First(t =>
                t.Kind == HlslTokenKind.FloatLiteral && t.NumericSuffix == NumericLiteralSuffix.Half);
            Assert.AreEqual(2.0, suffixedHalf.FloatValue);
        }

        [Test]
        public void CbufferAndStructSnippetTokenizesCleanly()
        {
            const string snippet = @"cbuffer PerFrame : register(b0)
{
    float4x4 _ViewProj;
    float3 _CameraPos;
    float _Time;
};

struct VertexInput
{
    float3 position : POSITION;
    float2 uv : TEXCOORD0;
};";

            var tokens = LexWithDiagnostics(snippet, out var diagnostics);

            Assert.AreEqual(0, diagnostics.Diagnostics.Count);
            Assert.IsFalse(tokens.Any(t => t.Kind == HlslTokenKind.Unknown));

            Assert.AreEqual(HlslTokenKind.Keyword, tokens[0].Kind); // cbuffer
            Assert.IsTrue(HlslKeywords.IsDeclarationKeyword(tokens[0].Text));

            Assert.IsTrue(tokens.Any(t => t.Kind == HlslTokenKind.Keyword && t.Text == "struct"));
            Assert.IsTrue(tokens.Any(t => t.Kind == HlslTokenKind.Keyword && t.Text == "float4x4"));
            Assert.IsTrue(tokens.Any(t => t.Kind == HlslTokenKind.Keyword && t.Text == "float3"));
            Assert.IsTrue(tokens.Any(t => t.Kind == HlslTokenKind.Keyword && t.Text == "float2"));
            Assert.IsTrue(tokens.Any(t => t.Kind == HlslTokenKind.Identifier && t.Text == "register"));
            Assert.IsTrue(tokens.Any(t => t.Kind == HlslTokenKind.Identifier && t.Text == "POSITION"));
            Assert.IsTrue(tokens.Any(t => t.Kind == HlslTokenKind.Identifier && t.Text == "TEXCOORD0"));

            // The cbuffer body's closing "}" is immediately followed by ";".
            var closeBraceIndex = Array.FindIndex(tokens, t => t.Kind == HlslTokenKind.CloseBrace);
            Assert.AreEqual(HlslTokenKind.Semicolon, tokens[closeBraceIndex + 1].Kind);
        }

        [Test]
        public void OperatorHeavyLineTokenizesWithoutMisSplittingMultiCharOperators()
        {
            const string snippet = @"uint mask = a + b * c - d / e % f;
mask <<= 2;
mask >>= 1;
bool ok = (mask & 0xF0u) != 0 && (flags | 1) == flags && !done;
flags ^= ~mask;
int n = i++ + --j;";

            var tokens = LexWithDiagnostics(snippet, out var diagnostics);

            Assert.AreEqual(0, diagnostics.Diagnostics.Count);
            Assert.IsFalse(tokens.Any(t => t.Kind == HlslTokenKind.Unknown));

            Assert.IsTrue(tokens.Any(t => t.Kind == HlslTokenKind.LessThanLessThanEquals));
            Assert.IsTrue(tokens.Any(t => t.Kind == HlslTokenKind.GreaterThanGreaterThanEquals));
            Assert.IsTrue(tokens.Any(t => t.Kind == HlslTokenKind.ExclamationEquals));
            Assert.IsTrue(tokens.Any(t => t.Kind == HlslTokenKind.EqualsEquals));
            Assert.IsTrue(tokens.Any(t => t.Kind == HlslTokenKind.AmpersandAmpersand));
            Assert.IsTrue(tokens.Any(t => t.Kind == HlslTokenKind.CaretEquals));

            var hex = tokens.First(t => t.Kind == HlslTokenKind.IntegerLiteral && t.IsHex);
            Assert.AreEqual(0xF0ul, hex.IntegerValue);
            Assert.AreEqual(NumericLiteralSuffix.Unsigned, hex.NumericSuffix);

            // "i++ + --j" must tokenize as PlusPlus, Plus, MinusMinus — three separate operator
            // tokens, never merged into "+++"/"+--".
            var iIndex = Array.FindIndex(tokens, t => t.Kind == HlslTokenKind.Identifier && t.Text == "i");
            Assert.AreEqual(HlslTokenKind.PlusPlus, tokens[iIndex + 1].Kind);
            Assert.AreEqual(HlslTokenKind.Plus, tokens[iIndex + 2].Kind);
            Assert.AreEqual(HlslTokenKind.MinusMinus, tokens[iIndex + 3].Kind);
            Assert.AreEqual(HlslTokenKind.Identifier, tokens[iIndex + 4].Kind);
            Assert.AreEqual("j", tokens[iIndex + 4].Text);
        }

        [Test]
        public void CgLegacySamplerSnippetTokenizesCleanly()
        {
            const string snippet = @"sampler2D _MainTex;
float4 frag(float2 uv : TEXCOORD0) : COLOR
{
    return tex2D(_MainTex, uv);
}";

            var tokens = LexWithDiagnostics(snippet, out var diagnostics);

            Assert.AreEqual(0, diagnostics.Diagnostics.Count);
            Assert.IsFalse(tokens.Any(t => t.Kind == HlslTokenKind.Unknown));

            Assert.AreEqual(HlslTokenKind.Keyword, tokens[0].Kind); // sampler2D
            Assert.IsTrue(HlslKeywords.IsResourceKeyword(tokens[0].Text));
            Assert.IsTrue(tokens.Any(t => t.Kind == HlslTokenKind.Keyword && t.Text == "float4"));
            Assert.IsTrue(tokens.Any(t => t.Kind == HlslTokenKind.Keyword && t.Text == "return"));
            Assert.IsTrue(tokens.Any(t => t.Kind == HlslTokenKind.Identifier && t.Text == "tex2D"));
        }
    }
}
