using System.Collections.Generic;
using Tesearis.HlslParser.Lexing;
using Tesearis.HlslParser.Text;

namespace Tesearis.HlslParser.Syntax
{
    /// <summary>A literal token used as an expression: an integer/float/string literal, or the
    /// contextual boolean literals <c>true</c>/<c>false</c> (not real keywords in this lexer's
    /// table — matched by identifier text, mirroring how <c>register</c>/<c>packoffset</c> are
    /// matched elsewhere in this parser).</summary>
    public sealed class LiteralExpressionNode : HlslNode
    {
        public LiteralExpressionNode(TextSpan span, HlslTokenKind tokenKind, string text, ulong integerValue,
            double floatValue, NumericLiteralSuffix numericSuffix, bool isHex) : base(span)
        {
            TokenKind = tokenKind;
            Text = text ?? string.Empty;
            IntegerValue = integerValue;
            FloatValue = floatValue;
            NumericSuffix = numericSuffix;
            IsHex = isHex;
        }

        public override HlslNodeKind Kind => HlslNodeKind.LiteralExpression;

        /// <summary>The lexical kind backing this literal: <c>IntegerLiteral</c>,
        /// <c>FloatLiteral</c>, <c>StringLiteral</c>, or <c>Identifier</c> for a contextual
        /// <c>true</c>/<c>false</c>.</summary>
        public HlslTokenKind TokenKind { get; }

        /// <summary>Raw source text, e.g. <c>"3.14f"</c>, <c>"true"</c>, <c>"\"hi\""</c>.</summary>
        public string Text { get; }

        public ulong IntegerValue { get; }
        public double FloatValue { get; }
        public NumericLiteralSuffix NumericSuffix { get; }
        public bool IsHex { get; }

        public bool IsBooleanLiteral => TokenKind == HlslTokenKind.Identifier;
        public bool BooleanValue => Text == "true";

        public override void Accept(HlslVisitor visitor) => visitor.VisitLiteralExpression(this);
    }

    /// <summary>A bare name reference — a variable/function name, or a built-in type name used as
    /// a constructor call's callee (e.g. the <c>float4</c> in <c>float4(1, 2, 3, 4)</c>).</summary>
    public sealed class IdentifierExpressionNode : HlslNode
    {
        public IdentifierExpressionNode(TextSpan span, string name) : base(span)
        {
            Name = name ?? string.Empty;
        }

        public override HlslNodeKind Kind => HlslNodeKind.IdentifierExpression;

        public string Name { get; }

        public override void Accept(HlslVisitor visitor) => visitor.VisitIdentifierExpression(this);
    }

    /// <summary>An explicitly parenthesized <c>(expr)</c> — kept as its own node (rather than
    /// discarded) so the tree reflects the source's explicit grouping, and because it's the
    /// fallback shape whenever a parenthesized construct isn't recognized as a
    /// <see cref="CastExpressionNode"/>.</summary>
    public sealed class ParenthesizedExpressionNode : HlslNode
    {
        public ParenthesizedExpressionNode(TextSpan span, HlslNode expression) : base(span)
        {
            Expression = expression;
        }

        public override HlslNodeKind Kind => HlslNodeKind.ParenthesizedExpression;

