using System.Collections.Generic;
using HlslParser.Text;

namespace HlslParser.Syntax
{
    public sealed class BlockStatementNode : HlslNode
    {
        private readonly IReadOnlyList<HlslNode> _statements;

        public BlockStatementNode(TextSpan span, IEnumerable<HlslNode> statements) : base(span)
        {
            _statements = Freeze(statements);
        }

        public override HlslNodeKind Kind => HlslNodeKind.Block;

        /// <summary>Statement nodes in source order, interleaved with <see cref="ErrorNode"/>
        /// wherever a statement couldn't be recognized at all.</summary>
        public IReadOnlyList<HlslNode> Statements => _statements;

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                foreach (var statement in _statements) yield return statement;
            }
        }

        public override void Accept(HlslVisitor visitor) => visitor.VisitBlock(this);
    }

    /// <summary>A bare expression used as a statement, e.g. <c>foo();</c> or <c>x = 1;</c>.</summary>
    public sealed class ExpressionStatementNode : HlslNode
    {
        public ExpressionStatementNode(TextSpan span, HlslNode expression) : base(span)
        {
            Expression = expression;
        }

        public override HlslNodeKind Kind => HlslNodeKind.ExpressionStatement;

        public HlslNode Expression { get; }

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                if (Expression != null) yield return Expression;
            }
        }

        public override void Accept(HlslVisitor visitor) => visitor.VisitExpressionStatement(this);
    }

    /// <summary>A local variable declaration statement, e.g. <c>float3 p = a + b;</c>. Reuses the
    /// same modifier/type/declarator-list shape as <see cref="GlobalVariableDeclarationNode"/>
    /// (register/packoffset clauses are grammatically permitted but never legal on a local — this
    /// parser doesn't validate that; see the library's "no semantic analysis" scope).</summary>
    public sealed class DeclarationStatementNode : HlslNode
    {
        private readonly IReadOnlyList<string> _modifiers;
        private readonly IReadOnlyList<VariableDeclaratorNode> _declarators;

        public DeclarationStatementNode(TextSpan span, IEnumerable<string> modifiers, TypeNameNode type,
            IEnumerable<VariableDeclaratorNode> declarators) : base(span)
        {
            _modifiers = Freeze(modifiers);
            Type = type;
            _declarators = Freeze(declarators);
        }

        public override HlslNodeKind Kind => HlslNodeKind.DeclarationStatement;

        /// <summary>Canonical modifier texts (e.g. "static", "const"), source order.</summary>
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

        public override void Accept(HlslVisitor visitor) => visitor.VisitDeclarationStatement(this);
    }

    /// <summary>An <c>if (condition) then [else elseStatement]</c> statement.</summary>
    public sealed class IfStatementNode : HlslNode
    {
        public IfStatementNode(TextSpan span, HlslNode condition, HlslNode thenStatement, HlslNode elseStatement) : base(span)
        {
            Condition = condition;
            Then = thenStatement;
            Else = elseStatement;
        }

        public override HlslNodeKind Kind => HlslNodeKind.IfStatement;

        public HlslNode Condition { get; }
        public HlslNode Then { get; }

        /// <summary>Nullable — absent when there's no <c>else</c> clause.</summary>
        public HlslNode Else { get; }

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                if (Condition != null) yield return Condition;
                if (Then != null) yield return Then;
                if (Else != null) yield return Else;
            }
        }

        public override void Accept(HlslVisitor visitor) => visitor.VisitIfStatement(this);
    }

    /// <summary>A classic C-style <c>for (initializer; condition; incrementor) body</c> loop. Any
    /// of the three head clauses may be omitted (<c>for (;;)</c> is legal).</summary>
    public sealed class ForStatementNode : HlslNode
    {
        public ForStatementNode(TextSpan span, HlslNode initializer, HlslNode condition, HlslNode incrementor, HlslNode body)
            : base(span)
        {
            Initializer = initializer;
            Condition = condition;
            Incrementor = incrementor;
            Body = body;
        }

        public override HlslNodeKind Kind => HlslNodeKind.ForStatement;

        /// <summary>A <see cref="DeclarationStatementNode"/> or <see cref="ExpressionStatementNode"/>; null when omitted.</summary>
        public HlslNode Initializer { get; }

        public HlslNode Condition { get; }
        public HlslNode Incrementor { get; }
        public HlslNode Body { get; }

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                if (Initializer != null) yield return Initializer;
                if (Condition != null) yield return Condition;
                if (Incrementor != null) yield return Incrementor;
                if (Body != null) yield return Body;
            }
        }

        public override void Accept(HlslVisitor visitor) => visitor.VisitForStatement(this);
    }

    /// <summary>A <c>while (condition) body</c> loop.</summary>
    public sealed class WhileStatementNode : HlslNode
    {
        public WhileStatementNode(TextSpan span, HlslNode condition, HlslNode body) : base(span)
        {
            Condition = condition;
            Body = body;
        }

        public override HlslNodeKind Kind => HlslNodeKind.WhileStatement;

        public HlslNode Condition { get; }
        public HlslNode Body { get; }

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                if (Condition != null) yield return Condition;
                if (Body != null) yield return Body;
            }
        }

        public override void Accept(HlslVisitor visitor) => visitor.VisitWhileStatement(this);
    }

    /// <summary>A <c>do body while (condition);</c> loop.</summary>
    public sealed class DoStatementNode : HlslNode
    {
        public DoStatementNode(TextSpan span, HlslNode body, HlslNode condition) : base(span)
        {
            Body = body;
            Condition = condition;
        }

        public override HlslNodeKind Kind => HlslNodeKind.DoStatement;

        public HlslNode Body { get; }
        public HlslNode Condition { get; }

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                if (Body != null) yield return Body;
                if (Condition != null) yield return Condition;
            }
        }

        public override void Accept(HlslVisitor visitor) => visitor.VisitDoStatement(this);
    }

    /// <summary>A <c>switch (expression) { sections... }</c> statement.</summary>
    public sealed class SwitchStatementNode : HlslNode
    {
        private readonly IReadOnlyList<HlslNode> _sections;

        public SwitchStatementNode(TextSpan span, HlslNode expression, IEnumerable<HlslNode> sections) : base(span)
        {
            Expression = expression;
            _sections = Freeze(sections);
        }

        public override HlslNodeKind Kind => HlslNodeKind.SwitchStatement;

        public HlslNode Expression { get; }

        /// <summary><see cref="SwitchSectionNode"/> entries, interleaved with <see cref="ErrorNode"/>
        /// wherever a section couldn't be recognized.</summary>
        public IReadOnlyList<HlslNode> Sections => _sections;

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                if (Expression != null) yield return Expression;
                foreach (var section in _sections) yield return section;
            }
        }

        public override void Accept(HlslVisitor visitor) => visitor.VisitSwitchStatement(this);
    }

    /// <summary>One or more stacked <c>case</c>/<c>default</c> labels sharing a single fallthrough
    /// statement list, e.g. <c>case 1: case 2: foo(); break;</c>.</summary>
    public sealed class SwitchSectionNode : HlslNode
    {
        private readonly IReadOnlyList<SwitchLabelNode> _labels;
        private readonly IReadOnlyList<HlslNode> _statements;

        public SwitchSectionNode(TextSpan span, IEnumerable<SwitchLabelNode> labels, IEnumerable<HlslNode> statements)
            : base(span)
        {
            _labels = Freeze(labels);
            _statements = Freeze(statements);
        }

        public override HlslNodeKind Kind => HlslNodeKind.SwitchSection;

        /// <summary>Always at least one entry.</summary>
        public IReadOnlyList<SwitchLabelNode> Labels => _labels;

        public IReadOnlyList<HlslNode> Statements => _statements;

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                foreach (var label in _labels) yield return label;
                foreach (var statement in _statements) yield return statement;
            }
        }

        public override void Accept(HlslVisitor visitor) => visitor.VisitSwitchSection(this);
    }

    /// <summary>One <c>case value:</c> or <c>default:</c> label.</summary>
    public sealed class SwitchLabelNode : HlslNode
    {
        public SwitchLabelNode(TextSpan span, HlslNode value, bool isDefault) : base(span)
        {
            Value = value;
            IsDefault = isDefault;
        }

        public override HlslNodeKind Kind => HlslNodeKind.SwitchLabel;

        /// <summary>The case value expression; null for a <c>default:</c> label.</summary>
        public HlslNode Value { get; }

        public bool IsDefault { get; }

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                if (Value != null) yield return Value;
            }
        }

        public override void Accept(HlslVisitor visitor) => visitor.VisitSwitchLabel(this);
    }

    /// <summary>A <c>return [expression];</c> statement.</summary>
    public sealed class ReturnStatementNode : HlslNode
    {
        public ReturnStatementNode(TextSpan span, HlslNode expression) : base(span)
        {
            Expression = expression;
        }

        public override HlslNodeKind Kind => HlslNodeKind.ReturnStatement;

        /// <summary>Nullable — a bare <c>return;</c> is legal for a <c>void</c> function.</summary>
        public HlslNode Expression { get; }

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                if (Expression != null) yield return Expression;
            }
        }

        public override void Accept(HlslVisitor visitor) => visitor.VisitReturnStatement(this);
    }

    /// <summary>A <c>discard;</c> statement (pixel-shader fragment kill).</summary>
    public sealed class DiscardStatementNode : HlslNode
    {
        public DiscardStatementNode(TextSpan span) : base(span)
        {
        }

        public override HlslNodeKind Kind => HlslNodeKind.DiscardStatement;

        public override void Accept(HlslVisitor visitor) => visitor.VisitDiscardStatement(this);
    }

    /// <summary>A <c>break;</c> statement.</summary>
    public sealed class BreakStatementNode : HlslNode
    {
        public BreakStatementNode(TextSpan span) : base(span)
        {
        }

        public override HlslNodeKind Kind => HlslNodeKind.BreakStatement;

        public override void Accept(HlslVisitor visitor) => visitor.VisitBreakStatement(this);
    }

    /// <summary>A <c>continue;</c> statement.</summary>
    public sealed class ContinueStatementNode : HlslNode
    {
        public ContinueStatementNode(TextSpan span) : base(span)
        {
        }

        public override HlslNodeKind Kind => HlslNodeKind.ContinueStatement;

        public override void Accept(HlslVisitor visitor) => visitor.VisitContinueStatement(this);
    }

    /// <summary>A bare <c>;</c> with no content.</summary>
    public sealed class EmptyStatementNode : HlslNode
    {
        public EmptyStatementNode(TextSpan span) : base(span)
        {
        }

        public override HlslNodeKind Kind => HlslNodeKind.EmptyStatement;

        public override void Accept(HlslVisitor visitor) => visitor.VisitEmptyStatement(this);
    }
}