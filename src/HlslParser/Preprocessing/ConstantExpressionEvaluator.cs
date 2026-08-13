using System.Collections.Generic;
using HlslParser.Diagnostics;
using HlslParser.Lexing;
using HlslParser.Text;

namespace HlslParser.Preprocessing
{
    /// <summary>
    /// Evaluates a <c>#if</c>/<c>#elif</c> constant expression: <c>defined()</c> reduction,
    /// macro expansion of what remains, then a recursive-descent, one-method-per-precedence-level
    /// integer evaluator — the same plain recursive-descent style planned for the future
    /// Phase-4 statement/expression parser.
    /// </summary>
    internal sealed class ConstantExpressionEvaluator
    {
        private readonly SourceText _source;
        private readonly MacroTable _macros;
        private readonly MacroExpander _expander;
        private readonly DiagnosticSink _diagnostics;

        private IReadOnlyList<Token> _tokens;
        private int _index;
        private TextSpan _conditionSpan;
        private bool _reportedMalformed;

        public ConstantExpressionEvaluator(SourceText source, MacroTable macros, MacroExpander expander, DiagnosticSink diagnostics)
        {
            _source = source;
            _macros = macros;
            _expander = expander;
            _diagnostics = diagnostics;
        }

        /// <summary>Evaluates the token run of a <c>#if</c>/<c>#elif</c> condition (not
        /// including the directive keyword itself) to a boolean. Only call this when the
        /// enclosing conditional context is already known to be live — evaluating a dead
        /// branch's condition would produce spurious diagnostics.</summary>
        public bool Evaluate(IReadOnlyList<Token> conditionTokens, TextSpan conditionSpan)
        {
            _conditionSpan = conditionSpan;
            _reportedMalformed = false;

            var reduced = ReduceDefined(conditionTokens);
            _tokens = _expander.ExpandLine(reduced);
            _index = 0;

            var value = ParseLogicalOr();

            if (Current.Kind != HlslTokenKind.EndOfFile) ReportMalformed(Current.Span, "Unexpected trailing token in #if/#elif expression.");

            return value != 0;
        }
        
        /// <summary>Replaces every <c>defined(NAME)</c>/<c>defined NAME</c> occurrence with a
        /// synthetic integer-literal token before ordinary macro expansion runs — macro-expanding
        /// the operand of <c>defined</c> is unsafe per the C standard and must not happen.</summary>
        private List<Token> ReduceDefined(IReadOnlyList<Token> tokens)
        {
            var result = new List<Token>(tokens.Count);
            var i = 0;
            while (i < tokens.Count)
            {
                var token = tokens[i];
                if (token.Kind == HlslTokenKind.Identifier && token.Text == "defined")
                {
                    if (i + 1 < tokens.Count && tokens[i + 1].Kind == HlslTokenKind.OpenParen)
                    {
                        if (i + 3 < tokens.Count && tokens[i + 2].Kind == HlslTokenKind.Identifier &&
                            tokens[i + 3].Kind == HlslTokenKind.CloseParen)
                        {
                            var name = tokens[i + 2].Text;
                            var span = TextSpan.FromBounds(token.Span.Start, tokens[i + 3].Span.End);
                            result.Add(MakeBoolToken(_macros.IsDefined(name), span));
                            i += 4;
                            continue;
                        }

                        _diagnostics.Error(DiagnosticIds.MalformedDefinedOperator, token.Span, "Malformed 'defined(...)' operator.");
                        result.Add(MakeBoolToken(false, token.Span));
                        i++;
                        continue;
                    }

                    if (i + 1 < tokens.Count && tokens[i + 1].Kind == HlslTokenKind.Identifier)
                    {
                        var name = tokens[i + 1].Text;
                        var span = TextSpan.FromBounds(token.Span.Start, tokens[i + 1].Span.End);
                        result.Add(MakeBoolToken(_macros.IsDefined(name), span));
                        i += 2;
                        continue;
                    }

                    _diagnostics.Error(DiagnosticIds.MalformedDefinedOperator, token.Span,
                        "'defined' must be followed by an identifier or '(identifier)'.");
                    result.Add(MakeBoolToken(false, token.Span));
                    i++;
                    continue;
                }

                result.Add(token);
                i++;
            }

            return result;
        }

        private Token MakeBoolToken(bool value, TextSpan span)
        {
            return new Token
            {
                Kind = HlslTokenKind.IntegerLiteral,
                Span = span,
                ValueSpan = span,
                IntegerValue = value ? 1ul : 0ul,
                Source = _source
            };
        }
        
        private Token Current => _index < _tokens.Count ? _tokens[_index] : EofSentinel;

        private Token EofSentinel => new()
        {
            Kind = HlslTokenKind.EndOfFile,
            Span = new TextSpan(_conditionSpan.End, 0),
            Source = _source
        };

        private Token Advance()
        {
            var token = Current;
            if (_index < _tokens.Count) _index++;
            return token;
        }

        private long ParseLogicalOr()
        {
            var left = ParseLogicalAnd();
            while (Current.Kind == HlslTokenKind.PipePipe)
            {
                Advance();
                var right = ParseLogicalAnd();
                left = (left != 0 || right != 0) ? 1 : 0;
            }

            return left;
        }

        private long ParseLogicalAnd()
        {
            var left = ParseBitwiseOr();
            while (Current.Kind == HlslTokenKind.AmpersandAmpersand)
            {
                Advance();
                var right = ParseBitwiseOr();
                left = (left != 0 && right != 0) ? 1 : 0;
            }

            return left;
        }

