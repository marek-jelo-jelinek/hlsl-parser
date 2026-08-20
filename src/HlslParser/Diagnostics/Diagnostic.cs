using HlslParser.Text;

namespace HlslParser.Diagnostics
{
    public enum DiagnosticSeverity
    {
        /// <summary>Informational only, e.g. an unrecognized-but-ignored preprocessor directive.</summary>
        Info,

        /// <summary>Something is likely wrong (e.g. a macro redefinition with a differing body),
        /// but parsing continues with a reasonable fallback.</summary>
        Warning,

        /// <summary>Malformed input that this best-effort parser still recovers from - it never
        /// throws, so "Error" here means "the resulting tree may be incomplete or synthesized".</summary>
        Error
    }

    /// <summary>Stable identifiers so callers can filter or suppress specific messages.</summary>
    public static class DiagnosticIds
    {
        // Lexer (HL0001–HL0099)
        public const string UnrecognizedCharacter = "HL0001";
        public const string UnterminatedBlockComment = "HL0002";
        public const string UnterminatedString = "HL0003";
        public const string InvalidNumericLiteral = "HL0004";

        // Preprocessor (HL0100–HL0199)
        public const string MacroRedefinition = "HL0100";
        public const string MalformedMacroDefinition = "HL0101";
        public const string MalformedUndefDirective = "HL0102";
        public const string MacroArgumentCountMismatch = "HL0103";
        public const string UnterminatedMacroInvocation = "HL0104";
        public const string RecursiveMacroExpansionLimitExceeded = "HL0105";
        public const string MalformedStringizeOperator = "HL0106";
        public const string MalformedConditionalDirective = "HL0107";
        public const string MalformedDefinedOperator = "HL0108";
        public const string MalformedConstantExpression = "HL0109";
        public const string DivisionByZeroInConstantExpression = "HL0110";
        public const string UnbalancedElif = "HL0111";
        public const string UnbalancedElse = "HL0112";
        public const string UnbalancedEndIf = "HL0113";
        public const string ElifOrElseAfterElse = "HL0114";
        public const string UnterminatedConditional = "HL0115";
        public const string MalformedInclude = "HL0116";
        public const string UnknownPreprocessorDirective = "HL0117";
        public const string MalformedTokenPaste = "HL0118";
        public const string PreprocessorErrorDirective = "HL0119";
        public const string PreprocessorWarningDirective = "HL0120";
        public const string MalformedLineDirective = "HL0121";

        // Declaration parsing (HL0200–HL0299)
        public const string ExpectedToken = "HL0200";
        public const string UnexpectedToken = "HL0201";
        public const string ExpectedDeclaration = "HL0202";
        public const string MalformedStructDeclaration = "HL0203";
        public const string UnterminatedStruct = "HL0204";
        public const string MalformedCbufferDeclaration = "HL0205";
        public const string UnterminatedCbuffer = "HL0206";
        public const string MalformedVariableDeclaration = "HL0207";
        public const string MalformedRegisterClause = "HL0208";
        public const string MalformedPackoffsetClause = "HL0209";
        public const string MalformedSemantic = "HL0210";
        public const string MalformedFunctionDeclaration = "HL0211";
        public const string UnterminatedParameterList = "HL0212";
        // HL0213 retired (was UnterminatedFunctionBody, superseded by UnterminatedBlock/HL0302); never reuse.
        public const string MalformedAttribute = "HL0214";
        public const string MalformedArrayRank = "HL0215";
        public const string MissingTypeName = "HL0216";
        public const string UnexpectedEndOfFile = "HL0217";
        // HL0218–HL0299 reserved for the statement/expression-parser phase spillover.

        // Statement/expression parsing (HL0300–HL0399)
        public const string ExpectedExpression = "HL0300";
        public const string ExpectedStatement = "HL0301";
        public const string UnterminatedBlock = "HL0302";
        public const string MalformedSwitchLabel = "HL0303";
        // HL0304–HL0399 reserved for spillover (mostly reuses HL0200/HL0217 from declaration parsing).

        // Semantic analysis (HL0400–HL0499) — reserved for a future semantic-analysis layer.
    }

    public sealed class Diagnostic
    {
        public Diagnostic(DiagnosticSeverity severity, string id, string message, TextSpan span, SourceText source)
        {
            Severity = severity;
            Id = id;
            Message = message ?? string.Empty;
            Span = span;
            Source = source;
        }

        public DiagnosticSeverity Severity { get; }
        public string Id { get; }
        public string Message { get; }
        public TextSpan Span { get; }
        public SourceText Source { get; }

        public LinePosition Position => Source?.GetLinePosition(Span.Start) ?? new LinePosition(0, 0);

        public string FileName => Source?.GetFileName(Span.Start) ?? Source?.FileName ?? "<unknown>";

        public override string ToString()
        {
            var file = FileName;
            var pos = Position;
            var severity = Severity.ToString().ToLowerInvariant();
            return file + "(" + pos.Line + "," + pos.Column + "): " + severity + " " + Id + ": " + Message;
        }
    }
}