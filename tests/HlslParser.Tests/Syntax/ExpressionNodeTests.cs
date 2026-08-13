using System.Linq;
using HlslParser.Lexing;
using HlslParser.Syntax;
using HlslParser.Text;
using NUnit.Framework;

namespace HlslParser.Tests.Syntax
{
    /// <summary>Unit coverage for every expression node: <c>Kind</c>, <c>Freeze</c>
    /// behavior on optional lists, <c>Children</c> order, and <c>Accept</c> dispatch.</summary>
    [TestFixture]
    public class ExpressionNodeTests
    {
        private static IdentifierExpressionNode Id(string name) => new IdentifierExpressionNode(new TextSpan(0, name.Length), name);

        private sealed class RecordingVisitor : HlslVisitor
        {
            public HlslNode Visited;
            public override void VisitLiteralExpression(LiteralExpressionNode node) => Visited = node;
            public override void VisitIdentifierExpression(IdentifierExpressionNode node) => Visited = node;
            public override void VisitParenthesizedExpression(ParenthesizedExpressionNode node) => Visited = node;
            public override void VisitCastExpression(CastExpressionNode node) => Visited = node;
            public override void VisitUnaryExpression(UnaryExpressionNode node) => Visited = node;
            public override void VisitBinaryExpression(BinaryExpressionNode node) => Visited = node;
            public override void VisitConditionalExpression(ConditionalExpressionNode node) => Visited = node;
            public override void VisitAssignmentExpression(AssignmentExpressionNode node) => Visited = node;
            public override void VisitInvocationExpression(InvocationExpressionNode node) => Visited = node;
            public override void VisitElementAccessExpression(ElementAccessExpressionNode node) => Visited = node;
            public override void VisitMemberAccessExpression(MemberAccessExpressionNode node) => Visited = node;
        }

        [Test]
        public void LiteralExposesNumericFieldsAndKind()
        {
            var node = new LiteralExpressionNode(new TextSpan(0, 1), HlslTokenKind.IntegerLiteral, "8", 8, 0, NumericLiteralSuffix.None, false);
            Assert.AreEqual(HlslNodeKind.LiteralExpression, node.Kind);
            Assert.AreEqual(8ul, node.IntegerValue);
            Assert.IsFalse(node.IsBooleanLiteral);
            CollectionAssert.IsEmpty(node.Children.ToList());
        }

        [Test]
        public void LiteralIdentifierKindMarksBooleanLiteral()
        {
            var trueNode = new LiteralExpressionNode(new TextSpan(0, 4), HlslTokenKind.Identifier, "true", 0, 0, NumericLiteralSuffix.None, false);
            var falseNode = new LiteralExpressionNode(new TextSpan(0, 5), HlslTokenKind.Identifier, "false", 0, 0, NumericLiteralSuffix.None, false);

            Assert.IsTrue(trueNode.IsBooleanLiteral);
            Assert.IsTrue(trueNode.BooleanValue);
            Assert.IsTrue(falseNode.IsBooleanLiteral);
            Assert.IsFalse(falseNode.BooleanValue);
        }

        [Test]
        public void LiteralAcceptDispatchesToVisitLiteralExpression()
        {
            var node = new LiteralExpressionNode(new TextSpan(0, 1), HlslTokenKind.IntegerLiteral, "1", 1, 0, NumericLiteralSuffix.None, false);
            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.Visited);
        }

        [Test]
        public void IdentifierNullNameBecomesEmpty()
        {
            var node = new IdentifierExpressionNode(new TextSpan(0, 0), null);
            Assert.AreEqual(HlslNodeKind.IdentifierExpression, node.Kind);
            Assert.AreEqual(string.Empty, node.Name);
        }

