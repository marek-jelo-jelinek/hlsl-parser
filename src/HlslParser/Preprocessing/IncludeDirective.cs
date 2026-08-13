using HlslParser.Text;

namespace HlslParser.Preprocessing
{
    /// <summary>Whether an <c>#include</c> path was written with quotes or angle brackets.</summary>
    public enum IncludeKind
    {
        Quoted,
        AngleBracketed
    }

    /// <summary>An <c>#include</c> directive recognized syntactically by <see cref="Preprocessor"/>.</summary>
    public sealed class IncludeDirective
    {
        public IncludeDirective(string path, IncludeKind kind, TextSpan span, TextSpan pathSpan)
        {
            Path = path ?? string.Empty;
            Kind = kind;
            Span = span;
            PathSpan = pathSpan;
        }

        /// <summary>The path text as written, without surrounding quotes/angle brackets.</summary>
        public string Path { get; }

        public IncludeKind Kind { get; }

        /// <summary>Absolute span of the whole <c>#include ...</c> directive line.</summary>
        public TextSpan Span { get; }

        /// <summary>Absolute span of just the path text.</summary>
        public TextSpan PathSpan { get; }
    }
}