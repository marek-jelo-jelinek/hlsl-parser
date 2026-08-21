using System.Collections.Generic;
using Tesearis.HlslParser.Text;

namespace Tesearis.HlslParser.Syntax
{
    public sealed class AttributeNode : HlslNode
    {
        private readonly IReadOnlyList<AttributeArgumentNode> _arguments;

        public AttributeNode(TextSpan span, string name, IEnumerable<AttributeArgumentNode> arguments) : base(span)
        {
            Name = name ?? string.Empty;
            _arguments = Freeze(arguments);
        }

        public override HlslNodeKind Kind => HlslNodeKind.Attribute;

        public string Name { get; }

        public IReadOnlyList<AttributeArgumentNode> Arguments => _arguments;

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                foreach (var argument in _arguments) yield return argument;
            }
        }

        public override void Accept(HlslVisitor visitor) => visitor.VisitAttribute(this);
    }

    /// <summary>
    /// One comma-delimited argument inside an <see cref="AttributeNode"/>'s parentheses, e.g. the
    /// <c>8</c>s in <c>[numthreads(8,8,1)]</c>. <see cref="Expression"/> is a real parsed
    /// expression tree; <see cref="RawText"/> is kept too as a convenient verbatim rendering.
    /// </summary>
    public sealed class AttributeArgumentNode : HlslNode
    {
        public AttributeArgumentNode(TextSpan span, HlslNode expression, string rawText) : base(span)
        {
            Expression = expression;
            RawText = rawText ?? string.Empty;
        }

        public override HlslNodeKind Kind => HlslNodeKind.AttributeArgument;

        public HlslNode Expression { get; }

        public string RawText { get; }

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                if (Expression != null) yield return Expression;
            }
        }

        public override void Accept(HlslVisitor visitor)
        {
            visitor.VisitAttributeArgument(this);
        }
    }
}