using System.Collections.Generic;
using HlslParser.Text;

namespace HlslParser.Syntax
{
    public sealed class CompilationUnitNode : HlslNode
    {
        private readonly IReadOnlyList<HlslNode> _declarations;

        public CompilationUnitNode(TextSpan span, IEnumerable<HlslNode> declarations) : base(span)
        {
            _declarations = Freeze(declarations);
        }

        public override HlslNodeKind Kind => HlslNodeKind.CompilationUnit;

        /// <summary>Struct/cbuffer/typedef/global-variable/function declarations, or
        /// <see cref="ErrorNode"/> where a top-level construct couldn't be recognized at all —
        /// always in source order.</summary>
        public IReadOnlyList<HlslNode> Declarations => _declarations;

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                foreach (var declaration in _declarations) yield return declaration;
            }
        }

        public override void Accept(HlslVisitor visitor) => visitor.VisitCompilationUnit(this);
    }

    /// <summary>
    /// Stands in for a stretch of source the parser couldn't make sense of at all, so the tree
    /// stays walkable instead of the parser throwing. The corresponding diagnostic (reported at
    /// the same span) explains why; this node just marks where.
    /// </summary>
    public sealed class ErrorNode : HlslNode
    {
        public ErrorNode(TextSpan span, string message) : base(span)
        {
            Message = message ?? string.Empty;
        }

        public override HlslNodeKind Kind => HlslNodeKind.Error;

        /// <summary>Human-readable explanation, echoing the diagnostic reported at this span.</summary>
        public string Message { get; }

        public override void Accept(HlslVisitor visitor) => visitor.VisitError(this);
    }
}