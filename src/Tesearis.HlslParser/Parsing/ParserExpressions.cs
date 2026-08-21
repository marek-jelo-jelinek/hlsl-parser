using System.Collections.Generic;
using Tesearis.HlslParser.Diagnostics;
using Tesearis.HlslParser.Lexing;
using Tesearis.HlslParser.Syntax;
using Tesearis.HlslParser.Text;

namespace Tesearis.HlslParser.Parsing
{
    public partial class Parser
    {
        private HlslNode ParseExpression()
        {
            return ParseAssignment();
        }

        private static bool IsAssignmentOperator(HlslTokenKind kind)
        {
            return kind is HlslTokenKind.Equals or HlslTokenKind.PlusEquals or HlslTokenKind.MinusEquals or HlslTokenKind.StarEquals
                or HlslTokenKind.SlashEquals or HlslTokenKind.PercentEquals or HlslTokenKind.AmpersandEquals or HlslTokenKind.PipeEquals
                or HlslTokenKind.CaretEquals or HlslTokenKind.LessThanLessThanEquals or HlslTokenKind.GreaterThanGreaterThanEquals;
        }

        private HlslNode ParseAssignment()
        {
            var start = Current.Span.Start;
            var target = ParseConditional();
            if (!IsAssignmentOperator(Current.Kind)) return target;

            var op = Advance();
            var value = ParseAssignment(); // right-associative: a = b = c
            return new AssignmentExpressionNode(SpanFrom(start), target, op.Kind, value);
        }

        private HlslNode ParseConditional()
        {
            var start = Current.Span.Start;
            var condition = ParseLogicalOr();
            if (!Match(HlslTokenKind.Question)) return condition;

            var whenTrue = ParseExpression(); // full expression, matching C's ?: grammar
            Expect(HlslTokenKind.Colon, "':'");
            var whenFalse = ParseConditional(); // right-associative: nested ternaries chain on the else-branch
            return new ConditionalExpressionNode(SpanFrom(start), condition, whenTrue, whenFalse);
        }

        private HlslNode ParseLogicalOr()
        {
            var start = Current.Span.Start;
            var left = ParseLogicalAnd();
            while (Current.Kind == HlslTokenKind.PipePipe)
            {
                var op = Advance();
                var right = ParseLogicalAnd();
                left = new BinaryExpressionNode(SpanFrom(start), left, op.Kind, right);
            }

            return left;
        }

        private HlslNode ParseLogicalAnd()
        {
            var start = Current.Span.Start;
            var left = ParseBitwiseOr();
            while (Current.Kind == HlslTokenKind.AmpersandAmpersand)
            {
                var op = Advance();
                var right = ParseBitwiseOr();
                left = new BinaryExpressionNode(SpanFrom(start), left, op.Kind, right);
            }

            return left;
        }

        private HlslNode ParseBitwiseOr()
        {
            var start = Current.Span.Start;
            var left = ParseBitwiseXor();
            while (Current.Kind == HlslTokenKind.Pipe)
            {
                var op = Advance();
                var right = ParseBitwiseXor();
                left = new BinaryExpressionNode(SpanFrom(start), left, op.Kind, right);
            }

            return left;
        }

        private HlslNode ParseBitwiseXor()
        {
            var start = Current.Span.Start;
            var left = ParseBitwiseAnd();
            while (Current.Kind == HlslTokenKind.Caret)
            {
                var op = Advance();
                var right = ParseBitwiseAnd();
                left = new BinaryExpressionNode(SpanFrom(start), left, op.Kind, right);
            }

            return left;
        }

        private HlslNode ParseBitwiseAnd()
        {
            var start = Current.Span.Start;
            var left = ParseEquality();
            while (Current.Kind == HlslTokenKind.Ampersand)
            {
                var op = Advance();
                var right = ParseEquality();
                left = new BinaryExpressionNode(SpanFrom(start), left, op.Kind, right);
            }

            return left;
        }

        private HlslNode ParseEquality()
        {
            var start = Current.Span.Start;
            var left = ParseRelational();
            while (Current.Kind == HlslTokenKind.EqualsEquals || Current.Kind == HlslTokenKind.ExclamationEquals)
            {
                var op = Advance();
                var right = ParseRelational();
                left = new BinaryExpressionNode(SpanFrom(start), left, op.Kind, right);
            }

            return left;
        }

        private HlslNode ParseRelational()
        {
            var start = Current.Span.Start;
            var left = ParseShift();
            while (Current.Kind is HlslTokenKind.LessThan or HlslTokenKind.GreaterThan or HlslTokenKind.LessThanEquals
                   or HlslTokenKind.GreaterThanEquals)
            {
                var op = Advance();
                var right = ParseShift();
                left = new BinaryExpressionNode(SpanFrom(start), left, op.Kind, right);
            }

            return left;
        }

