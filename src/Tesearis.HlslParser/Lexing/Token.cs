using Tesearis.HlslParser.Text;

namespace Tesearis.HlslParser.Lexing
{
    public enum NumericLiteralSuffix
    {
        None,
        Unsigned, // u/U — IntegerLiteral only
        Long, // l/L — IntegerLiteral only (rare/legacy)
        Float, // f/F — FloatLiteral only
        Half // h/H — FloatLiteral only
    }

    /// <summary>
    /// A lexical token. Carries full source tracking (span plus 1-based line/column) so every
    /// downstream layer can produce clickable diagnostics.
    /// </summary>
    public struct Token
    {
        public HlslTokenKind Kind;

        /// <summary>Span of the entire lexeme (ABSOLUTE coordinates — see <see cref="SourceText.BaseOffset"/>).</summary>
        public TextSpan Span;

        /// <summary>
        /// Span of the semantic payload: string-literal contents without the surrounding quotes.
        /// Equals <see cref="Span"/> for every other token kind.
        /// </summary>
        public TextSpan ValueSpan;

        /// <summary>Set for numeric literals whose lexeme carries an explicit suffix.</summary>
        public NumericLiteralSuffix NumericSuffix;

        /// <summary>True when an <see cref="HlslTokenKind.IntegerLiteral"/> was written in hex (<c>0x...</c>).</summary>
        public bool IsHex;

        /// <summary>Set for <see cref="HlslTokenKind.IntegerLiteral"/> tokens. Always non-negative — a
        /// leading <c>-</c> is always its own <see cref="HlslTokenKind.Minus"/> token, never folded in.</summary>
        public ulong IntegerValue;

        /// <summary>Set for <see cref="HlslTokenKind.FloatLiteral"/> tokens.</summary>
        public double FloatValue;

        /// <summary>True iff this is the first token in the file, or at least one genuine
        /// (non-spliced) physical line break appeared in the trivia immediately preceding it.
        /// A backslash-newline splice does not count as a line break — it joins two physical
        /// lines into one logical line. Used by <c>Preprocessing/</c> to find directive lines
        /// (a <c>#</c> token starting a directive) without re-scanning raw text.</summary>
        public bool IsAtStartOfLine;

        /// <summary>True for a synthetic token produced by <see cref="Token.Missing"/> to stand in
        /// for an expected-but-absent token during parser error recovery. A missing token has a
        /// zero-length <see cref="Span"/> at the point of failure and empty <see cref="Text"/>.</summary>
        public bool IsMissing;

        private SourceText _source;
        private string _text;
        private string _value;
        private int _line;
        private int _column;

        /// <summary>Attaches the source text backing lazy Text/Value/Line/Column materialization. Set once by the lexer.</summary>
        internal SourceText Source
        {
            set => _source = value;
        }

        // The four Cached* setters below let a synthetic token (produced by Preprocessing/'s
        // '#'/'##' handling, or Token.Missing) pre-supply a field directly — needed because such a
        // token's Span doesn't map to one real source location, so the ordinary Source-backed
        // lookup would return the wrong value.

        /// <summary>Interned spelling for a keyword or punctuation token, so repeated occurrences
        /// share one string instance.</summary>
        internal string CachedText
        {
            set => _text = value;
        }

        /// <summary>Decoded <see cref="Value"/> for a synthetic token.</summary>
        internal string CachedValue
        {
            set => _value = value;
        }

        /// <summary>1-based <see cref="Line"/> for a synthetic token.</summary>
        internal int CachedLine
        {
            set => _line = value;
        }

        /// <summary>1-based <see cref="Column"/> for a synthetic token.</summary>
        internal int CachedColumn
        {
            set => _column = value;
        }

        /// <summary>Raw lexeme text as it appears in the source.</summary>
        public string Text
        {
            get
            {
                if (_text != null) return _text;
                var punctuation = PunctuationText.Get(Kind);
                if (punctuation != null) return punctuation;
                return _source != null ? _source.GetText(Span) : string.Empty;
            }
        }

        /// <summary>Decoded payload for string literals (escapes resolved, quotes stripped);
        /// otherwise same as <see cref="Text"/>.</summary>
        public string Value
        {
            get
            {
                if (_value != null) return _value;
                if (Kind == HlslTokenKind.StringLiteral)
                {
                    var raw = _source != null ? _source.GetText(ValueSpan) : string.Empty;
                    _value = StringEscapes.Decode(raw);
                }
                else
                {
                    _value = Text;
                }

                return _value;
            }
        }

        public int Line
        {
            get
            {
                if (_line == 0) ComputeLinePosition();
                return _line;
            }
        }

        public int Column
        {
            get
            {
                if (_line == 0) ComputeLinePosition();
                return _column;
            }
        }

        private void ComputeLinePosition()
        {
            if (_source == null)
            {
                _line = 1;
                _column = 1;
                return;
            }

            var position = _source.GetLinePosition(Span.Start);
            _line = position.Line;
            _column = position.Column;
        }