        public HlslNode Expression { get; }

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                if (Expression != null) yield return Expression;
            }
        }

        public override void Accept(HlslVisitor visitor) => visitor.VisitParenthesizedExpression(this);
    }

    /// <summary>A C-style cast <c>(Type)operand</c>, optionally preceded by type modifiers e.g. <c>(unorm float4)operand</c>.</summary>
    public sealed class CastExpressionNode : HlslNode
    {
        public CastExpressionNode(TextSpan span, IEnumerable<string> modifiers, TypeNameNode targetType, HlslNode operand) : base(span)
        {
            Modifiers = Freeze(modifiers);
            TargetType = targetType;
            Operand = operand;
        }

        public CastExpressionNode(TextSpan span, TypeNameNode targetType, HlslNode operand) : this(span, null, targetType, operand)
        {
        }

        public override HlslNodeKind Kind => HlslNodeKind.CastExpression;

        public IReadOnlyList<string> Modifiers { get; }
        public TypeNameNode TargetType { get; }
        public HlslNode Operand { get; }

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                if (TargetType != null) yield return TargetType;
                if (Operand != null) yield return Operand;
            }
        }

        public override void Accept(HlslVisitor visitor) => visitor.VisitCastExpression(this);
    }

    /// <summary>A prefix (<c>!x</c>, <c>~x</c>, <c>-x</c>, <c>+x</c>, <c>++x</c>, <c>--x</c>) or
    /// postfix (<c>x++</c>, <c>x--</c>) unary operator application.</summary>
    public sealed class UnaryExpressionNode : HlslNode
    {
        public UnaryExpressionNode(TextSpan span, HlslTokenKind operatorKind, HlslNode operand, bool isPostfix) : base(span)
        {
            OperatorKind = operatorKind;
            Operand = operand;
            IsPostfix = isPostfix;
        }

        public override HlslNodeKind Kind => HlslNodeKind.UnaryExpression;

        public HlslTokenKind OperatorKind { get; }
        public HlslNode Operand { get; }
        public bool IsPostfix { get; }

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                if (Operand != null) yield return Operand;
            }
        }

        public override void Accept(HlslVisitor visitor) => visitor.VisitUnaryExpression(this);
    }

    /// <summary>A binary operator application. One node kind covers every precedence level
    /// (logical/bitwise/equality/relational/shift/additive/multiplicative) — <see cref="OperatorKind"/>
    /// carries the actual operator token.</summary>
    public sealed class BinaryExpressionNode : HlslNode
    {
        public BinaryExpressionNode(TextSpan span, HlslNode left, HlslTokenKind operatorKind, HlslNode right) : base(span)
        {
            Left = left;
            OperatorKind = operatorKind;
            Right = right;
        }

        public override HlslNodeKind Kind => HlslNodeKind.BinaryExpression;

        public HlslNode Left { get; }
        public HlslTokenKind OperatorKind { get; }
        public HlslNode Right { get; }

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                if (Left != null) yield return Left;
                if (Right != null) yield return Right;
            }
        }

        public override void Accept(HlslVisitor visitor) => visitor.VisitBinaryExpression(this);
    }

    /// <summary>A ternary <c>condition ? whenTrue : whenFalse</c> expression.</summary>
    public sealed class ConditionalExpressionNode : HlslNode
    {
        public ConditionalExpressionNode(TextSpan span, HlslNode condition, HlslNode whenTrue, HlslNode whenFalse) : base(span)
        {
            Condition = condition;
            WhenTrue = whenTrue;
            WhenFalse = whenFalse;
        }

        public override HlslNodeKind Kind => HlslNodeKind.ConditionalExpression;

        public HlslNode Condition { get; }
        public HlslNode WhenTrue { get; }
        public HlslNode WhenFalse { get; }

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                if (Condition != null) yield return Condition;
                if (WhenTrue != null) yield return WhenTrue;
                if (WhenFalse != null) yield return WhenFalse;
            }
        }

        public override void Accept(HlslVisitor visitor) => visitor.VisitConditionalExpression(this);
    }

    /// <summary>An assignment <c>target op= value</c> (<c>=</c>, <c>+=</c>, <c>-=</c>, ... ).
    /// <see cref="Target"/> isn't grammatically restricted to lvalue-shaped expressions — this
    /// parser doesn't validate lvalue-ness, matching its "no semantic analysis" scope.</summary>
    public sealed class AssignmentExpressionNode : HlslNode
    {
        public AssignmentExpressionNode(TextSpan span, HlslNode target, HlslTokenKind operatorKind, HlslNode value) : base(span)
        {
            Target = target;
            OperatorKind = operatorKind;
            Value = value;
        }

        public override HlslNodeKind Kind => HlslNodeKind.AssignmentExpression;

        public HlslNode Target { get; }
        public HlslTokenKind OperatorKind { get; }
        public HlslNode Value { get; }

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                if (Target != null) yield return Target;
                if (Value != null) yield return Value;
            }
        }

        public override void Accept(HlslVisitor visitor) => visitor.VisitAssignmentExpression(this);
    }

    /// <summary>A call: <c>callee(arguments...)</c>. Covers both function calls and type
    /// constructor syntax (<c>float4(1, 2, 3, 4)</c>), since HLSL uses identical syntax for both
    /// and this parser has no symbol table to distinguish them.</summary>
    public sealed class InvocationExpressionNode : HlslNode
    {
        private readonly IReadOnlyList<HlslNode> _arguments;

        public InvocationExpressionNode(TextSpan span, HlslNode callee, IEnumerable<HlslNode> arguments) : base(span)
        {
            Callee = callee;
            _arguments = Freeze(arguments);
        }

        public override HlslNodeKind Kind => HlslNodeKind.InvocationExpression;

        public HlslNode Callee { get; }

        public IReadOnlyList<HlslNode> Arguments => _arguments;

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                if (Callee != null) yield return Callee;
                foreach (var argument in _arguments) yield return argument;
            }
        }

        public override void Accept(HlslVisitor visitor) => visitor.VisitInvocationExpression(this);
    }

    /// <summary>A brace-initializer list: <c>{ expr, expr, ... }</c>. Each element may itself be an <see cref="InitializerListExpressionNode"/> for
    /// nested aggregate initializers or an array-of-struct initializer.</summary>
    public sealed class InitializerListExpressionNode : HlslNode
    {
        public InitializerListExpressionNode(TextSpan span, IEnumerable<HlslNode> elements) : base(span)
        {
            Elements = Freeze(elements);
        }

        public override HlslNodeKind Kind => HlslNodeKind.InitializerListExpression;

        public IReadOnlyList<HlslNode> Elements { get; }

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                foreach (var element in Elements) yield return element;
            }
        }

        public override void Accept(HlslVisitor visitor) => visitor.VisitInitializerListExpression(this);
    }

    /// <summary>An index: <c>target[index]</c>.</summary>
    public sealed class ElementAccessExpressionNode : HlslNode
    {
        public ElementAccessExpressionNode(TextSpan span, HlslNode target, HlslNode index) : base(span)
        {
            Target = target;
            Index = index;
        }

        public override HlslNodeKind Kind => HlslNodeKind.ElementAccessExpression;

        public HlslNode Target { get; }
        public HlslNode Index { get; }

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                if (Target != null) yield return Target;
                if (Index != null) yield return Index;
            }
        }

        public override void Accept(HlslVisitor visitor) => visitor.VisitElementAccessExpression(this);
    }

    /// <summary>A member access: <c>target.name</c> — covers both struct-field access and
    /// vector-swizzle syntax (<c>v.xyz</c>) uniformly; distinguishing them needs type information
    /// this parser doesn't have.</summary>
    public sealed class MemberAccessExpressionNode : HlslNode
    {
        public MemberAccessExpressionNode(TextSpan span, HlslNode target, string memberName) : base(span)
        {
            Target = target;
            MemberName = memberName ?? string.Empty;
        }

        public override HlslNodeKind Kind => HlslNodeKind.MemberAccessExpression;

        public HlslNode Target { get; }
        public string MemberName { get; }

        public override IEnumerable<HlslNode> Children
        {
            get
            {
                if (Target != null) yield return Target;
            }
        }

        public override void Accept(HlslVisitor visitor)
        {
            visitor.VisitMemberAccessExpression(this);
        }
    }
}