        private HlslNode ParseShift()
        {
            var start = Current.Span.Start;
            var left = ParseAdditive();
            while (Current.Kind is HlslTokenKind.LessThanLessThan or HlslTokenKind.GreaterThanGreaterThan)
            {
                var op = Advance();
                var right = ParseAdditive();
                left = new BinaryExpressionNode(SpanFrom(start), left, op.Kind, right);
            }

            return left;
        }

        private HlslNode ParseAdditive()
        {
            var start = Current.Span.Start;
            var left = ParseMultiplicative();
            while (Current.Kind is HlslTokenKind.Plus or HlslTokenKind.Minus)
            {
                var op = Advance();
                var right = ParseMultiplicative();
                left = new BinaryExpressionNode(SpanFrom(start), left, op.Kind, right);
            }

            return left;
        }

        private HlslNode ParseMultiplicative()
        {
            var start = Current.Span.Start;
            var left = ParseUnary();
            while (Current.Kind is HlslTokenKind.Star or HlslTokenKind.Slash or HlslTokenKind.Percent)
            {
                var op = Advance();
                var right = ParseUnary();
                left = new BinaryExpressionNode(SpanFrom(start), left, op.Kind, right);
            }

            return left;
        }

        private static bool IsUnaryPrefixOperator(HlslTokenKind kind)
        {
            return kind is HlslTokenKind.Exclamation or HlslTokenKind.Tilde or HlslTokenKind.Minus or HlslTokenKind.Plus or HlslTokenKind.PlusPlus
                or HlslTokenKind.MinusMinus;
        }

        private HlslNode ParseUnary()
        {
            var start = Current.Span.Start;
            if (!IsUnaryPrefixOperator(Current.Kind)) return ParsePostfix();

            var op = Advance();
            var operand = ParseUnary(); // right-associative: !!x, --x, -(-x)
            return new UnaryExpressionNode(SpanFrom(start), op.Kind, operand, isPostfix: false);
        }

        private HlslNode ParsePostfix()
        {
            var start = Current.Span.Start;
            var expression = ParsePrimary();

            while (true)
            {
                if (Current.Kind == HlslTokenKind.OpenParen)
                {
                    expression = ParseInvocation(start, expression);
                }
                else if (Current.Kind == HlslTokenKind.OpenBracket)
                {
                    expression = ParseElementAccess(start, expression);
                }
                else if (Current.Kind == HlslTokenKind.Dot)
                {
                    Advance();
                    var nameToken = Expect(HlslTokenKind.Identifier, "member name");
                    expression = new MemberAccessExpressionNode(SpanFrom(start), expression, nameToken.Text);
                }
                else if (Current.Kind is HlslTokenKind.PlusPlus or HlslTokenKind.MinusMinus)
                {
                    var op = Advance();
                    expression = new UnaryExpressionNode(SpanFrom(start), op.Kind, expression, isPostfix: true);
                }
                else
                {
                    break;
                }
            }

            return expression;
        }

        private HlslNode ParseInvocation(int start, HlslNode callee)
        {
            Advance();
            var arguments = new List<HlslNode>();
            if (Current.Kind != HlslTokenKind.CloseParen)
            {
                while (true)
                {
                    var before = _index;
                    arguments.Add(ParseAssignment()); // args exclude the comma operator (HLSL has none anyway)
                    if (!Match(HlslTokenKind.Comma)) break;
                    if (_index == before) Advance();
                }
            }

            Expect(HlslTokenKind.CloseParen, "')'");
            return new InvocationExpressionNode(SpanFrom(start), callee, arguments);
        }

        private HlslNode ParseElementAccess(int start, HlslNode target)
        {
            Advance();
            var index = ParseExpression();
            Expect(HlslTokenKind.CloseBracket, "']'");
            return new ElementAccessExpressionNode(SpanFrom(start), target, index);
        }

        /// <summary>Whether <paramref name="token"/> could start a primary/unary expression —
        /// used both by the statement dispatcher (to tell an expression statement from garbage)
        /// and by cast-vs-parenthesized-expression disambiguation.</summary>
        private static bool CanStartExpression(Token token)
        {
            if (token.Kind is HlslTokenKind.Identifier or HlslTokenKind.IntegerLiteral or HlslTokenKind.FloatLiteral or HlslTokenKind.StringLiteral
                or HlslTokenKind.OpenParen)
            {
                return true;
            }

            if (IsUnaryPrefixOperator(token.Kind)) return true;
            return token.Kind == HlslTokenKind.Keyword && HlslKeywords.IsTypeKeyword(token.Text);
        }