        public override string ToString()
        {
            if (IsMissing) return "Missing<" + Kind + ">";
            switch (Kind)
            {
                case HlslTokenKind.EndOfFile:
                    return "<EOF>";
                case HlslTokenKind.StringLiteral:
                    return "StringLiteral(\"" + Value + "\")";
                default:
                    return Kind + "(" + Text + ")";
            }
        }

        /// <summary>
        /// Produces a synthetic zero-length token standing in for an expected-but-absent token, so
        /// a parser can report a diagnostic and keep building a coherent tree instead of throwing.
        /// </summary>
        /// <param name="kind">The kind the caller expected to find.</param>
        /// <param name="span">A zero-length span at the point of failure (absolute coordinates).</param>
        /// <param name="line">1-based line of the failure point, for <see cref="Line"/>.</param>
        /// <param name="column">1-based column of the failure point, for <see cref="Column"/>.</param>
        public static Token Missing(HlslTokenKind kind, TextSpan span, int line, int column)
        {
            return new Token
            {
                Kind = kind,
                Span = span,
                ValueSpan = span,
                CachedText = string.Empty,
                CachedLine = line,
                CachedColumn = column,
                IsMissing = true
            };
        }
    }

    /// <summary>Cached constant text for punctuation/EOF kinds, avoiding any source access.
    /// Returns null for kinds whose text genuinely depends on the source (Identifier/Keyword/
    /// literal kinds/Unknown).</summary>
    internal static class PunctuationText
    {
        public static string Get(HlslTokenKind kind)
        {
            return kind switch
            {
                HlslTokenKind.OpenBrace => "{",
                HlslTokenKind.CloseBrace => "}",
                HlslTokenKind.OpenParen => "(",
                HlslTokenKind.CloseParen => ")",
                HlslTokenKind.OpenBracket => "[",
                HlslTokenKind.CloseBracket => "]",
                HlslTokenKind.Semicolon => ";",
                HlslTokenKind.Comma => ",",
                HlslTokenKind.Dot => ".",
                HlslTokenKind.Question => "?",
                HlslTokenKind.Colon => ":",
                HlslTokenKind.Equals => "=",
                HlslTokenKind.PlusEquals => "+=",
                HlslTokenKind.MinusEquals => "-=",
                HlslTokenKind.StarEquals => "*=",
                HlslTokenKind.SlashEquals => "/=",
                HlslTokenKind.PercentEquals => "%=",
                HlslTokenKind.AmpersandEquals => "&=",
                HlslTokenKind.PipeEquals => "|=",
                HlslTokenKind.CaretEquals => "^=",
                HlslTokenKind.LessThanLessThanEquals => "<<=",
                HlslTokenKind.GreaterThanGreaterThanEquals => ">>=",
                HlslTokenKind.EqualsEquals => "==",
                HlslTokenKind.ExclamationEquals => "!=",
                HlslTokenKind.LessThan => "<",
                HlslTokenKind.GreaterThan => ">",
                HlslTokenKind.LessThanEquals => "<=",
                HlslTokenKind.GreaterThanEquals => ">=",
                HlslTokenKind.AmpersandAmpersand => "&&",
                HlslTokenKind.PipePipe => "||",
                HlslTokenKind.Exclamation => "!",
                HlslTokenKind.Ampersand => "&",
                HlslTokenKind.Pipe => "|",
                HlslTokenKind.Caret => "^",
                HlslTokenKind.Tilde => "~",
                HlslTokenKind.LessThanLessThan => "<<",
                HlslTokenKind.GreaterThanGreaterThan => ">>",
                HlslTokenKind.PlusPlus => "++",
                HlslTokenKind.MinusMinus => "--",
                HlslTokenKind.Plus => "+",
                HlslTokenKind.Minus => "-",
                HlslTokenKind.Star => "*",
                HlslTokenKind.Slash => "/",
                HlslTokenKind.Percent => "%",
                HlslTokenKind.Hash => "#",
                HlslTokenKind.HashHash => "##",
                HlslTokenKind.EndOfFile => string.Empty,
                _ => null
            };
        }
    }

    /// <summary>Decodes the small set of backslash escapes HLSL/Cg string literals support.
    /// Never throws — an unrecognized escape is passed through literally (backslash dropped,
    /// next character kept), matching this library's best-effort recovery philosophy.</summary>
    internal static class StringEscapes
    {
        public static string Decode(string raw)
        {
            if (raw == null || raw.IndexOf('\\') < 0) return raw ?? string.Empty;

            var builder = new System.Text.StringBuilder(raw.Length);
            for (var i = 0; i < raw.Length; i++)
            {
                var c = raw[i];
                if (c != '\\' || i + 1 >= raw.Length)
                {
                    builder.Append(c);
                    continue;
                }

                var next = raw[i + 1];
                switch (next)
                {
                    case '\\': builder.Append('\\'); break;
                    case '"': builder.Append('"'); break;
                    case 'n': builder.Append('\n'); break;
                    case 't': builder.Append('\t'); break;
                    case 'r': builder.Append('\r'); break;
                    case '0': builder.Append('\0'); break;
                    default: builder.Append(next); break;
                }

                i++;
            }

            return builder.ToString();
        }
    }
}