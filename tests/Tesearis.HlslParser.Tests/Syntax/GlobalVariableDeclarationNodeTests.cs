using System.Linq;
using Tesearis.HlslParser.Lexing;
using Tesearis.HlslParser.Syntax;
using Tesearis.HlslParser.Text;
using NUnit.Framework;

namespace Tesearis.HlslParser.Tests.Syntax
{
    [TestFixture]
    public class GlobalVariableDeclarationNodeTests
    {
        private static TypeNameNode Float() => new TypeNameNode(new TextSpan(0, 5), "float", HlslKeywordCategory.ScalarType, null);

        private static VariableDeclaratorNode Declarator(string name) =>
            new VariableDeclaratorNode(new TextSpan(0, name.Length), name, null, null, null, null, null);

        private sealed class RecordingVisitor : HlslVisitor
        {
            public GlobalVariableDeclarationNode VisitedGlobal;
            public VariableDeclaratorNode VisitedDeclarator;
            public RegisterClauseNode VisitedRegister;
            public PackoffsetClauseNode VisitedPackoffset;
            public SemanticClauseNode VisitedSemantic;
            public InitializerNode VisitedInitializer;

            public override void VisitGlobalVariableDeclaration(GlobalVariableDeclarationNode node) => VisitedGlobal = node;
            public override void VisitVariableDeclarator(VariableDeclaratorNode node) => VisitedDeclarator = node;
            public override void VisitRegisterClause(RegisterClauseNode node) => VisitedRegister = node;
            public override void VisitPackoffsetClause(PackoffsetClauseNode node) => VisitedPackoffset = node;
            public override void VisitSemanticClause(SemanticClauseNode node) => VisitedSemantic = node;
            public override void VisitInitializer(InitializerNode node) => VisitedInitializer = node;
        }

        [Test]
        public void GlobalVariableKindAndNullModifiersFreezeToEmpty()
        {
            var node = new GlobalVariableDeclarationNode(new TextSpan(0, 1), null, Float(), new[] { Declarator("x") });
            Assert.AreEqual(HlslNodeKind.GlobalVariableDeclaration, node.Kind);
            Assert.AreEqual(0, node.Modifiers.Count);
        }

        [Test]
        public void GlobalVariableChildrenAreTypeThenDeclaratorsInOrder()
        {
            var type = Float();
            var d1 = Declarator("a");
            var d2 = Declarator("b");
            var node = new GlobalVariableDeclarationNode(new TextSpan(0, 1), new[] { "static", "const" }, type, new[] { d1, d2 });

            CollectionAssert.AreEqual(new HlslNode[] { type, d1, d2 }, node.Children.ToList());
            CollectionAssert.AreEqual(new[] { "static", "const" }, node.Modifiers);
        }

        [Test]
        public void GlobalVariableAcceptDispatchesToVisitGlobalVariableDeclaration()
        {
            var node = new GlobalVariableDeclarationNode(new TextSpan(0, 1), null, Float(), new[] { Declarator("x") });
            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.VisitedGlobal);
        }

        [Test]
        public void DeclaratorNullNameBecomesEmptyAndNullListsFreezeToEmpty()
        {
            var node = new VariableDeclaratorNode(new TextSpan(0, 0), null, null, null, null, null, null);
            Assert.AreEqual(string.Empty, node.Name);
            Assert.AreEqual(0, node.ArrayRanks.Count);
            CollectionAssert.IsEmpty(node.Children.ToList());
        }

        [Test]
        public void DeclaratorExposesOptionalChildrenOnlyWhenPresent()
        {
            var rank = new ArrayRankNode(new TextSpan(0, 2), false, null);
            var semantic = new SemanticClauseNode(new TextSpan(0, 1), "SV_Target");
            var register = new RegisterClauseNode(new TextSpan(0, 1), "b0", null);
            var packoffset = new PackoffsetClauseNode(new TextSpan(0, 1), "c0", "x");
            var initializer = new InitializerNode(new TextSpan(0, 1), new IdentifierExpressionNode(new TextSpan(0, 1), "x"));

            var node = new VariableDeclaratorNode(new TextSpan(0, 1), "x", new[] { rank }, semantic, register, packoffset, initializer);

            CollectionAssert.AreEqual(new HlslNode[] { rank, semantic, register, packoffset, initializer }, node.Children.ToList());
            Assert.AreSame(semantic, node.Semantic);
            Assert.AreSame(register, node.RegisterClause);
            Assert.AreSame(packoffset, node.PackoffsetClause);
            Assert.AreSame(initializer, node.Initializer);
        }

        [Test]
        public void DeclaratorAcceptDispatchesToVisitVariableDeclarator()
        {
            var node = Declarator("x");
            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.VisitedDeclarator);
        }

        [Test]
        public void RegisterClauseNullSpaceStaysNullAndKindIsCorrect()
        {
            var node = new RegisterClauseNode(new TextSpan(0, 1), "b0", null);
            Assert.AreEqual(HlslNodeKind.RegisterClause, node.Kind);
            Assert.AreEqual("b0", node.RegisterSlot);
            Assert.IsNull(node.RegisterSpace);

            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.VisitedRegister);
        }

        [Test]
        public void PackoffsetClauseNullSwizzleStaysNullAndKindIsCorrect()
        {
            var node = new PackoffsetClauseNode(new TextSpan(0, 1), "c0", null);
            Assert.AreEqual(HlslNodeKind.PackoffsetClause, node.Kind);
            Assert.AreEqual("c0", node.Offset);
            Assert.IsNull(node.ComponentSwizzle);

            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.VisitedPackoffset);
        }

        [Test]
        public void SemanticClauseKindAndAccept()
        {
            var node = new SemanticClauseNode(new TextSpan(0, 1), "SV_Position");
            Assert.AreEqual(HlslNodeKind.SemanticClause, node.Kind);

            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.VisitedSemantic);
        }

        [Test]
        public void InitializerNullExpressionHasNoChildren()
        {
            var node = new InitializerNode(new TextSpan(0, 3), null);
            Assert.AreEqual(HlslNodeKind.Initializer, node.Kind);
            CollectionAssert.IsEmpty(node.Children.ToList());
        }

        [Test]
        public void InitializerExposesExpressionAsChildAndAccepts()
        {
            var expression = new IdentifierExpressionNode(new TextSpan(0, 1), "x");
            var node = new InitializerNode(new TextSpan(0, 3), expression);
            Assert.AreSame(expression, node.Expression);
            CollectionAssert.AreEqual(new HlslNode[] { expression }, node.Children.ToList());

            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.VisitedInitializer);
        }

        [Test]
        public void ArrayRankReflectsHasContentAndConstantSize()
        {
            var unsized = new ArrayRankNode(new TextSpan(0, 2), false, null);
            var expr = new ArrayRankNode(new TextSpan(0, 5), true, null);
            var constant = new ArrayRankNode(new TextSpan(0, 3), true, 4);

            Assert.IsFalse(unsized.HasContent);
            Assert.IsNull(unsized.ConstantSize);
            Assert.IsTrue(expr.HasContent);
            Assert.IsNull(expr.ConstantSize);
            Assert.IsTrue(constant.HasContent);
            Assert.AreEqual(4, constant.ConstantSize);
            Assert.AreEqual(HlslNodeKind.ArrayRank, constant.Kind);
        }
    }
}