        private HlslNode ParsePrimary()
        {
            var start = Current.Span.Start;

            if (Current.Kind is HlslTokenKind.IntegerLiteral or HlslTokenKind.FloatLiteral or HlslTokenKind.StringLiteral)
            {
                var token = Advance();
                return new LiteralExpressionNode(token.Span, token.Kind, token.Text, token.IntegerValue, token.FloatValue,
                    token.NumericSuffix, token.IsHex);
            }

            if (Current.Kind == HlslTokenKind.Identifier)
            {
                if (Current.Text is "true" or "false")
                {
                    var token = Advance();
                    return new LiteralExpressionNode(token.Span, token.Kind, token.Text, 0, 0, NumericLiteralSuffix.None, false);
                }

                var identifier = Advance();
                return new IdentifierExpressionNode(identifier.Span, identifier.Text);
            }

            // A built-in type keyword used bare is only ever a constructor-call callee, e.g. the
            // `float4` in `float4(1, 2, 3, 4)` — model it the same as any other name reference.
            if (Current.Kind == HlslTokenKind.Keyword && HlslKeywords.IsTypeKeyword(Current.Text))
            {
                var typeToken = Advance();
                return new IdentifierExpressionNode(typeToken.Span, typeToken.Text);
            }

            if (Current.Kind == HlslTokenKind.OpenParen) return ParseParenthesizedOrCast(start);

            Diagnostics.Error(DiagnosticIds.ExpectedExpression, Current.Span, "Expected an expression but found '" + Current.Text + "'.");

            // Don't eat a boundary token the caller needs to see (')', ']', '}', ';', ',', ':', EOF);
            // otherwise consume exactly one poison token so callers always make forward progress.
            if (IsExpressionBoundary(Current)) return new ErrorNode(new TextSpan(Current.Span.Start, 0), "Missing expression.");
            var poison = Advance();
            return new ErrorNode(TextSpan.FromBounds(start, poison.Span.End), "Unexpected token in expression position.");
        }

        private static bool IsExpressionBoundary(Token token)
        {
            return token.Kind is HlslTokenKind.CloseParen or HlslTokenKind.CloseBracket or HlslTokenKind.CloseBrace or HlslTokenKind.Semicolon
                or HlslTokenKind.Comma or HlslTokenKind.Colon or HlslTokenKind.EndOfFile;
        }

        /// <summary>
        /// Resolves the classic C parenthesized-expression-vs-cast ambiguity.
        /// Recognized for built-in type keywords, optional modifiers.
        /// </summary>
        private bool LooksLikeCast()
        {
            var offset = 0;
            while (Peek(offset).Kind == HlslTokenKind.Keyword && HlslKeywords.IsModifierKeyword(Peek(offset).Text))
            {
                offset++;
            }

            var typeToken = Peek(offset);
            if (typeToken.Kind != HlslTokenKind.Keyword || !HlslKeywords.IsTypeKeyword(typeToken.Text))
            {
                return false;
            }

            offset++;

            if ((typeToken.Text is "vector" or "matrix" || HlslKeywords.IsResourceKeyword(typeToken.Text)) &&
                Peek(offset).Kind == HlslTokenKind.LessThan)
            {
                var depth = 0;
                do
                {
                    if (Peek(offset).Kind == HlslTokenKind.LessThan) depth++;
                    else if (Peek(offset).Kind == HlslTokenKind.GreaterThan) depth--;
                    else if (Peek(offset).Kind == HlslTokenKind.GreaterThanGreaterThan) depth -= 2;
                    offset++;
                } while (depth > 0 && Peek(offset).Kind != HlslTokenKind.EndOfFile && Peek(offset).Kind != HlslTokenKind.Semicolon);
            }

            return Peek(offset).Kind == HlslTokenKind.CloseParen && CanStartExpression(Peek(offset + 1));
        }

        private HlslNode ParseParenthesizedOrCast(int start)
        {
            Advance();

            if (LooksLikeCast())
            {
                var modifiers = ParseModifierList();
                var type = ParseTypeName();
                Expect(HlslTokenKind.CloseParen, "')'");
                var operand = ParseUnary();
                return new CastExpressionNode(SpanFrom(start), modifiers, type, operand);
            }

            var inner = ParseExpression();
            Expect(HlslTokenKind.CloseParen, "')'");
            return new ParenthesizedExpressionNode(SpanFrom(start), inner);
        }
    }
}