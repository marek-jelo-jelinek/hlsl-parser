using System.Collections.Generic;
using Tesearis.HlslParser.Text;

namespace Tesearis.HlslParser.Syntax
{
    public sealed class FunctionDeclarationNode : HlslNode
    {
        private readonly IReadOnlyList<AttributeNode> _attributes;
        private readonly IReadOnlyList<string> _modifiers;
        private readonly IReadOnlyList<HlslNode> _parameters;

        public FunctionDeclarationNode(TextSpan span, IEnumerable<AttributeNode> attributes,
            IEnumerable<string> modifiers, TypeNameNode returnType, string name,
            IEnumerable<HlslNode> parameters, SemanticClauseNode returnSemantic, HlslNode body) : base(span)
        {
            _attributes = Freeze(attributes);
            _modifiers = Freeze(modifiers);
            ReturnType = returnType;
            Name = name ?? string.Empty;
            _parameters = Freeze(parameters);
            ReturnSemantic = returnSemantic;
            Body = body;
        }

        public override HlslNodeKind Kind => HlslNodeKind.FunctionDeclaration;

        /// <summary>E.g. <c>[numthreads(8,8,1)]</c>.</summary>
        public IReadOnlyList<AttributeNode> Attributes => _attributes;

        /// <summary>E.g. "inline".</summary>
        public IReadOnlyList<string> Modifiers => _modifiers;

        public TypeNameNode ReturnType { get; }

        public string Name { get; }

        /// <summary><see cref="ParameterNode"/> entries in source order, interleaved with
        /// <see cref="ErrorNode"/> wherever a parameter couldn't be recognized. May legitimately
        /// be empty (<c>void Foo()</c>).</summary>
        public IReadOnlyList<HlslNode> Parameters => _parameters;

        /// <summary>The <c>: SV_Target</c>-style clause on the function header; null when absent.</summary>
        public SemanticClauseNode ReturnSemantic { get; }

        /// <summary>A <see cref="BlockStatementNode"/> full of real parsed statements; null for a
        /// <c>;</c>-terminated forward declaration, or when the body couldn't be recovered at all
        /// — disambiguate the two via <c>Diagnostics</c>, not tree shape. Typed as the base
        /// <see cref="HlslNode"/> so a future phase could swap in a richer body node without an
        /// API break.</summary>
        public HlslNode Body { get; }

        public bool IsForwardDeclaration => Body == null;

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                foreach (var attribute in _attributes) yield return attribute;
                if (ReturnType != null) yield return ReturnType;
                foreach (var parameter in _parameters) yield return parameter;
                if (ReturnSemantic != null) yield return ReturnSemantic;
                if (Body != null) yield return Body;
            }
        }

        public override void Accept(HlslVisitor visitor) => visitor.VisitFunctionDeclaration(this);
    }

    /// <summary>One parameter in a function's parameter list.</summary>
    public sealed class ParameterNode : HlslNode
    {
        private readonly IReadOnlyList<string> _modifiers;
        private readonly IReadOnlyList<ArrayRankNode> _arrayRanks;

        public ParameterNode(TextSpan span, IEnumerable<string> modifiers, TypeNameNode type, string name, IEnumerable<ArrayRankNode> arrayRanks,
            SemanticClauseNode semantic, InitializerNode defaultValue) : base(span)
        {
            _modifiers = Freeze(modifiers);
            Type = type;
            Name = name ?? string.Empty;
            _arrayRanks = Freeze(arrayRanks);
            Semantic = semantic;
            DefaultValue = defaultValue;
        }

        public override HlslNodeKind Kind => HlslNodeKind.Parameter;

        /// <summary>E.g. "in", "out", "inout", "uniform", "precise".</summary>
        public IReadOnlyList<string> Modifiers => _modifiers;

        public TypeNameNode Type { get; }

        /// <summary>May legitimately be empty — a prototype-only parameter (<c>void Foo(float);</c>)
        /// is valid HLSL and isn't a diagnostic.</summary>
        public string Name { get; }

        public IReadOnlyList<ArrayRankNode> ArrayRanks => _arrayRanks;

        public SemanticClauseNode Semantic { get; }

        /// <summary>A rare default-value initializer; null when absent.</summary>
        public InitializerNode DefaultValue { get; }

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                if (Type != null) yield return Type;
                foreach (var rank in _arrayRanks) yield return rank;
                if (Semantic != null) yield return Semantic;
                if (DefaultValue != null) yield return DefaultValue;
            }
        }

        public override void Accept(HlslVisitor visitor)
        {
            visitor.VisitParameter(this);
        }
    }
}