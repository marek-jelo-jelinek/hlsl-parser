using System.Collections.Generic;
using System.Linq;
using HlslParser.Lexing;
using HlslParser.Syntax;
using HlslParser.Text;
using NUnit.Framework;

namespace HlslParser.Tests.Syntax
{
    [TestFixture]
    public class HlslVisitorTests
    {
        private sealed class CountingVisitor : HlslVisitor
        {
            public int Count;

            public override void DefaultVisit(HlslNode node)
            {
                if (node != null) Count++;
                base.DefaultVisit(node);
            }
        }

        private sealed class RecordingTypeNameVisitor : HlslVisitor
        {
            public readonly List<string> Visited = new List<string>();
            private readonly bool _stopDescent;

            public RecordingTypeNameVisitor(bool stopDescent)
            {
                _stopDescent = stopDescent;
            }

            public override void VisitTypeName(TypeNameNode node)
            {
                Visited.Add(node.Name);
                if (!_stopDescent) DefaultVisit(node);
            }
        }

        private static CompilationUnitNode BuildChain()
        {
            var c = new TypeNameNode(new TextSpan(20, 5), "C", HlslKeywordCategory.None, null);
            var b = new TypeNameNode(new TextSpan(10, 15), "B", HlslKeywordCategory.None, new[] { c });
            var a = new TypeNameNode(new TextSpan(0, 25), "A", HlslKeywordCategory.None, new[] { b });
            return new CompilationUnitNode(new TextSpan(0, 30), new HlslNode[] { a });
        }

        [Test]
        public void DefaultVisitWalksEveryDescendantExactlyOnce()
        {
            var root = BuildChain();
            var visitor = new CountingVisitor();

            visitor.Visit(root);

            Assert.AreEqual(root.DescendantsAndSelf().Count(), visitor.Count);
        }

        [Test]
        public void VisitNullNoOps()
        {
            var visitor = new CountingVisitor();
            Assert.DoesNotThrow(() => visitor.Visit(null));
            Assert.AreEqual(0, visitor.Count);
        }

        [Test]
        public void DefaultVisitNullNoOps()
        {
            var visitor = new CountingVisitor();
            Assert.DoesNotThrow(() => visitor.DefaultVisit(null));
            Assert.AreEqual(0, visitor.Count);
        }

        [Test]
        public void OverridingWithoutCallingDefaultVisitStopsDescent()
        {
            var root = BuildChain();
            var visitor = new RecordingTypeNameVisitor(stopDescent: true);

            visitor.Visit(root);

            CollectionAssert.AreEqual(new[] { "A" }, visitor.Visited);
        }

        [Test]
        public void OverridingAndCallingDefaultVisitContinuesDescent()
        {
            var root = BuildChain();
            var visitor = new RecordingTypeNameVisitor(stopDescent: false);

            visitor.Visit(root);

            CollectionAssert.AreEqual(new[] { "A", "B", "C" }, visitor.Visited);
        }
    }
}