        private long ParseBitwiseOr()
        {
            var left = ParseBitwiseXor();
            while (Current.Kind == HlslTokenKind.Pipe)
            {
                Advance();
                left |= ParseBitwiseXor();
            }

            return left;
        }

        private long ParseBitwiseXor()
        {
            var left = ParseBitwiseAnd();
            while (Current.Kind == HlslTokenKind.Caret)
            {
                Advance();
                left ^= ParseBitwiseAnd();
            }

            return left;
        }

        private long ParseBitwiseAnd()
        {
            var left = ParseEquality();
            while (Current.Kind == HlslTokenKind.Ampersand)
            {
                Advance();
                left &= ParseEquality();
            }

            return left;
        }

        private long ParseEquality()
        {
            var left = ParseRelational();
            while (Current.Kind is HlslTokenKind.EqualsEquals or HlslTokenKind.ExclamationEquals)
            {
                var op = Advance().Kind;
                var right = ParseRelational();
                left = op == HlslTokenKind.EqualsEquals ? (left == right ? 1 : 0) : (left != right ? 1 : 0);
            }

            return left;
        }

        private long ParseRelational()
        {
            var left = ParseShift();
            while (Current.Kind is HlslTokenKind.LessThan or HlslTokenKind.LessThanEquals or HlslTokenKind.GreaterThan
                   or HlslTokenKind.GreaterThanEquals)
            {
                var op = Advance().Kind;
                var right = ParseShift();
                left = op switch
                {
                    HlslTokenKind.LessThan => left < right ? 1 : 0,
                    HlslTokenKind.LessThanEquals => left <= right ? 1 : 0,
                    HlslTokenKind.GreaterThan => left > right ? 1 : 0,
                    _ => left >= right ? 1 : 0
                };
            }

            return left;
        }

        private long ParseShift()
        {
            var left = ParseAdditive();
            while (Current.Kind is HlslTokenKind.LessThanLessThan or HlslTokenKind.GreaterThanGreaterThan)
            {
                var op = Advance().Kind;
                var right = ParseAdditive();
                left = op == HlslTokenKind.LessThanLessThan ? left << (int)right : left >> (int)right;
            }

            return left;
        }

        private long ParseAdditive()
        {
            var left = ParseMultiplicative();
            while (Current.Kind is HlslTokenKind.Plus or HlslTokenKind.Minus)
            {
                var op = Advance().Kind;
                var right = ParseMultiplicative();
                left = op == HlslTokenKind.Plus ? left + right : left - right;
            }

            return left;
        }

        private long ParseMultiplicative()
        {
            var left = ParseUnary();
            while (Current.Kind is HlslTokenKind.Star or HlslTokenKind.Slash or HlslTokenKind.Percent)
            {
                var opToken = Advance();
                var right = ParseUnary();

                if ((opToken.Kind == HlslTokenKind.Slash || opToken.Kind == HlslTokenKind.Percent) && right == 0)
                {
                    _diagnostics.Error(DiagnosticIds.DivisionByZeroInConstantExpression, opToken.Span,
                        "Division or modulo by zero in a #if/#elif constant expression.");
                    left = 0;
                    continue;
                }

                left = opToken.Kind switch
                {
                    HlslTokenKind.Star => left * right,
                    HlslTokenKind.Slash => left / right,
                    _ => left % right
                };
            }

            return left;
        }

        private long ParseUnary()
        {
            while (true)
            {
                if (Current.Kind == HlslTokenKind.Exclamation)
                {
                    Advance();
                    return ParseUnary() == 0 ? 1 : 0;
                }

                if (Current.Kind == HlslTokenKind.Tilde)
                {
                    Advance();
                    return ~ParseUnary();
                }

                if (Current.Kind == HlslTokenKind.Minus)
                {
                    Advance();
                    return -ParseUnary();
                }

                if (Current.Kind == HlslTokenKind.Plus)
                {
                    Advance();
                    continue;
                }

                return ParsePrimary();
            }
        }

        private long ParsePrimary()
        {
            var token = Current;

            if (token.Kind == HlslTokenKind.IntegerLiteral)
            {
                Advance();
                return unchecked((long)token.IntegerValue);
            }

            if (token.Kind == HlslTokenKind.OpenParen)
            {
                Advance();
                var value = ParseLogicalOr();
                if (Current.Kind == HlslTokenKind.CloseParen) Advance();
                else ReportMalformed(Current.Span, "Expected ')' in #if/#elif constant expression.");
                return value;
            }

            if (token.Kind == HlslTokenKind.Identifier)
            {
                // A bare identifier that isn't a defined macro evaluates to 0 — standard
                // C-preprocessor behavior (macros were already expanded before parsing reached
                // here, so any identifier still standing is genuinely undefined/unexpandable).
                Advance();
                return 0;
            }

            if (token.Kind == HlslTokenKind.EndOfFile)
            {
                ReportMalformed(token.Span, "Empty or incomplete #if/#elif constant expression.");
                return 0;
            }

            // Anything else — a float literal, ternary '?'/':' , a stray operator/punctuation —
            // is out of scope for this evaluator; report once, consume the token, fold to 0.
            ReportMalformed(token.Span, "Unsupported token in #if/#elif constant expression.");
            Advance();
            return 0;
        }

        private void ReportMalformed(TextSpan span, string message)
        {
            if (_reportedMalformed) return;
            _reportedMalformed = true;
            _diagnostics.Error(DiagnosticIds.MalformedConstantExpression, span, message);
        }
    }
}