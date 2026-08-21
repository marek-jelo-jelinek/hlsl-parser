using System;
using System.Collections.Generic;
using System.Globalization;
using Tesearis.HlslParser.Diagnostics;
using Tesearis.HlslParser.Text;

namespace Tesearis.HlslParser.Lexing
{
    /// <summary>
    /// Turns HLSL/Cg source text into a flat token stream.
    /// </summary>
    public sealed class Lexer
    {
        private readonly SourceText _source;
        private readonly string _text;
        private readonly DiagnosticSink _diagnostics;
        private int _position; // LOCAL cursor into _text
        private bool _isFirstToken = true;

        public Lexer(SourceText source, DiagnosticSink diagnostics)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _text = source.Text;
            _diagnostics = diagnostics ?? new DiagnosticSink(source);
        }

        public DiagnosticSink Diagnostics => _diagnostics;

        public List<Token> Tokenize()
        {
            var tokens = new List<Token>(Math.Max(16, _text.Length / 6));
            Token token;
            do
            {
                token = NextToken();
                tokens.Add(token);
            } while (token.Kind != HlslTokenKind.EndOfFile);

            return tokens;
        }
        
        private Token NextToken()
        {
            var atLineStart = SkipTrivia() || _isFirstToken;
            _isFirstToken = false;

            var start = _position;
            Token token;
            if (_position >= _text.Length)
            {
                token = Make(HlslTokenKind.EndOfFile, start, start);
            }
            else
            {
                var c = Peek();
                if (IsIdentifierStart(c)) token = ReadIdentifierOrKeyword();
                else if (char.IsDigit(c) || (c == '.' && char.IsDigit(Peek(1)))) token = ReadNumericLiteral();
                else if (c == '"') token = ReadStringLiteral();
                else token = ReadPunctuation();
            }

            token.IsAtStartOfLine = atLineStart;
            return token;
        }
        
        /// <summary>
        /// Consumes whitespace, comments and backslash-newline line-continuations. Returns true
        /// iff a genuine, un-spliced physical line break was consumed (used by
        /// <see cref="NextToken"/> to stamp <see cref="Token.IsAtStartOfLine"/> — a splice joins
        /// two physical lines into one logical line, so it must not count as a line break).
        /// </summary>
        private bool SkipTrivia()
        {
            var sawNewline = false;

            while (true)
            {
                var c = Peek();

                // Backslash-newline line splicing (C/HLSL "translation phase 2"): consumed as
                // trivia everywhere, not just inside preprocessor directives, and deliberately
                // does NOT count as a line break — the spliced line is one logical line. Token
                // spans are untouched (the lexer indexes _text in place), so no offset-map
                // bookkeeping is needed here, unlike a scanner that copies/rewrites text.
                if (c == '\\')
                {
                    var breakLength = LineBreakLength(Peek(1), Peek(2));
                    if (breakLength > 0)
                    {
                        _position += 1 + breakLength;
                        continue;
                    }
                }

                if (c == '\n' || c == '\r')
                {
                    _position += LineBreakLength(c, Peek(1));
                    sawNewline = true;
                    continue;
                }

                if (char.IsWhiteSpace(c))
                {
                    _position++;
                    continue;
                }

                if (c == '/' && Peek(1) == '/')
                {
                    _position += 2;
                    while (_position < _text.Length && _text[_position] != '\n' && _text[_position] != '\r') _position++;
                    continue;
                }

                if (c == '/' && Peek(1) == '*')
                {
                    var start = _position;
                    _position += 2;
                    var closed = false;
                    while (_position < _text.Length)
                    {
                        if (_text[_position] == '*' && Peek(1) == '/')
                        {
                            _position += 2;
                            closed = true;
                            break;
                        }

                        if (_text[_position] == '\n' || _text[_position] == '\r') sawNewline = true;
                        _position++;
                    }

                    if (!closed)
                    {
                        _diagnostics.Warning(DiagnosticIds.UnterminatedBlockComment, MakeSpan(start, _position), "Unterminated block comment.");
                    }

                    continue;
                }

                return sawNewline;
            }
        }

        /// <summary>Length of the line-break sequence starting at <paramref name="first"/>
        /// (0 if <paramref name="first"/> isn't a line-break character); treats <c>\r\n</c> as
        /// one two-character break.</summary>
        private static int LineBreakLength(char first, char second)
        {
            if (first == '\r') return second == '\n' ? 2 : 1;
            if (first == '\n') return 1;
            return 0;
        }
        
