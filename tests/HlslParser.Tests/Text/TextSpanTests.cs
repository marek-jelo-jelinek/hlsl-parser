using System;
using HlslParser.Text;
using NUnit.Framework;

namespace HlslParser.Tests.Text
{
    [TestFixture]
    public class TextSpanTests
    {
        [Test]
        public void ConstructorThrowsOnNegativeStart()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TextSpan(-1, 0));
        }

        [Test]
        public void ConstructorThrowsOnNegativeLength()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TextSpan(0, -1));
        }

        [Test]
        public void EndIsStartPlusLength()
        {
            var span = new TextSpan(3, 4);
            Assert.AreEqual(7, span.End);
        }

        [Test]
        public void IsEmptyWhenLengthIsZero()
        {
            Assert.IsTrue(new TextSpan(3, 0).IsEmpty);
            Assert.IsFalse(new TextSpan(3, 1).IsEmpty);
        }

        [TestCase(3, 7, 3, 4)]
        [TestCase(7, 3, 7, 0)] // end before start clamps to zero length, Start stays as given
        public void FromBoundsComputesLength(int start, int end, int expectedStart, int expectedLength)
        {
            var span = TextSpan.FromBounds(start, end);
            Assert.AreEqual(expectedStart, span.Start);
            Assert.AreEqual(expectedLength, span.Length);
        }

        [Test]
        public void UnionCoversBothSpans()
        {
            var a = new TextSpan(2, 3); // [2..5)
            var b = new TextSpan(10, 2); // [10..12)
            var union = TextSpan.Union(a, b);
            Assert.AreEqual(2, union.Start);
            Assert.AreEqual(12, union.End);
        }

        [TestCase(2, false)]
        [TestCase(3, true)]
        [TestCase(5, true)]
        [TestCase(6, false)]
        public void ContainsPosition(int position, bool expected)
        {
            var span = new TextSpan(3, 3); // [3..6)
            Assert.AreEqual(expected, span.Contains(position));
        }

        [Test]
        public void ContainsSpan()
        {
            var outer = new TextSpan(0, 10);
            Assert.IsTrue(outer.Contains(new TextSpan(2, 3)));
            Assert.IsFalse(outer.Contains(new TextSpan(8, 5)));
        }

        [TestCase(0, 5, 3, 4, true)]
        [TestCase(0, 3, 3, 4, false)]
        [TestCase(0, 5, 5, 4, false)]
        public void OverlapsWith(int aStart, int aLength, int bStart, int bLength, bool expected)
        {
            var a = new TextSpan(aStart, aLength);
            var b = new TextSpan(bStart, bLength);
            Assert.AreEqual(expected, a.OverlapsWith(b));
        }

        [Test]
        public void EqualityIsByValue()
        {
            var a = new TextSpan(1, 2);
            var b = new TextSpan(1, 2);
            var c = new TextSpan(1, 3);

            Assert.AreEqual(a, b);
            Assert.IsTrue(a == b);
            Assert.IsTrue(a != c);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void ToStringFormatsHalfOpenRange()
        {
            Assert.AreEqual("[3..7)", new TextSpan(3, 4).ToString());
        }
    }
}
