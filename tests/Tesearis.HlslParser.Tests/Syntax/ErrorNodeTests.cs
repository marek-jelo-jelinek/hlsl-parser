using Tesearis.HlslParser.Syntax;
using Tesearis.HlslParser.Text;
using NUnit.Framework;

namespace Tesearis.HlslParser.Tests.Syntax
{
    [TestFixture]
    public class ErrorNodeTests
    {
        private sealed class RecordingVisitor : HlslVisitor
        {
            public ErrorNode Visited;
            public override void VisitError(ErrorNode node) => Visited = node;
        }

        [Test]
        public void KindIsError()
        {
            var node = new ErrorNode(new TextSpan(0, 1), "oops");
            Assert.AreEqual(HlslNodeKind.Error, node.Kind);
        }

        [Test]
        public void NullMessageBecomesEmptyString()
        {
            var node = new ErrorNode(new TextSpan(0, 1), null);
            Assert.AreEqual(string.Empty, node.Message);
        }

        [Test]
        public void HasNoChildren()
        {
            var node = new ErrorNode(new TextSpan(0, 1), "oops");
            CollectionAssert.IsEmpty(new System.Collections.Generic.List<HlslNode>(node.Children));
        }

        [Test]
        public void AcceptDispatchesToVisitError()
        {
            var node = new ErrorNode(new TextSpan(0, 1), "oops");
            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.Visited);
        }
    }
}