        private Token ReadIdentifierOrKeyword()
        {
            var start = _position;
            _position++;
            while (_position < _text.Length && IsIdentifierPart(_text[_position])) _position++;
            var text = _text.Substring(start, _position - start);

            Token token;
            if (HlslKeywords.TryGetCanonical(text, out var canonical, out _))
            {
                token = Make(HlslTokenKind.Keyword, start, _position);
                token.CachedText = canonical;
            }
            else
            {
                token = Make(HlslTokenKind.Identifier, start, _position);
                token.CachedText = text;
            }

            return token;
        }
        
        private Token ReadNumericLiteral()
        {
            var start = _position;

            if (Peek() == '0' && (Peek(1) == 'x' || Peek(1) == 'X')) return ReadHexIntegerLiteral(start);

            var isFloat = false;

            while (_position < _text.Length && char.IsDigit(_text[_position])) _position++;

            if (_position < _text.Length && _text[_position] == '.')
            {
                isFloat = true;
                _position++;
                while (_position < _text.Length && char.IsDigit(_text[_position])) _position++;
            }

            if (_position < _text.Length && (_text[_position] == 'e' || _text[_position] == 'E'))
            {
                var lookahead = _position + 1;
                if (lookahead < _text.Length && (_text[lookahead] == '+' || _text[lookahead] == '-')) lookahead++;
                if (lookahead < _text.Length && char.IsDigit(_text[lookahead]))
                {
                    isFloat = true;
                    _position = lookahead;
                    while (_position < _text.Length && char.IsDigit(_text[_position])) _position++;
                }
            }

            var digitsText = _text.Substring(start, _position - start);

            if (isFloat)
            {
                if (!double.TryParse(digitsText, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
                {
                    _diagnostics.Error(DiagnosticIds.InvalidNumericLiteral, MakeSpan(start, _position), "Malformed floating-point literal.");
                    floatValue = 0;
                }

                var suffix = ReadFloatSuffix();
                var token = Make(HlslTokenKind.FloatLiteral, start, _position);
                token.FloatValue = floatValue;
                token.NumericSuffix = suffix;
                return token;
            }
            else
            {
                if (!ulong.TryParse(digitsText, NumberStyles.None, CultureInfo.InvariantCulture, out var integerValue))
                {
                    _diagnostics.Error(DiagnosticIds.InvalidNumericLiteral, MakeSpan(start, _position), "Malformed integer literal.");
                    integerValue = 0;
                }

                var suffix = ReadIntegerSuffix();
                var token = Make(HlslTokenKind.IntegerLiteral, start, _position);
                token.IntegerValue = integerValue;
                token.NumericSuffix = suffix;
                return token;
            }
        }

        private Token ReadHexIntegerLiteral(int start)
        {
            _position += 2;
            var digitsStart = _position;

            // Hex-digit scanning is greedy and happens before suffix scanning: the 'F' in
            // "0x1F" is a hex digit, not a float suffix — hex integers have no float suffix form.
            while (_position < _text.Length && IsHexDigit(_text[_position])) _position++;

            var hexDigits = _text.Substring(digitsStart, _position - digitsStart);
            ulong integerValue = 0;
            if (hexDigits.Length == 0)
            {
                _diagnostics.Error(DiagnosticIds.InvalidNumericLiteral, MakeSpan(start, _position), "Hexadecimal literal has no digits.");
            }
            else
            {
                try
                {
                    integerValue = Convert.ToUInt64(hexDigits, 16);
                }
                catch (Exception)
                {
                    _diagnostics.Error(DiagnosticIds.InvalidNumericLiteral, MakeSpan(start, _position), "Malformed hexadecimal literal.");
                }
            }

            var suffix = ReadIntegerSuffix();
            var token = Make(HlslTokenKind.IntegerLiteral, start, _position);
            token.IsHex = true;
            token.IntegerValue = integerValue;
            token.NumericSuffix = suffix;
            return token;
        }

        private NumericLiteralSuffix ReadIntegerSuffix()
        {
            var c = Peek();
            if (c is 'u' or 'U')
            {
                _position++;
                return NumericLiteralSuffix.Unsigned;
            }

            if (c is 'l' or 'L')
            {
                _position++;
                return NumericLiteralSuffix.Long;
            }

            return NumericLiteralSuffix.None;
        }

        private NumericLiteralSuffix ReadFloatSuffix()
        {
            var c = Peek();
            if (c is 'f' or 'F')
            {
                _position++;
                return NumericLiteralSuffix.Float;
            }

            if (c is 'h' or 'H')
            {
                _position++;
                return NumericLiteralSuffix.Half;
            }

            return NumericLiteralSuffix.None;
        }
        
        private Token ReadStringLiteral()
        {
            var start = _position;
            _position++; // opening quote
            var valueStart = _position;
            var terminated = false;

            while (_position < _text.Length)
            {
                var c = _text[_position];
                if (c == '"')
                {
                    terminated = true;
                    break;
                }

                if (c is '\n' or '\r') break;

                if (c == '\\' && _position + 1 < _text.Length && _text[_position + 1] != '\n' && _text[_position + 1] != '\r')
                {
                    _position += 2;
                    continue;
                }

                _position++;
            }

            var valueEnd = _position;
            if (terminated) _position++; // closing quote

            var token = Make(HlslTokenKind.StringLiteral, start, _position);
            token.ValueSpan = MakeSpan(valueStart, valueEnd);

            if (!terminated)
            {
                _diagnostics.Error(DiagnosticIds.UnterminatedString, token.Span, "Unterminated string literal.");
            }

            return token;
        }
        
        private Token ReadPunctuation()
        {
            var start = _position;
            var c = Peek();

            switch (c)
            {
                case '{':
                    _position++;
                    return Make(HlslTokenKind.OpenBrace, start, _position);
                case '}':
                    _position++;
                    return Make(HlslTokenKind.CloseBrace, start, _position);
                case '(':
                    _position++;
                    return Make(HlslTokenKind.OpenParen, start, _position);
                case ')':
                    _position++;
                    return Make(HlslTokenKind.CloseParen, start, _position);
                case '[':
                    _position++;
                    return Make(HlslTokenKind.OpenBracket, start, _position);
                case ']':
                    _position++;
                    return Make(HlslTokenKind.CloseBracket, start, _position);
                case ';':
                    _position++;
                    return Make(HlslTokenKind.Semicolon, start, _position);
                case ',':
                    _position++;
                    return Make(HlslTokenKind.Comma, start, _position);
                case '.':
                    _position++;
                    return Make(HlslTokenKind.Dot, start, _position);
                case '?':
                    _position++;
                    return Make(HlslTokenKind.Question, start, _position);
                case ':':
                    _position++;
                    return Make(HlslTokenKind.Colon, start, _position);
                case '~':
                    _position++;
                    return Make(HlslTokenKind.Tilde, start, _position);

                case '=':
                    if (Peek(1) == '=')
                    {
                        _position += 2;
                        return Make(HlslTokenKind.EqualsEquals, start, _position);
                    }

                    _position++;
                    return Make(HlslTokenKind.Equals, start, _position);

                case '!':
                    if (Peek(1) == '=')
                    {
                        _position += 2;
                        return Make(HlslTokenKind.ExclamationEquals, start, _position);
                    }

                    _position++;
                    return Make(HlslTokenKind.Exclamation, start, _position);

                case '+':
                    if (Peek(1) == '+')
                    {
                        _position += 2;
                        return Make(HlslTokenKind.PlusPlus, start, _position);
                    }

                    if (Peek(1) == '=')
                    {
                        _position += 2;
                        return Make(HlslTokenKind.PlusEquals, start, _position);
                    }

                    _position++;
                    return Make(HlslTokenKind.Plus, start, _position);

                case '-':
                    if (Peek(1) == '-')
                    {
                        _position += 2;
                        return Make(HlslTokenKind.MinusMinus, start, _position);
                    }

                    if (Peek(1) == '=')
                    {
                        _position += 2;
                        return Make(HlslTokenKind.MinusEquals, start, _position);
                    }

                    // '-' is always its own token here, never folded into a following numeric
                    // literal — HLSL unary/binary expression grammar needs '-' as a real
                    // operator token.
                    _position++;
                    return Make(HlslTokenKind.Minus, start, _position);

                case '*':
                    if (Peek(1) == '=')
                    {
                        _position += 2;
                        return Make(HlslTokenKind.StarEquals, start, _position);
                    }

                    _position++;
                    return Make(HlslTokenKind.Star, start, _position);

                case '/':
                    if (Peek(1) == '=')
                    {
                        _position += 2;
                        return Make(HlslTokenKind.SlashEquals, start, _position);
                    }

                    _position++;
                    return Make(HlslTokenKind.Slash, start, _position);

                case '%':
                    if (Peek(1) == '=')
                    {
                        _position += 2;
                        return Make(HlslTokenKind.PercentEquals, start, _position);
                    }

                    _position++;
                    return Make(HlslTokenKind.Percent, start, _position);

                case '&':
                    if (Peek(1) == '&')
                    {
                        _position += 2;
                        return Make(HlslTokenKind.AmpersandAmpersand, start, _position);
                    }

                    if (Peek(1) == '=')
                    {
                        _position += 2;
                        return Make(HlslTokenKind.AmpersandEquals, start, _position);
                    }

                    _position++;
                    return Make(HlslTokenKind.Ampersand, start, _position);

                case '|':
                    if (Peek(1) == '|')
                    {
                        _position += 2;
                        return Make(HlslTokenKind.PipePipe, start, _position);
                    }

                    if (Peek(1) == '=')
                    {
                        _position += 2;
                        return Make(HlslTokenKind.PipeEquals, start, _position);
                    }

                    _position++;
                    return Make(HlslTokenKind.Pipe, start, _position);

                case '^':
                    if (Peek(1) == '=')
                    {
                        _position += 2;
                        return Make(HlslTokenKind.CaretEquals, start, _position);
                    }

                    _position++;
                    return Make(HlslTokenKind.Caret, start, _position);

                case '<':
                    if (Peek(1) == '<' && Peek(2) == '=')
                    {
                        _position += 3;
                        return Make(HlslTokenKind.LessThanLessThanEquals, start, _position);
                    }

                    if (Peek(1) == '<')
                    {
                        _position += 2;
                        return Make(HlslTokenKind.LessThanLessThan, start, _position);
                    }

                    if (Peek(1) == '=')
                    {
                        _position += 2;
                        return Make(HlslTokenKind.LessThanEquals, start, _position);
                    }

                    _position++;
                    return Make(HlslTokenKind.LessThan, start, _position);

                case '>':
                    if (Peek(1) == '>' && Peek(2) == '=')
                    {
                        _position += 3;
                        return Make(HlslTokenKind.GreaterThanGreaterThanEquals, start, _position);
                    }

                    if (Peek(1) == '>')
                    {
                        _position += 2;
                        return Make(HlslTokenKind.GreaterThanGreaterThan, start, _position);
                    }

                    if (Peek(1) == '=')
                    {
                        _position += 2;
                        return Make(HlslTokenKind.GreaterThanEquals, start, _position);
                    }

                    _position++;
                    return Make(HlslTokenKind.GreaterThan, start, _position);

                case '#':
                    if (Peek(1) == '#')
                    {
                        _position += 2;
                        return Make(HlslTokenKind.HashHash, start, _position);
                    }

                    _position++;
                    return Make(HlslTokenKind.Hash, start, _position);

                default:
                {
                    _position++;
                    var token = Make(HlslTokenKind.Unknown, start, _position);
                    _diagnostics.Error(DiagnosticIds.UnrecognizedCharacter, token.Span,
                        "Unrecognized character '" + c + "'.");
                    return token;
                }
            }
        }
        
        private char Peek(int offset = 0)
        {
            var index = _position + offset;
            return index >= 0 && index < _text.Length ? _text[index] : '\0';
        }

        private Token Make(HlslTokenKind kind, int localStart, int localEnd)
        {
            var span = MakeSpan(localStart, localEnd);
            return new Token
            {
                Kind = kind,
                Span = span,
                ValueSpan = span,
                Source = _source
            };
        }

        private TextSpan MakeSpan(int localStart, int localEnd)
        {
            return new TextSpan(_source.BaseOffset + localStart, localEnd - localStart);
        }

        private static bool IsIdentifierStart(char c)
        {
            return char.IsLetter(c) || c == '_';
        }

        private static bool IsIdentifierPart(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        private static bool IsHexDigit(char c)
        {
            return c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F';
        }
    }
}