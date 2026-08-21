using System.Linq;
using Tesearis.HlslParser.Lexing;
using Tesearis.HlslParser.Syntax;
using Tesearis.HlslParser.Text;
using NUnit.Framework;

namespace Tesearis.HlslParser.Tests.Syntax
{
    [TestFixture]
    public class FunctionDeclarationNodeTests
    {
        private static TypeNameNode Float4() => new TypeNameNode(new TextSpan(0, 6), "float4", HlslKeywordCategory.VectorType, null);

        private sealed class RecordingVisitor : HlslVisitor
        {
            public FunctionDeclarationNode VisitedFunction;
            public ParameterNode VisitedParameter;
            public BlockStatementNode VisitedBody;
            public AttributeNode VisitedAttribute;
            public AttributeArgumentNode VisitedArgument;
            public TypedefDeclarationNode VisitedTypedef;

            public override void VisitFunctionDeclaration(FunctionDeclarationNode node) => VisitedFunction = node;
            public override void VisitParameter(ParameterNode node) => VisitedParameter = node;
            public override void VisitBlock(BlockStatementNode node) => VisitedBody = node;
            public override void VisitAttribute(AttributeNode node) => VisitedAttribute = node;
            public override void VisitAttributeArgument(AttributeArgumentNode node) => VisitedArgument = node;
            public override void VisitTypedefDeclaration(TypedefDeclarationNode node) => VisitedTypedef = node;
        }

        [Test]
        public void KindAndNullNameBecomesEmpty()
        {
            var node = new FunctionDeclarationNode(new TextSpan(0, 1), null, null, Float4(), null, null, null, null);
            Assert.AreEqual(HlslNodeKind.FunctionDeclaration, node.Kind);
            Assert.AreEqual(string.Empty, node.Name);
        }

        [Test]
        public void NullListsFreezeToEmpty()
        {
            var node = new FunctionDeclarationNode(new TextSpan(0, 1), null, null, Float4(), "Foo", null, null, null);
            Assert.AreEqual(0, node.Attributes.Count);
            Assert.AreEqual(0, node.Modifiers.Count);
            Assert.AreEqual(0, node.Parameters.Count);
        }

        [Test]
        public void NullBodyMeansForwardDeclaration()
        {
            var node = new FunctionDeclarationNode(new TextSpan(0, 1), null, null, Float4(), "Foo", null, null, null);
            Assert.IsTrue(node.IsForwardDeclaration);

            var withBody = new FunctionDeclarationNode(new TextSpan(0, 1), null, null, Float4(), "Foo", null, null,
                new BlockStatementNode(new TextSpan(0, 2), null));
            Assert.IsFalse(withBody.IsForwardDeclaration);
        }

        [Test]
        public void ChildrenAreAttributesThenReturnTypeThenParametersThenSemanticThenBodyInOrder()
        {
            var attribute = new AttributeNode(new TextSpan(0, 1), "numthreads", null);
            var returnType = Float4();
            var parameter = new ParameterNode(new TextSpan(0, 1), null, returnType, "p", null, null, null);
            var semantic = new SemanticClauseNode(new TextSpan(0, 1), "SV_Target");
            var body = new BlockStatementNode(new TextSpan(0, 2), null);

            var node = new FunctionDeclarationNode(new TextSpan(0, 1), new[] { attribute }, null, returnType, "Foo",
                new HlslNode[] { parameter }, semantic, body);

            CollectionAssert.AreEqual(new HlslNode[] { attribute, returnType, parameter, semantic, body }, node.Children.ToList());
        }

        [Test]
        public void AcceptDispatchesToVisitFunctionDeclaration()
        {
            var node = new FunctionDeclarationNode(new TextSpan(0, 1), null, null, Float4(), "Foo", null, null, null);
            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.VisitedFunction);
        }

        [Test]
        public void ParameterAllowsEmptyName()
        {
            var node = new ParameterNode(new TextSpan(0, 1), null, Float4(), null, null, null, null);
            Assert.AreEqual(HlslNodeKind.Parameter, node.Kind);
            Assert.AreEqual(string.Empty, node.Name);
        }

