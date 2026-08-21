using System.Collections.Generic;
using Tesearis.HlslParser.Text;

namespace Tesearis.HlslParser.Syntax
{
    /// <summary>
    /// A classic brace-delimited <c>cbuffer Name [: register(bN)] { members... }</c> (or
    /// <c>tbuffer</c>) declaration. The modern templated form (<c>ConstantBuffer&lt;T&gt; cb0 :
    /// register(b0);</c>) is structurally an ordinary global-variable declaration instead — see
    /// <see cref="GlobalVariableDeclarationNode"/> — and never produces this node kind.
    /// </summary>
    public sealed class CbufferDeclarationNode : HlslNode
    {
        private readonly IReadOnlyList<HlslNode> _members;

        public CbufferDeclarationNode(TextSpan span, string name, IEnumerable<HlslNode> members,
            RegisterClauseNode registerClause, bool isMissingBody) : base(span)
        {
            Name = name ?? string.Empty;
            _members = Freeze(members);
            RegisterClause = registerClause;
            IsMissingBody = isMissingBody;
        }

        public override HlslNodeKind Kind => HlslNodeKind.CbufferDeclaration;

        /// <summary>Empty when the cbuffer name token was missing.</summary>
        public string Name { get; }

        /// <summary><see cref="GlobalVariableDeclarationNode"/> entries in source order,
        /// interleaved with <see cref="ErrorNode"/> wherever a member couldn't be recognized.</summary>
        public IReadOnlyList<HlslNode> Members => _members;

        /// <summary>Optional <c>: register(bN)</c> clause on the <c>cbuffer</c> line itself.</summary>
        public RegisterClauseNode RegisterClause { get; }

        /// <summary>True only when the opening <c>{</c> itself was never found.</summary>
        public bool IsMissingBody { get; }

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                if (RegisterClause != null) yield return RegisterClause;
                foreach (var member in _members) yield return member;
            }
        }

        public override void Accept(HlslVisitor visitor) => visitor.VisitCbufferDeclaration(this);
    }
}
