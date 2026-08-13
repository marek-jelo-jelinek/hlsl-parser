using System.Linq;
using HlslParser.Lexing;
using HlslParser.Syntax;
using HlslParser.Text;
using NUnit.Framework;

namespace HlslParser.Tests.Syntax
{
    /// <summary>Unit coverage for every statement node: <c>Kind</c>, <c>Freeze</c>
    /// behavior on optional lists, <c>Children</c> order, and <c>Accept</c> dispatch.</summary>
    [TestFixture]
    public class StatementNodeTests
    {
        private static IdentifierExpressionNode Id(string name) => new IdentifierExpressionNode(new TextSpan(0, name.Length), name);
        private static EmptyStatementNode Empty() => new EmptyStatementNode(new TextSpan(0, 1));

        private sealed class RecordingVisitor : HlslVisitor
        {
            public HlslNode Visited;
            public override void VisitBlock(BlockStatementNode node) => Visited = node;
            public override void VisitExpressionStatement(ExpressionStatementNode node) => Visited = node;
            public override void VisitDeclarationStatement(DeclarationStatementNode node) => Visited = node;
            public override void VisitIfStatement(IfStatementNode node) => Visited = node;
            public override void VisitForStatement(ForStatementNode node) => Visited = node;
            public override void VisitWhileStatement(WhileStatementNode node) => Visited = node;
            public override void VisitDoStatement(DoStatementNode node) => Visited = node;
            public override void VisitSwitchStatement(SwitchStatementNode node) => Visited = node;
            public override void VisitSwitchSection(SwitchSectionNode node) => Visited = node;
            public override void VisitSwitchLabel(SwitchLabelNode node) => Visited = node;
            public override void VisitReturnStatement(ReturnStatementNode node) => Visited = node;
            public override void VisitDiscardStatement(DiscardStatementNode node) => Visited = node;
            public override void VisitBreakStatement(BreakStatementNode node) => Visited = node;
            public override void VisitContinueStatement(ContinueStatementNode node) => Visited = node;
            public override void VisitEmptyStatement(EmptyStatementNode node) => Visited = node;
        }

        [Test]
        public void BlockNullStatementsFreezeToEmpty()
        {
            var node = new BlockStatementNode(new TextSpan(0, 2), null);
            Assert.AreEqual(HlslNodeKind.Block, node.Kind);
            CollectionAssert.IsEmpty(node.Children.ToList());
        }

        [Test]
        public void BlockAcceptDispatchesToVisitBlock()
        {
            var node = new BlockStatementNode(new TextSpan(0, 2), new HlslNode[] { Empty() });
            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.Visited);
        }

