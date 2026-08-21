using System.Linq;
using Tesearis.HlslParser.Lexing;
using Tesearis.HlslParser.Syntax;
using Tesearis.HlslParser.Text;
using NUnit.Framework;

namespace Tesearis.HlslParser.Tests.Syntax
{
    /// <summary>Exercises <see cref="HlslNode"/>'s shared plumbing (<c>Freeze</c>,
    /// <c>DescendantsAndSelf</c>, <c>FindNodeAt</c>, <c>ToString</c>) via <see cref="ErrorNode"/>
    /// and <see cref="CompilationUnitNode"/> — the two node kinds with no other dependencies.</summary>
    [TestFixture]
    public class HlslNodeTests
    {
        [Test]
        public void FreezeReturnsSharedEmptyArrayForNullInput()
        {
            var a = new CompilationUnitNode(new TextSpan(0, 0), null).Declarations;
            var b = new CompilationUnitNode(new TextSpan(0, 0), null).Declarations;

            Assert.IsTrue(ReferenceEquals(a, b));
            Assert.AreEqual(0, a.Count);
        }

        [Test]
        public void FreezeReturnsSharedEmptyArrayForEmptyInput()
        {
            var a = new CompilationUnitNode(new TextSpan(0, 0), null).Declarations;
            var b = new CompilationUnitNode(new TextSpan(0, 0), new HlslNode[0]).Declarations;

            Assert.IsTrue(ReferenceEquals(a, b));
        }

        [Test]
        public void FreezeFiltersNullEntries()
        {
            var first = new ErrorNode(new TextSpan(0, 1), "a");
            var second = new ErrorNode(new TextSpan(1, 1), "b");
            var node = new CompilationUnitNode(new TextSpan(0, 2), new HlslNode[] { first, null, second });

            Assert.AreEqual(2, node.Declarations.Count);
            Assert.AreSame(first, node.Declarations[0]);
            Assert.AreSame(second, node.Declarations[1]);
        }

        // Root [0..30) -> A (TypeName) [0..25) -> B (TypeName) [10..24) -> C (TypeName) [20..23)
        // A three-level chain built via TypeNameNode.TypeArguments, the simplest real node kind
        // that nests. Each level's End is distinct so half-open boundary behavior is unambiguous.
        private static CompilationUnitNode BuildChain(out TypeNameNode a, out TypeNameNode b, out TypeNameNode c)
        {
            c = new TypeNameNode(new TextSpan(20, 3), "C", HlslKeywordCategory.None, null);
            b = new TypeNameNode(new TextSpan(10, 14), "B", HlslKeywordCategory.None, new[] { c });
            a = new TypeNameNode(new TextSpan(0, 25), "A", HlslKeywordCategory.None, new[] { b });
            return new CompilationUnitNode(new TextSpan(0, 30), new HlslNode[] { a });
        }

        [Test]
        public void DescendantsAndSelfIsPreOrderAndNonRecursive()
        {
            var root = BuildChain(out var a, out var b, out var c);

            var kinds = root.DescendantsAndSelf().Select(n => n.Kind).ToList();

            CollectionAssert.AreEqual(
                new[] { HlslNodeKind.CompilationUnit, HlslNodeKind.TypeName, HlslNodeKind.TypeName, HlslNodeKind.TypeName },
                kinds);
            var nodes = root.DescendantsAndSelf().ToList();
            Assert.AreSame(root, nodes[0]);
            Assert.AreSame(a, nodes[1]);
            Assert.AreSame(b, nodes[2]);
            Assert.AreSame(c, nodes[3]);
        }

        [Test]
        public void DescendantsAndSelfOnALeafYieldsJustItself()
        {
            var leaf = new ErrorNode(new TextSpan(0, 1), "x");
            CollectionAssert.AreEqual(new HlslNode[] { leaf }, leaf.DescendantsAndSelf().ToList());
        }

        [Test]
        public void FindNodeAtReturnsDeepestContainingNode()
        {
            var root = BuildChain(out _, out _, out var c);

            Assert.AreSame(c, root.FindNodeAt(22));
        }

        [Test]
        public void FindNodeAtReturnsShallowerNodeOutsideDeeperSpans()
        {
            var root = BuildChain(out var a, out _, out _);

            // 5 is inside root [0..30) and A [0..25) but not B [10..24).
            Assert.AreSame(a, root.FindNodeAt(5));
        }

        [Test]
        public void FindNodeAtRespectsHalfOpenSpanBoundary()
        {
            var root = BuildChain(out var a, out var b, out var c);

            // C ends at 23 (exclusive), B at 24, A at 25 — each boundary should fall back to the
            // next shallower node exactly at its End, not before.
            Assert.AreSame(c, root.FindNodeAt(22));
            Assert.AreSame(b, root.FindNodeAt(23));
            Assert.AreSame(a, root.FindNodeAt(24));
            Assert.AreSame(root, root.FindNodeAt(25));
        }

        [Test]
        public void FindNodeAtReturnsNullOutsideRootSpan()
        {
            var root = BuildChain(out _, out _, out _);

            Assert.IsNull(root.FindNodeAt(-1));
            Assert.IsNull(root.FindNodeAt(30));
            Assert.IsNull(root.FindNodeAt(1000));
        }

        [Test]
        public void ToStringFormatsKindAndSpan()
        {
            var node = new ErrorNode(new TextSpan(20, 5), "boom");
            Assert.AreEqual("Error [20..25)", node.ToString());
        }
    }
}