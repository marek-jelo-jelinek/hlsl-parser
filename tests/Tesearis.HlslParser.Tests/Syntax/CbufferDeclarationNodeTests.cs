using System.Linq;
using Tesearis.HlslParser.Lexing;
using Tesearis.HlslParser.Syntax;
using Tesearis.HlslParser.Text;
using NUnit.Framework;

namespace Tesearis.HlslParser.Tests.Syntax
{
    [TestFixture]
    public class CbufferDeclarationNodeTests
    {
        private static GlobalVariableDeclarationNode Member(string name)
        {
            var type = new TypeNameNode(new TextSpan(0, 5), "float", HlslKeywordCategory.ScalarType, null);
            var declarator = new VariableDeclaratorNode(new TextSpan(0, name.Length), name, null, null, null, null, null);
            return new GlobalVariableDeclarationNode(new TextSpan(0, 1), null, type, new[] { declarator });
        }

        private sealed class RecordingVisitor : HlslVisitor
        {
            public CbufferDeclarationNode Visited;
            public override void VisitCbufferDeclaration(CbufferDeclarationNode node) => Visited = node;
        }

        [Test]
        public void KindAndNullNameBecomesEmpty()
        {
            var node = new CbufferDeclarationNode(new TextSpan(0, 1), null, null, null, false);
            Assert.AreEqual(HlslNodeKind.CbufferDeclaration, node.Kind);
            Assert.AreEqual(string.Empty, node.Name);
        }

        [Test]
        public void NullMembersFreezeToEmpty()
        {
            var node = new CbufferDeclarationNode(new TextSpan(0, 1), "PerFrame", null, null, false);
            Assert.AreEqual(0, node.Members.Count);
            CollectionAssert.IsEmpty(node.Children.ToList());
        }

        [Test]
        public void RegisterClauseIsFirstChildWhenPresent()
        {
            var register = new RegisterClauseNode(new TextSpan(0, 1), "b0", null);
            var member = Member("x");
            var node = new CbufferDeclarationNode(new TextSpan(0, 1), "PerFrame", new HlslNode[] { member }, register, false);

            CollectionAssert.AreEqual(new HlslNode[] { register, member }, node.Children.ToList());
            Assert.AreSame(register, node.RegisterClause);
        }

        [Test]
        public void MembersCanIncludeErrorNodes()
        {
            var member = Member("x");
            var error = new ErrorNode(new TextSpan(1, 1), "bad");
            var node = new CbufferDeclarationNode(new TextSpan(0, 2), "PerFrame", new HlslNode[] { member, error }, null, false);

            CollectionAssert.AreEqual(new HlslNode[] { member, error }, node.Children.ToList());
        }

        [Test]
        public void IsMissingBodyReflectsConstructorArgument()
        {
            Assert.IsTrue(new CbufferDeclarationNode(new TextSpan(0, 1), "S", null, null, true).IsMissingBody);
            Assert.IsFalse(new CbufferDeclarationNode(new TextSpan(0, 1), "S", null, null, false).IsMissingBody);
        }

        [Test]
        public void AcceptDispatchesToVisitCbufferDeclaration()
        {
            var node = new CbufferDeclarationNode(new TextSpan(0, 1), "PerFrame", null, null, false);
            var visitor = new RecordingVisitor();
            node.Accept(visitor);
            Assert.AreSame(node, visitor.Visited);
        }
    }
}
