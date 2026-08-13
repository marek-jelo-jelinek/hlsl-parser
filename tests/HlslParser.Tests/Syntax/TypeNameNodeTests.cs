using System.Linq;
using HlslParser.Lexing;
using HlslParser.Syntax;
using HlslParser.Text;
using NUnit.Framework;

namespace HlslParser.Tests.Syntax
{
    [TestFixture]
    public class TypeNameNodeTests
    {
        private sealed class RecordingVisitor : HlslVisitor
        {
            public TypeNameNode Visited;
            public override void VisitTypeName(TypeNameNode node) => Visited = node;
        }

        [Test]
        public void KindIsTypeName()
        {
            var node = new TypeNameNode(new TextSpan(0, 5), "float4", HlslKeywordCategory.VectorType, null);
            Assert.AreEqual(HlslNodeKind.TypeName, node.Kind);
        }

        [Test]
        public void NullNameBecomesEmptyString()
        {
            var node = new TypeNameNode(new TextSpan(0, 0), null, HlslKeywordCategory.None, null);
            Assert.AreEqual(string.Empty, node.Name);
        }

        [Test]
        public void IsUserTypeReflectsNoneCategory()
        {
            var user = new TypeNameNode(new TextSpan(0, 1), "MyStruct", HlslKeywordCategory.None, null);
            var builtin = new TypeNameNode(new TextSpan(0, 1), "float", HlslKeywordCategory.ScalarType, null);

            Assert.IsTrue(user.IsUserType);
            Assert.IsFalse(builtin.IsUserType);
        }

        [Test]
        public void NullTypeArgumentsFreezeToEmpty()
        {
            var node = new TypeNameNode(new TextSpan(0, 1), "float", HlslKeywordCategory.ScalarType, null);
            Assert.AreEqual(0, node.TypeArguments.Count);
            CollectionAssert.IsEmpty(node.Children.ToList());
        }

        [Test]
        public void TypeArgumentsAreExposedInOrderAsChildren()
        {
            var float4 = new TypeNameNode(new TextSpan(10, 6), "float4", HlslKeywordCategory.VectorType, null);
            var texture = new TypeNameNode(new TextSpan(0, 17), "Texture2D", HlslKeywordCategory.ResourceType, new[] { float4 });

            CollectionAssert.AreEqual(new HlslNode[] { float4 }, texture.Children.ToList());
            Assert.AreSame(float4, texture.TypeArguments[0]);
        }

        [Test]
        public void AcceptDispatchesToVisitTypeName()
        {
            var node = new TypeNameNode(new TextSpan(0, 1), "float", HlslKeywordCategory.ScalarType, null);
            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.Visited);
        }
    }
}
