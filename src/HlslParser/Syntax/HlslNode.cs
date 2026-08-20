using System;
using System.Collections.Generic;
using HlslParser.Text;

namespace HlslParser.Syntax
{
    /// <summary>
    /// Discriminator for every AST node kind, exposed via <see cref="HlslNode.Kind"/>. The tree
    /// is otherwise a flat hierarchy — there's no intermediate <c>StatementNode</c>/
    /// <c>ExpressionNode</c> abstract base — so this enum, plus a plain type check or
    /// <see cref="HlslVisitor"/> double-dispatch, is how callers distinguish node shapes.
    /// </summary>
    public enum HlslNodeKind
    {
        CompilationUnit,
        Error,
        PragmaDirective,

        TypeName,
        ArrayRank,

        Attribute,
        AttributeArgument,

        StructDeclaration,
        StructField,

        CbufferDeclaration,

        TypedefDeclaration,

        GlobalVariableDeclaration,
        VariableDeclarator,
        RegisterClause,
        PackoffsetClause,
        SemanticClause,
        Initializer,

        FunctionDeclaration,
        Parameter,

        // Statements. A function's Body is a real Block full of parsed statements.
        Block,
        ExpressionStatement,
        DeclarationStatement,
        IfStatement,
        ForStatement,
        WhileStatement,
        DoStatement,
        SwitchStatement,
        SwitchSection,
        SwitchLabel,
        ReturnStatement,
        DiscardStatement,
        BreakStatement,
        ContinueStatement,
        EmptyStatement,

        // Expressions.
        LiteralExpression,
        IdentifierExpression,
        ParenthesizedExpression,
        CastExpression,
        UnaryExpression,
        BinaryExpression,
        ConditionalExpression,
        AssignmentExpression,
        InvocationExpression,
        ElementAccessExpression,
        MemberAccessExpression
    }

    /// <summary>
    /// Base class for every HLSL/Cg AST node. Nodes are immutable once constructed: all state is
    /// assigned in the constructor and exposed through read-only members, which makes the tree
    /// safe to share across threads and cache between analyses.
    /// </summary>
    public abstract class HlslNode
    {
        protected HlslNode(TextSpan span)
        {
            Span = span;
        }

        /// <summary>Full source range covered by this node, including its braces.</summary>
        public TextSpan Span { get; }

        public abstract HlslNodeKind Kind { get; }

        /// <summary>Direct children in source order. Never returns null entries.</summary>
        public virtual IEnumerable<HlslNode> Children
        {
            get { yield break; }
        }

        public abstract void Accept(HlslVisitor visitor);

        /// <summary>Pre-order walk of this node and every descendant, iterative rather than
        /// recursive so it never stack-overflows on a deep tree.</summary>
        public IEnumerable<HlslNode> DescendantsAndSelf()
        {
            yield return this;

            var stack = new Stack<IEnumerator<HlslNode>>();
            var current = Children.GetEnumerator();
            try
            {
                while (true)
                {
                    if (current.MoveNext())
                    {
                        var node = current.Current;
                        if (node == null) continue;

                        yield return node;
                        stack.Push(current);
                        current = node.Children.GetEnumerator();
                    }
                    else
                    {
                        current.Dispose();
                        if (stack.Count == 0) yield break;
                        current = stack.Pop();
                    }
                }
            }
            finally
            {
                current.Dispose();
                while (stack.Count > 0) stack.Pop().Dispose();
            }
        }

        /// <summary>Deepest node whose span contains the offset. Useful for editor tooling.</summary>
        public HlslNode FindNodeAt(int position)
        {
            if (!Span.Contains(position)) return null;
            var best = this;
            foreach (var child in Children)
            {
                var found = child?.FindNodeAt(position);
                if (found != null) best = found;
            }

            return best;
        }

        protected static IReadOnlyList<T> Freeze<T>(IEnumerable<T> items)
        {
            List<T> list = null;
            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item == null) continue;
                    list ??= new List<T>();
                    list.Add(item);
                }
            }

            if (list == null) return Array.Empty<T>();
            return list.AsReadOnly();
        }

        public override string ToString()
        {
            return Kind + " " + Span;
        }
    }
}