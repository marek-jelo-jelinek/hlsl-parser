using System.Collections.Generic;
using Tesearis.HlslParser.Lexing;
using Tesearis.HlslParser.Text;

namespace Tesearis.HlslParser.Syntax
{
    public sealed class TypeNameNode : HlslNode
    {
        private readonly IReadOnlyList<TypeNameNode> _typeArguments;

        public TypeNameNode(TextSpan span, string name, HlslKeywordCategory category, IEnumerable<TypeNameNode> typeArguments)
            : base(span)
        {
            Name = name ?? string.Empty;
            Category = category;
            _typeArguments = Freeze(typeArguments);
        }

        public override HlslNodeKind Kind => HlslNodeKind.TypeName;

        /// <summary>Canonical keyword spelling for a built-in type, or the verbatim identifier
        /// text for a user-defined type.</summary>
        public string Name { get; }

        /// <summary><see cref="HlslKeywordCategory.None"/> for a user-defined (struct/typedef) type.</summary>
        public HlslKeywordCategory Category { get; }

        public bool IsUserType => Category == HlslKeywordCategory.None;

        /// <summary>Template arguments, e.g. the <c>float4</c> in <c>Texture2D&lt;float4&gt;</c>.
        /// Empty when this type isn't templated.</summary>
        public IReadOnlyList<TypeNameNode> TypeArguments => _typeArguments;

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                foreach (var typeArgument in _typeArguments) yield return typeArgument;
            }
        }

        public override void Accept(HlslVisitor visitor) => visitor.VisitTypeName(this);
    }

    /// <summary>One <c>[...]</c> array-rank suffix on a declarator or parameter.</summary>
    public sealed class ArrayRankNode : HlslNode
    {
        public ArrayRankNode(TextSpan span, bool hasContent, int? constantSize) : base(span)
        {
            HasContent = hasContent;
            ConstantSize = constantSize;
        }

        public override HlslNodeKind Kind => HlslNodeKind.ArrayRank;

        /// <summary>False for a bare <c>[]</c> (an unsized array, legal on some struct/parameter forms).</summary>
        public bool HasContent { get; }

        /// <summary>The rank's size when it was written as a single integer literal (the common
        /// case, e.g. <c>[4]</c>); null for a computed/expression size or an unsized rank — this
        /// grammar doesn't evaluate expressions.</summary>
        public int? ConstantSize { get; }

        public override void Accept(HlslVisitor visitor)
        {
            visitor.VisitArrayRank(this);
        }
    }
}