        [Test]
        public void IdentifierAcceptDispatchesToVisitIdentifierExpression()
        {
            var node = Id("x");
            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.Visited);
        }

        [Test]
        public void ParenthesizedExposesInnerAsChild()
        {
            var inner = Id("x");
            var node = new ParenthesizedExpressionNode(new TextSpan(0, 3), inner);
            Assert.AreEqual(HlslNodeKind.ParenthesizedExpression, node.Kind);
            CollectionAssert.AreEqual(new HlslNode[] { inner }, node.Children.ToList());

            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.Visited);
        }

        [Test]
        public void ParenthesizedNullInnerHasNoChildren()
        {
            var node = new ParenthesizedExpressionNode(new TextSpan(0, 2), null);
            CollectionAssert.IsEmpty(node.Children.ToList());
        }

        [Test]
        public void CastChildrenAreTargetTypeThenOperand()
        {
            var type = new TypeNameNode(new TextSpan(0, 5), "float", HlslKeywordCategory.ScalarType, null);
            var operand = Id("x");
            var node = new CastExpressionNode(new TextSpan(0, 8), type, operand);

            Assert.AreEqual(HlslNodeKind.CastExpression, node.Kind);
            CollectionAssert.AreEqual(new HlslNode[] { type, operand }, node.Children.ToList());

            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.Visited);
        }

        [Test]
        public void UnaryExposesOperatorKindAndIsPostfix()
        {
            var operand = Id("x");
            var prefix = new UnaryExpressionNode(new TextSpan(0, 2), HlslTokenKind.Minus, operand, isPostfix: false);
            var postfix = new UnaryExpressionNode(new TextSpan(0, 2), HlslTokenKind.PlusPlus, operand, isPostfix: true);

            Assert.AreEqual(HlslNodeKind.UnaryExpression, prefix.Kind);
            Assert.AreEqual(HlslTokenKind.Minus, prefix.OperatorKind);
            Assert.IsFalse(prefix.IsPostfix);
            Assert.IsTrue(postfix.IsPostfix);
            CollectionAssert.AreEqual(new HlslNode[] { operand }, prefix.Children.ToList());

            var visitor = new RecordingVisitor();
            prefix.Accept(visitor);
            Assert.AreSame(prefix, visitor.Visited);
        }

        [Test]
        public void BinaryChildrenAreLeftThenRight()
        {
            var left = Id("a");
            var right = Id("b");
            var node = new BinaryExpressionNode(new TextSpan(0, 5), left, HlslTokenKind.Plus, right);

            Assert.AreEqual(HlslNodeKind.BinaryExpression, node.Kind);
            Assert.AreEqual(HlslTokenKind.Plus, node.OperatorKind);
            CollectionAssert.AreEqual(new HlslNode[] { left, right }, node.Children.ToList());

            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.Visited);
        }

        [Test]
        public void ConditionalChildrenAreConditionThenBranches()
        {
            var condition = Id("c");
            var whenTrue = Id("t");
            var whenFalse = Id("f");
            var node = new ConditionalExpressionNode(new TextSpan(0, 5), condition, whenTrue, whenFalse);

            Assert.AreEqual(HlslNodeKind.ConditionalExpression, node.Kind);
            CollectionAssert.AreEqual(new HlslNode[] { condition, whenTrue, whenFalse }, node.Children.ToList());

            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.Visited);
        }

        [Test]
        public void AssignmentChildrenAreTargetThenValue()
        {
            var target = Id("x");
            var value = Id("y");
            var node = new AssignmentExpressionNode(new TextSpan(0, 5), target, HlslTokenKind.PlusEquals, value);

            Assert.AreEqual(HlslNodeKind.AssignmentExpression, node.Kind);
            Assert.AreEqual(HlslTokenKind.PlusEquals, node.OperatorKind);
            CollectionAssert.AreEqual(new HlslNode[] { target, value }, node.Children.ToList());

            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.Visited);
        }

        [Test]
        public void InvocationNullArgumentsFreezeToEmpty()
        {
            var callee = Id("foo");
            var node = new InvocationExpressionNode(new TextSpan(0, 5), callee, null);
            Assert.AreEqual(HlslNodeKind.InvocationExpression, node.Kind);
            Assert.AreEqual(0, node.Arguments.Count);
            CollectionAssert.AreEqual(new HlslNode[] { callee }, node.Children.ToList());
        }

        [Test]
        public void InvocationChildrenAreCalleeThenArgumentsInOrder()
        {
            var callee = Id("foo");
            var a = Id("a");
            var b = Id("b");
            var node = new InvocationExpressionNode(new TextSpan(0, 8), callee, new HlslNode[] { a, b });

            CollectionAssert.AreEqual(new HlslNode[] { callee, a, b }, node.Children.ToList());

            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.Visited);
        }

        [Test]
        public void ElementAccessChildrenAreTargetThenIndex()
        {
            var target = Id("arr");
            var index = Id("i");
            var node = new ElementAccessExpressionNode(new TextSpan(0, 6), target, index);

            Assert.AreEqual(HlslNodeKind.ElementAccessExpression, node.Kind);
            CollectionAssert.AreEqual(new HlslNode[] { target, index }, node.Children.ToList());

            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.Visited);
        }

        [Test]
        public void MemberAccessNullMemberNameBecomesEmpty()
        {
            var target = Id("v");
            var node = new MemberAccessExpressionNode(new TextSpan(0, 2), target, null);
            Assert.AreEqual(string.Empty, node.MemberName);
            CollectionAssert.AreEqual(new HlslNode[] { target }, node.Children.ToList());
        }

        [Test]
        public void MemberAccessExposesMemberNameAndAccepts()
        {
            var target = Id("v");
            var node = new MemberAccessExpressionNode(new TextSpan(0, 4), target, "xyz");
            Assert.AreEqual(HlslNodeKind.MemberAccessExpression, node.Kind);
            Assert.AreEqual("xyz", node.MemberName);

            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.Visited);
        }
    }
}