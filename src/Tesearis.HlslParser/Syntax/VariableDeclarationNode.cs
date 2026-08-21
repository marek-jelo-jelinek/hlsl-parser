using System.Collections.Generic;
using Tesearis.HlslParser.Text;

namespace Tesearis.HlslParser.Syntax
{
    public sealed class GlobalVariableDeclarationNode : HlslNode
    {
        private readonly IReadOnlyList<string> _modifiers;
        private readonly IReadOnlyList<VariableDeclaratorNode> _declarators;

        public GlobalVariableDeclarationNode(TextSpan span, IEnumerable<string> modifiers, TypeNameNode type,
            IEnumerable<VariableDeclaratorNode> declarators) : base(span)
        {
            _modifiers = Freeze(modifiers);
            Type = type;
            _declarators = Freeze(declarators);
        }

        public override HlslNodeKind Kind => HlslNodeKind.GlobalVariableDeclaration;

        /// <summary>Canonical modifier texts (e.g. "static", "const", "uniform"), source order.</summary>
        public IReadOnlyList<string> Modifiers => _modifiers;

        public TypeNameNode Type { get; }

        /// <summary>Always at least one entry.</summary>
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
            visitor.VisitGlobalVariableDeclaration(this);
        }
    }

    /// <summary>One name (plus optional array ranks/semantic/register/packoffset/initializer) in a
    /// comma-separated declarator list.</summary>
    public sealed class VariableDeclaratorNode : HlslNode
    {
        private readonly IReadOnlyList<ArrayRankNode> _arrayRanks;

        public VariableDeclaratorNode(TextSpan span, string name, IEnumerable<ArrayRankNode> arrayRanks,
            SemanticClauseNode semantic, RegisterClauseNode registerClause, PackoffsetClauseNode packoffsetClause,
            InitializerNode initializer) : base(span)
        {
            Name = name ?? string.Empty;
            _arrayRanks = Freeze(arrayRanks);
            Semantic = semantic;
            RegisterClause = registerClause;
            PackoffsetClause = packoffsetClause;
            Initializer = initializer;
        }

        public override HlslNodeKind Kind => HlslNodeKind.VariableDeclarator;

        /// <summary>Empty when the declarator name token was missing.</summary>
        public string Name { get; }

        public IReadOnlyList<ArrayRankNode> ArrayRanks => _arrayRanks;

        public SemanticClauseNode Semantic { get; }

        public RegisterClauseNode RegisterClause { get; }

        public PackoffsetClauseNode PackoffsetClause { get; }

        public InitializerNode Initializer { get; }

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                foreach (var rank in _arrayRanks) yield return rank;
                if (Semantic != null) yield return Semantic;
                if (RegisterClause != null) yield return RegisterClause;
                if (PackoffsetClause != null) yield return PackoffsetClause;
                if (Initializer != null) yield return Initializer;
            }
        }

        public override void Accept(HlslVisitor visitor) => visitor.VisitVariableDeclarator(this);
    }

    /// <summary>A <c>: register(slot[, space])</c> clause.</summary>
    public sealed class RegisterClauseNode : HlslNode
    {
        public RegisterClauseNode(TextSpan span, string registerSlot, string registerSpace) : base(span)
        {
            RegisterSlot = registerSlot ?? string.Empty;
            RegisterSpace = registerSpace;
        }

        public override HlslNodeKind Kind => HlslNodeKind.RegisterClause;

        /// <summary>Verbatim slot text, e.g. "b0" — kept as text since the numbering namespace
        /// (b/t/s/u/c) is resource-kind-dependent, not something this grammar validates.</summary>
        public string RegisterSlot { get; }

        /// <summary>Verbatim space text, e.g. "space1"; null when absent.</summary>
        public string RegisterSpace { get; }

        public override void Accept(HlslVisitor visitor) => visitor.VisitRegisterClause(this);
    }

    /// <summary>A <c>: packoffset(offset[.swizzle])</c> clause.</summary>
    public sealed class PackoffsetClauseNode : HlslNode
    {
        public PackoffsetClauseNode(TextSpan span, string offset, string componentSwizzle) : base(span)
        {
            Offset = offset ?? string.Empty;
            ComponentSwizzle = componentSwizzle;
        }

        public override HlslNodeKind Kind => HlslNodeKind.PackoffsetClause;

        /// <summary>Verbatim offset text, e.g. "c0".</summary>
        public string Offset { get; }

        /// <summary>Verbatim component swizzle, e.g. "x"; null when absent.</summary>
        public string ComponentSwizzle { get; }

        public override void Accept(HlslVisitor visitor) => visitor.VisitPackoffsetClause(this);
    }

    /// <summary>A <c>: SEMANTIC</c> clause on a declarator, parameter, or function return value.</summary>
    public sealed class SemanticClauseNode : HlslNode
    {
        public SemanticClauseNode(TextSpan span, string name) : base(span)
        {
            Name = name ?? string.Empty;
        }

        public override HlslNodeKind Kind => HlslNodeKind.SemanticClause;

        /// <summary>Verbatim semantic name, e.g. "SV_Target0" — deliberately not validated against
        /// a known-semantics table.</summary>
        public string Name { get; }

        public override void Accept(HlslVisitor visitor) => visitor.VisitSemanticClause(this);
    }

    /// <summary>An <c>= expression</c> initializer on a declarator or parameter default value.
    /// <see cref="Expression"/> is a real parsed expression tree.</summary>
    public sealed class InitializerNode : HlslNode
    {
        public InitializerNode(TextSpan span, HlslNode expression) : base(span)
        {
            Expression = expression;
        }

        public override HlslNodeKind Kind => HlslNodeKind.Initializer;

        public HlslNode Expression { get; }

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                if (Expression != null) yield return Expression;
            }
        }

        public override void Accept(HlslVisitor visitor)
        {
            visitor.VisitInitializer(this);
        }
    }
}