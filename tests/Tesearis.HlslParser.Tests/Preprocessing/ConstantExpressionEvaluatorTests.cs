using System.Linq;
using Tesearis.HlslParser.Diagnostics;
using Tesearis.HlslParser.Lexing;
using Tesearis.HlslParser.Preprocessing;
using Tesearis.HlslParser.Text;
using NUnit.Framework;

namespace Tesearis.HlslParser.Tests.Preprocessing
{
    /// <summary>Covers the <c>#if</c>/<c>#elif</c> constant-expression evaluator — exercised
    /// through the public <see cref="Preprocessor"/> surface by checking which branch of an
    /// <c>#if ... #else ... #endif</c> survives (the evaluator itself is an internal type).</summary>
    [TestFixture]
    public class ConstantExpressionEvaluatorTests
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

        private static bool ConditionIsTrue(string expression)
        {
            var tokens = Preprocess("#if " + expression + "\nLIVE\n#else\nDEAD\n#endif");
            var live = HasIdentifier(tokens, "LIVE");
            var dead = HasIdentifier(tokens, "DEAD");
            Assert.AreNotEqual(live, dead, "exactly one branch should survive");
            return live;
        }
        
        [TestCase("2 + 3 * 4 == 14", true)]
        [TestCase("(2 + 3) * 4 == 20", true)]
        [TestCase("1 << 4 == 16", true)]
        [TestCase("16 >> 2 == 4", true)]
        [TestCase("(1 || 0) && 1", true)]
        [TestCase("1 && 0", false)]
        [TestCase("5 & 3", true)] // 5 & 3 == 1, nonzero
        [TestCase("5 & 2", false)] // 5 & 2 == 0
        [TestCase("6 | 1 == 7", true)] // '==' binds tighter than '|': 6 | (1==7) == 6 | 0 == 6, nonzero
        [TestCase("5 ^ 5", false)]
        [TestCase("~0 == -1", true)]
        [TestCase("!0", true)]
        [TestCase("!1", false)]
        [TestCase("10 % 3 == 1", true)]
        [TestCase("2 < 3 && 3 <= 3 && 4 > 3 && 4 >= 4", true)]
        [TestCase("1 != 2", true)]
        public void EvaluatesWithCorrectPrecedenceAndAssociativity(string expression, bool expected)
        {
            Assert.AreEqual(expected, ConditionIsTrue(expression));
        }
        
        [Test]
        public void DefinedWithParensDetectsADefinedMacro()
        {
            var tokens = Preprocess("#define X 1\n#if defined(X)\nLIVE\n#endif");
            Assert.IsTrue(HasIdentifier(tokens, "LIVE"));
        }

        [Test]
        public void DefinedWithoutParensDetectsADefinedMacro()
        {
            var tokens = Preprocess("#define X 1\n#if defined X\nLIVE\n#endif");
            Assert.IsTrue(HasIdentifier(tokens, "LIVE"));
        }

        [Test]
        public void DefinedIsFalseForAnUndefinedMacro()
        {
            var tokens = Preprocess("#if defined(NEVER_DEFINED)\nLIVE\n#else\nDEAD\n#endif");
            Assert.IsTrue(HasIdentifier(tokens, "DEAD"));
        }

        [Test]
        public void UndefinedBareIdentifierEvaluatesToZero()
        {
            var tokens = Preprocess("#if UNDEFINED_THING\nLIVE\n#else\nDEAD\n#endif");
            Assert.IsTrue(HasIdentifier(tokens, "DEAD"));
        }
        
        [Test]
        public void FloatLiteralInConditionReportsMalformedConstantExpression()
        {
            DiagnosticSink sink = null;
            Assert.DoesNotThrow(() => PreprocessWithDiagnostics("#if 1.5\nX\n#endif", out sink));

            Assert.IsTrue(sink.Diagnostics.Any(d => d.Id == DiagnosticIds.MalformedConstantExpression));
        }

        [Test]
        public void TernaryInConditionReportsMalformedConstantExpression()
        {
            DiagnosticSink sink = null;
            Assert.DoesNotThrow(() => PreprocessWithDiagnostics("#if 1 ? 2 : 3\nX\n#endif", out sink));

            Assert.IsTrue(sink.Diagnostics.Any(d => d.Id == DiagnosticIds.MalformedConstantExpression));
        }

        [Test]
        public void StrayTokenInConditionReportsMalformedConstantExpressionOnlyOnce()
        {
            DiagnosticSink sink = null;
            Assert.DoesNotThrow(() => PreprocessWithDiagnostics("#if ) ) )\nX\n#endif", out sink));

            Assert.AreEqual(1, sink.Diagnostics.Count(d => d.Id == DiagnosticIds.MalformedConstantExpression));
        }
        
        [Test]
        public void DivisionByZeroReportsErrorAndFoldsToZero()
        {
            DiagnosticSink sink = null;
            Token[] tokens = null;
            Assert.DoesNotThrow(() => tokens = PreprocessWithDiagnostics("#if 1 / 0\nLIVE\n#else\nDEAD\n#endif", out sink));

            Assert.IsTrue(sink.Diagnostics.Any(d => d.Id == DiagnosticIds.DivisionByZeroInConstantExpression));
            Assert.IsTrue(HasIdentifier(tokens, "DEAD")); // 1/0 folds to 0 => condition is false
        }

        [Test]
        public void ModuloByZeroReportsError()
        {
            DiagnosticSink sink = null;
            Assert.DoesNotThrow(() => PreprocessWithDiagnostics("#if 1 % 0\nX\n#endif", out sink));

            Assert.IsTrue(sink.Diagnostics.Any(d => d.Id == DiagnosticIds.DivisionByZeroInConstantExpression));
        }
    }
}