        [Test]
        public void ParameterChildrenOrder()
        {
            var type = Float4();
            var rank = new ArrayRankNode(new TextSpan(0, 2), false, null);
            var semantic = new SemanticClauseNode(new TextSpan(0, 1), "TEXCOORD0");
            var defaultValue = new InitializerNode(new TextSpan(0, 1), new IdentifierExpressionNode(new TextSpan(0, 1), "x"));

            var node = new ParameterNode(new TextSpan(0, 1), new[] { "in" }, type, "uv", new[] { rank }, semantic, defaultValue);

            CollectionAssert.AreEqual(new HlslNode[] { type, rank, semantic, defaultValue }, node.Children.ToList());
            CollectionAssert.AreEqual(new[] { "in" }, node.Modifiers);
        }

        [Test]
        public void ParameterAcceptDispatchesToVisitParameter()
        {
            var node = new ParameterNode(new TextSpan(0, 1), null, Float4(), "p", null, null, null);
            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.VisitedParameter);
        }

        [Test]
        public void BlockNullStatementsFreezeToEmptyAndAccepts()
        {
            var node = new BlockStatementNode(new TextSpan(0, 4), null);
            Assert.AreEqual(HlslNodeKind.Block, node.Kind);
            CollectionAssert.IsEmpty(node.Children.ToList());

            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.VisitedBody);
        }

        [Test]
        public void BlockStatementsAreChildrenInOrder()
        {
            var a = new EmptyStatementNode(new TextSpan(0, 1));
            var b = new EmptyStatementNode(new TextSpan(1, 1));
            var node = new BlockStatementNode(new TextSpan(0, 2), new HlslNode[] { a, b });

            CollectionAssert.AreEqual(new HlslNode[] { a, b }, node.Children.ToList());
        }

        [Test]
        public void AttributeNullArgumentsFreezeToEmptyAndAccepts()
        {
            var node = new AttributeNode(new TextSpan(0, 1), "maxvertexcount", null);
            Assert.AreEqual(HlslNodeKind.Attribute, node.Kind);
            Assert.AreEqual(0, node.Arguments.Count);

            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.VisitedAttribute);
        }

        private static LiteralExpressionNode IntegerLiteral(string text) =>
            new LiteralExpressionNode(new TextSpan(0, text.Length), HlslTokenKind.IntegerLiteral, text,
                ulong.Parse(text), 0, NumericLiteralSuffix.None, false);

        [Test]
        public void AttributeArgumentsAreChildrenInOrder()
        {
            var a = new AttributeArgumentNode(new TextSpan(0, 1), IntegerLiteral("8"), "8");
            var b = new AttributeArgumentNode(new TextSpan(2, 1), IntegerLiteral("8"), "8");
            var node = new AttributeNode(new TextSpan(0, 5), "numthreads", new[] { a, b });

            CollectionAssert.AreEqual(new HlslNode[] { a, b }, node.Children.ToList());
        }

        [Test]
        public void AttributeArgumentRawTextAndAccept()
        {
            var node = new AttributeArgumentNode(new TextSpan(0, 1), IntegerLiteral("8"), "8");
            Assert.AreEqual(HlslNodeKind.AttributeArgument, node.Kind);
            Assert.AreEqual("8", node.RawText);

            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.VisitedArgument);
        }

        [Test]
        public void TypedefKindAndNullAliasBecomesEmpty()
        {
            var node = new TypedefDeclarationNode(new TextSpan(0, 1), Float4(), null, null);
            Assert.AreEqual(HlslNodeKind.TypedefDeclaration, node.Kind);
            Assert.AreEqual(string.Empty, node.AliasName);
        }

        [Test]
        public void TypedefChildrenAreUnderlyingTypeThenRanks()
        {
            var type = Float4();
            var rank = new ArrayRankNode(new TextSpan(0, 3), true, 4);
            var node = new TypedefDeclarationNode(new TextSpan(0, 1), type, "Float4Array", new[] { rank });

            CollectionAssert.AreEqual(new HlslNode[] { type, rank }, node.Children.ToList());
        }

        [Test]
        public void TypedefAcceptDispatchesToVisitTypedefDeclaration()
        {
            var node = new TypedefDeclarationNode(new TextSpan(0, 1), Float4(), "Alias", null);
            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.VisitedTypedef);
        }
    }
}