namespace HlslParser.Lexing
{
    /// <summary>
    /// The flat set of lexical token kinds produced by <see cref="Lexer"/> for the combined
    /// HLSL+Cg superset — punctuation/operators, literals, and the two identifier-shaped kinds.
    /// Preprocessor-only kinds (<see cref="Hash"/>/<see cref="HashHash"/>) only appear in the raw
    /// token stream the lexer produces; <see cref="Preprocessing.Preprocessor"/> consumes them
    /// and they never reach the parser.
    /// </summary>
    public enum HlslTokenKind
    {
        /// <summary>An unrecognized character; the lexer never throws, so this plus an Error
        /// diagnostic is emitted instead, and the cursor still advances.</summary>
        Unknown,
        EndOfFile,

        Identifier,

        /// <summary>An identifier-shaped lexeme found in <see cref="HlslKeywords"/> table; the
        /// specific keyword text and category are read from <see cref="Token.Text"/> and
        /// <see cref="HlslKeywords.GetCategory"/> rather than from separate enum members.
        /// </summary>
        Keyword,

        IntegerLiteral,
        FloatLiteral,
        StringLiteral,

        OpenBrace,
        CloseBrace,
        OpenParen,
        CloseParen,
        OpenBracket,
        CloseBracket,
        Semicolon,
        Comma,
        Dot,
        Question,
        Colon,

        Equals,
        PlusEquals,
        MinusEquals,
        StarEquals,
        SlashEquals,
        PercentEquals,
        AmpersandEquals,
        PipeEquals,
        CaretEquals,
        LessThanLessThanEquals,
        GreaterThanGreaterThanEquals,

        EqualsEquals,
        ExclamationEquals,
        LessThan,
        GreaterThan,
        LessThanEquals,
        GreaterThanEquals,

        AmpersandAmpersand,
        PipePipe,
        Exclamation,

        Ampersand,
        Pipe,
        Caret,
        Tilde,
        LessThanLessThan,
        GreaterThanGreaterThan,

        PlusPlus,
        MinusMinus,
        Plus,
        Minus,
        Star,
        Slash,
        Percent,

        /// <summary>A standalone <c>#</c>. Only meaningful pre-preprocessing: outside a macro
        /// replacement list it starts a directive line, inside one it's the stringize operator.</summary>
        Hash,

        /// <summary>A standalone <c>##</c> (token-paste), meaningful only inside a function-like macro replacement list.</summary>
        HashHash
    }
}