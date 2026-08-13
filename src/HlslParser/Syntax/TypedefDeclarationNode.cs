using System.Collections.Generic;
using HlslParser.Text;

namespace HlslParser.Syntax
{
    /// <summary>A <c>typedef UnderlyingType Alias[ranks];</c> declaration. Deliberately minimal —
    /// one alias name, no declarator-list/semantic/register/initializer, since real-world
    /// <c>typedef</c> usage never needs those.</summary>
    public sealed class TypedefDeclarationNode : HlslNode
    {
        private readonly IReadOnlyList<ArrayRankNode> _arrayRanks;

        public TypedefDeclarationNode(TextSpan span, TypeNameNode underlyingType, string aliasName,
            IEnumerable<ArrayRankNode> arrayRanks) : base(span)
        {
            UnderlyingType = underlyingType;
            AliasName = aliasName ?? string.Empty;
            _arrayRanks = Freeze(arrayRanks);
        }

        public override HlslNodeKind Kind => HlslNodeKind.TypedefDeclaration;

        public TypeNameNode UnderlyingType { get; }

        /// <summary>Empty when the alias name token was missing.</summary>
        public string AliasName { get; }

        public IReadOnlyList<ArrayRankNode> ArrayRanks => _arrayRanks;

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                if (UnderlyingType != null) yield return UnderlyingType;
                foreach (var rank in _arrayRanks) yield return rank;
            }
        }

        public override void Accept(HlslVisitor visitor)
        {
            visitor.VisitTypedefDeclaration(this);
        }
    }
}