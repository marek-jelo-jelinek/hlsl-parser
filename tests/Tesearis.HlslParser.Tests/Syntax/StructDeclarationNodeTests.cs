using System.Linq;
using Tesearis.HlslParser.Lexing;
using Tesearis.HlslParser.Syntax;
using Tesearis.HlslParser.Text;
using NUnit.Framework;

namespace Tesearis.HlslParser.Tests.Syntax
{
    [TestFixture]
    public class StructDeclarationNodeTests
    {
        private static TypeNameNode Float4() => new TypeNameNode(new TextSpan(0, 6), "float4", HlslKeywordCategory.VectorType, null);

        private static VariableDeclaratorNode Declarator(string name) =>
            new VariableDeclaratorNode(new TextSpan(0, name.Length), name, null, null, null, null, null);

        private sealed class RecordingVisitor : HlslVisitor
        {
            public StructDeclarationNode VisitedStruct;
            public StructFieldNode VisitedField;
            public override void VisitStructDeclaration(StructDeclarationNode node) => VisitedStruct = node;
            public override void VisitStructField(StructFieldNode node) => VisitedField = node;
        }

        [Test]
        public void StructDeclarationKindAndNullNameBecomesEmpty()
        {
            var node = new StructDeclarationNode(new TextSpan(0, 1), null, null, false);
            Assert.AreEqual(HlslNodeKind.StructDeclaration, node.Kind);
            Assert.AreEqual(string.Empty, node.Name);
        }

        [Test]
        public void StructDeclarationNullFieldsFreezeToEmpty()
        {
            var node = new StructDeclarationNode(new TextSpan(0, 1), "S", null, false);
            Assert.AreEqual(0, node.Fields.Count);
            CollectionAssert.IsEmpty(node.Children.ToList());
        }

        [Test]
        public void StructDeclarationFieldsAreExposedAsChildrenInOrder()
        {
            var field = new StructFieldNode(new TextSpan(0, 1), null, Float4(), new[] { Declarator("x") });
            var error = new ErrorNode(new TextSpan(1, 1), "bad");
            var node = new StructDeclarationNode(new TextSpan(0, 2), "S", new HlslNode[] { field, error }, false);

            CollectionAssert.AreEqual(new HlslNode[] { field, error }, node.Children.ToList());
        }

        [Test]
        public void IsMissingBodyReflectsConstructorArgument()
        {
            var missing = new StructDeclarationNode(new TextSpan(0, 1), "S", null, true);
            var present = new StructDeclarationNode(new TextSpan(0, 1), "S", null, false);
            Assert.IsTrue(missing.IsMissingBody);
            Assert.IsFalse(present.IsMissingBody);
        }

        [Test]
        public void StructDeclarationAcceptDispatchesToVisitStructDeclaration()
        {
            var node = new StructDeclarationNode(new TextSpan(0, 1), "S", null, false);
            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.VisitedStruct);
        }

        [Test]
        public void StructFieldKindAndModifiersAndDeclarators()
        {
            var declarator = Declarator("x");
            var type = Float4();
            var field = new StructFieldNode(new TextSpan(0, 5), new[] { "row_major" }, type, new[] { declarator });

            Assert.AreEqual(HlslNodeKind.StructField, field.Kind);
            Assert.AreEqual(1, field.Modifiers.Count);
            Assert.AreEqual("row_major", field.Modifiers[0]);
            CollectionAssert.AreEqual(new HlslNode[] { type, declarator }, field.Children.ToList());
        }

        [Test]
        public void StructFieldNullModifiersFreezeToEmpty()
        {
            var field = new StructFieldNode(new TextSpan(0, 1), null, Float4(), new[] { Declarator("x") });
            Assert.AreEqual(0, field.Modifiers.Count);
        }

        [Test]
        public void StructFieldAcceptDispatchesToVisitStructField()
        {
            var field = new StructFieldNode(new TextSpan(0, 1), null, Float4(), new[] { Declarator("x") });
            var visitor = new RecordingVisitor();
            field.Accept(visitor);
            Assert.AreSame(field, visitor.VisitedField);
        }
    }
}
