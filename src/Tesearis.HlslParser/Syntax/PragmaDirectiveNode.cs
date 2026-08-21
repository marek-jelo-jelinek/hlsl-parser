using System;
using System.Collections.Generic;
using Tesearis.HlslParser.Text;

namespace Tesearis.HlslParser.Syntax
{
    /// <summary>
    /// Represents a <c>#pragma</c> directive recognized during preprocessing and preserved on the
    /// AST (such as Unity compute shader kernels <c>#pragma kernel CSMain</c>, shader variants
    /// <c>#pragma multi_compile</c>, or ray tracing settings <c>#pragma max_recursion_depth 5</c>).
    /// </summary>
    public sealed class PragmaDirectiveNode : HlslNode
    {
        private readonly IReadOnlyList<string> _arguments;
        private readonly IReadOnlyList<TextSpan> _argumentSpans;

        public PragmaDirectiveNode(TextSpan span, int line, string name, IEnumerable<string> arguments,
            IEnumerable<TextSpan> argumentSpans, string rawText) : base(span)
        {
            Line = line;
            Name = name ?? string.Empty;
            _arguments = Freeze(arguments);
            _argumentSpans = Freeze(argumentSpans);
            RawText = rawText ?? string.Empty;
        }

        public override HlslNodeKind Kind => HlslNodeKind.PragmaDirective;

        /// <summary>1-based line number where the pragma directive appears in source.</summary>
        public int Line { get; }

        /// <summary>The pragma verb or name, e.g. <c>kernel</c>, <c>multi_compile</c>, <c>target</c>.</summary>
        public string Name { get; }

        /// <summary>Tokenized argument strings following the pragma name.</summary>
        public IReadOnlyList<string> Arguments => _arguments;

        /// <summary>Source spans for each tokenized argument in <see cref="Arguments"/>.</summary>
        public IReadOnlyList<TextSpan> ArgumentSpans => _argumentSpans;

        /// <summary>The unparsed body/directive text as written in source.</summary>
        public string RawText { get; }

        public override void Accept(HlslVisitor visitor) => visitor.VisitPragmaDirective(this);
    }
}
