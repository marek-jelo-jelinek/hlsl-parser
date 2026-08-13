using System.Collections.Generic;
using HlslParser.Text;

namespace HlslParser.Syntax
{
    /// <summary>A <c>struct Name { fields... }</c> declaration.</summary>
    public sealed class StructDeclarationNode : HlslNode
    {
        private readonly IReadOnlyList<HlslNode> _fields;

        public StructDeclarationNode(TextSpan span, string name, IEnumerable<HlslNode> fields, bool isMissingBody)
            : base(span)
        {
            Name = name ?? string.Empty;
            _fields = Freeze(fields);
            IsMissingBody = isMissingBody;
        }

        public override HlslNodeKind Kind => HlslNodeKind.StructDeclaration;

        /// <summary>Empty when the struct name token was missing.</summary>
        public string Name { get; }

        /// <summary><see cref="StructFieldNode"/> entries in source order, interleaved with
        /// <see cref="ErrorNode"/> wherever a member couldn't be recognized at all.</summary>
        public IReadOnlyList<HlslNode> Fields => _fields;

        /// <summary>True only when the opening <c>{</c> itself was never found (in which case
        /// <see cref="Fields"/> is empty) — junk inside an existing <c>{}</c> instead surfaces as
        /// <see cref="ErrorNode"/> entries in <see cref="Fields"/>.</summary>
        public bool IsMissingBody { get; }

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                foreach (var field in _fields) yield return field;
            }
        }

        public override void Accept(HlslVisitor visitor) => visitor.VisitStructDeclaration(this);
    }

    /// <summary>One <c>[modifiers] Type name[, name2, ...];</c> member line inside a struct body.</summary>
    public sealed class StructFieldNode : HlslNode
    {
        private readonly IReadOnlyList<string> _modifiers;
        private readonly IReadOnlyList<VariableDeclaratorNode> _declarators;

        public StructFieldNode(TextSpan span, IEnumerable<string> modifiers, TypeNameNode type,
            IEnumerable<VariableDeclaratorNode> declarators) : base(span)
        {
            _modifiers = Freeze(modifiers);
            Type = type;
            _declarators = Freeze(declarators);
        }

        public override HlslNodeKind Kind => HlslNodeKind.StructField;

        /// <summary>E.g. "linear", "noperspective", "row_major" — mirrors
        /// <see cref="GlobalVariableDeclarationNode.Modifiers"/> so interpolation/layout modifiers
        /// on struct fields (common on vertex-output/fragment-input structs) aren't discarded.</summary>
        public IReadOnlyList<string> Modifiers => _modifiers;

        public TypeNameNode Type { get; }

        /// <summary>Always at least one entry — an unparseable declarator name still yields a
        /// synthetic declarator with an empty <see cref="VariableDeclaratorNode.Name"/>.</summary>
        public IReadOnlyList<VariableDeclaratorNode> Declarators => _declarators;

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                if (Type != null) yield return Type;
                foreach (var declarator in _declarators) yield return declarator;
            }
        }

        public override void Accept(HlslVisitor visitor)
        {
            visitor.VisitStructField(this);
        }
    }
}