        [Test]
        public void ExpressionStatementExposesExpressionAsChild()
        {
            var expression = Id("x");
            var node = new ExpressionStatementNode(new TextSpan(0, 2), expression);
            Assert.AreEqual(HlslNodeKind.ExpressionStatement, node.Kind);
            CollectionAssert.AreEqual(new HlslNode[] { expression }, node.Children.ToList());

            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.Visited);
        }

        [Test]
        public void DeclarationStatementChildrenAreTypeThenDeclarators()
        {
            var type = new TypeNameNode(new TextSpan(0, 5), "float", HlslKeywordCategory.ScalarType, null);
            var declarator = new VariableDeclaratorNode(new TextSpan(0, 1), "x", null, null, null, null, null);
            var node = new DeclarationStatementNode(new TextSpan(0, 6), new[] { "const" }, type, new[] { declarator });

            Assert.AreEqual(HlslNodeKind.DeclarationStatement, node.Kind);
            CollectionAssert.AreEqual(new[] { "const" }, node.Modifiers);
            CollectionAssert.AreEqual(new HlslNode[] { type, declarator }, node.Children.ToList());

            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.Visited);
        }

        [Test]
        public void IfNullElseOmittedFromChildren()
        {
            var condition = Id("c");
            var then = Empty();
            var node = new IfStatementNode(new TextSpan(0, 5), condition, then, null);
            CollectionAssert.AreEqual(new HlslNode[] { condition, then }, node.Children.ToList());
            Assert.IsNull(node.Else);
        }

        [Test]
        public void IfWithElseIncludesItAsThirdChild()
        {
            var condition = Id("c");
            var then = Empty();
            var elseStatement = Empty();
            var node = new IfStatementNode(new TextSpan(0, 5), condition, then, elseStatement);

            Assert.AreEqual(HlslNodeKind.IfStatement, node.Kind);
            CollectionAssert.AreEqual(new HlslNode[] { condition, then, elseStatement }, node.Children.ToList());

            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.Visited);
        }

        [Test]
        public void ForAllClausesNullYieldsOnlyBodyAsChild()
        {
            var body = Empty();
            var node = new ForStatementNode(new TextSpan(0, 10), null, null, null, body);
            Assert.AreEqual(HlslNodeKind.ForStatement, node.Kind);
            CollectionAssert.AreEqual(new HlslNode[] { body }, node.Children.ToList());
        }

        [Test]
        public void ForAllClausesPresentInOrder()
        {
            var initializer = new ExpressionStatementNode(new TextSpan(0, 2), Id("i"));
            var condition = Id("c");
            var incrementor = Id("inc");
            var body = Empty();
            var node = new ForStatementNode(new TextSpan(0, 10), initializer, condition, incrementor, body);

            CollectionAssert.AreEqual(new HlslNode[] { initializer, condition, incrementor, body }, node.Children.ToList());

            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.Visited);
        }

        [Test]
        public void WhileChildrenAreConditionThenBody()
        {
            var condition = Id("c");
            var body = Empty();
            var node = new WhileStatementNode(new TextSpan(0, 5), condition, body);

            Assert.AreEqual(HlslNodeKind.WhileStatement, node.Kind);
            CollectionAssert.AreEqual(new HlslNode[] { condition, body }, node.Children.ToList());

            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.Visited);
        }

        [Test]
        public void DoChildrenAreBodyThenCondition()
        {
            var body = Empty();
            var condition = Id("c");
            var node = new DoStatementNode(new TextSpan(0, 5), body, condition);

            Assert.AreEqual(HlslNodeKind.DoStatement, node.Kind);
            CollectionAssert.AreEqual(new HlslNode[] { body, condition }, node.Children.ToList());

            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.Visited);
        }

        [Test]
        public void SwitchNullSectionsFreezeToEmpty()
        {
            var expression = Id("v");
            var node = new SwitchStatementNode(new TextSpan(0, 5), expression, null);
            Assert.AreEqual(HlslNodeKind.SwitchStatement, node.Kind);
            CollectionAssert.AreEqual(new HlslNode[] { expression }, node.Children.ToList());
        }

        [Test]
        public void SwitchAcceptDispatchesToVisitSwitchStatement()
        {
            var node = new SwitchStatementNode(new TextSpan(0, 5), Id("v"), null);
            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.Visited);
        }

        [Test]
        public void SwitchSectionChildrenAreLabelsThenStatements()
        {
            var label = new SwitchLabelNode(new TextSpan(0, 1), Id("1"), false);
            var statement = Empty();
            var node = new SwitchSectionNode(new TextSpan(0, 5), new[] { label }, new HlslNode[] { statement });

            Assert.AreEqual(HlslNodeKind.SwitchSection, node.Kind);
            CollectionAssert.AreEqual(new HlslNode[] { label, statement }, node.Children.ToList());

            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.Visited);
        }

        [Test]
        public void SwitchLabelDefaultHasNullValueAndNoChildren()
        {
            var node = new SwitchLabelNode(new TextSpan(0, 1), null, true);
            Assert.AreEqual(HlslNodeKind.SwitchLabel, node.Kind);
            Assert.IsTrue(node.IsDefault);
            Assert.IsNull(node.Value);
            CollectionAssert.IsEmpty(node.Children.ToList());
        }

        [Test]
        public void SwitchLabelCaseExposesValueAsChild()
        {
            var value = Id("1");
            var node = new SwitchLabelNode(new TextSpan(0, 1), value, false);
            Assert.IsFalse(node.IsDefault);
            CollectionAssert.AreEqual(new HlslNode[] { value }, node.Children.ToList());

            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.Visited);
        }

        [Test]
        public void ReturnNullExpressionHasNoChildren()
        {
            var node = new ReturnStatementNode(new TextSpan(0, 6), null);
            Assert.AreEqual(HlslNodeKind.ReturnStatement, node.Kind);
            CollectionAssert.IsEmpty(node.Children.ToList());
        }

        [Test]
        public void ReturnExposesExpressionAsChildAndAccepts()
        {
            var expression = Id("x");
            var node = new ReturnStatementNode(new TextSpan(0, 6), expression);
            CollectionAssert.AreEqual(new HlslNode[] { expression }, node.Children.ToList());

            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.Visited);
        }

        [Test]
        public void DiscardBreakContinueEmptyAreLeavesWithCorrectKindsAndAccept()
        {
            var discard = new DiscardStatementNode(new TextSpan(0, 8));
            var brk = new BreakStatementNode(new TextSpan(0, 6));
            var cont = new ContinueStatementNode(new TextSpan(0, 9));
            var empty = Empty();

            Assert.AreEqual(HlslNodeKind.DiscardStatement, discard.Kind);
            Assert.AreEqual(HlslNodeKind.BreakStatement, brk.Kind);
            Assert.AreEqual(HlslNodeKind.ContinueStatement, cont.Kind);
            Assert.AreEqual(HlslNodeKind.EmptyStatement, empty.Kind);

            CollectionAssert.IsEmpty(discard.Children.ToList());
            CollectionAssert.IsEmpty(brk.Children.ToList());
            CollectionAssert.IsEmpty(cont.Children.ToList());
            CollectionAssert.IsEmpty(empty.Children.ToList());

            var visitor = new RecordingVisitor();
            discard.Accept(visitor);
            Assert.AreSame(discard, visitor.Visited);
            brk.Accept(visitor);
            Assert.AreSame(brk, visitor.Visited);
            cont.Accept(visitor);
            Assert.AreSame(cont, visitor.Visited);
            empty.Accept(visitor);
            Assert.AreSame(empty, visitor.Visited);
        }
    }
}