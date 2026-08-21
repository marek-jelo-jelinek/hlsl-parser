using System;
using Tesearis.HlslParser.Text;
using NUnit.Framework;

namespace Tesearis.HlslParser.Tests.Text
{
    [TestFixture]
    public class SourceTextTests
    {
        // "line1\nline2\r\nline3" — local line starts at 0, 6, 13; length 18.
        private const string Fixture = "line1\nline2\r\nline3";

        [Test]
        public void NullTextAndFileNameDefault()
        {
            var source = new SourceText(null, null);
            Assert.AreEqual(string.Empty, source.Text);
            Assert.AreEqual("<unknown>", source.FileName);
        }

        [Test]
        public void BaseOffsetDefaultsToZero()
        {
            var source = new SourceText("abc", "f.hlsl");
            Assert.AreEqual(0, source.BaseOffset);
        }

        [Test]
        public void ConstructorThrowsOnNegativeBaseOffset()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SourceText("abc", "f.hlsl", -1));
        }

        [Test]
        public void BaseLineDefaultsToZero()
        {
            var source = new SourceText("abc", "f.hlsl");
            Assert.AreEqual(0, source.BaseLine);
        }

        [Test]
        public void ConstructorThrowsOnNegativeBaseLine()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SourceText("abc", "f.hlsl", 0, -1));
        }

        [TestCase(0)]
        [TestCase(500)]
        public void LinePositionAndLineStartAgreeUnderBaseOffset(int baseOffset)
        {
            var source = new SourceText(Fixture, "f.hlsl", baseOffset);

            Assert.AreEqual(3, source.LineCount);

            Assert.AreEqual(baseOffset + 0, source.GetLineStart(0));
            Assert.AreEqual(baseOffset + 6, source.GetLineStart(1));
            Assert.AreEqual(baseOffset + 13, source.GetLineStart(2));

            Assert.AreEqual(new LinePosition(1, 1), source.GetLinePosition(baseOffset + 0));
            Assert.AreEqual(new LinePosition(2, 1), source.GetLinePosition(baseOffset + 6));
            Assert.AreEqual(new LinePosition(3, 1), source.GetLinePosition(baseOffset + 13));
            Assert.AreEqual(new LinePosition(2, 5), source.GetLinePosition(baseOffset + 10)); // '2' in "line2"

            Assert.AreEqual(0, source.GetLineIndex(baseOffset + 0));
            Assert.AreEqual(1, source.GetLineIndex(baseOffset + 10));
            Assert.AreEqual(2, source.GetLineIndex(baseOffset + 17));
        }

        [TestCase(0)]
        [TestCase(6)]
        public void LinePositionAndLineStartAgreeUnderBaseLine(int baseLine)
        {
            const int baseOffset = 500;
            var source = new SourceText(Fixture, "f.hlsl", baseOffset, baseLine);

            Assert.AreEqual(3, source.LineCount); // local count — unaffected by BaseLine

            Assert.AreEqual(baseOffset + 0, source.GetLineStart(baseLine + 0));
            Assert.AreEqual(baseOffset + 6, source.GetLineStart(baseLine + 1));
            Assert.AreEqual(baseOffset + 13, source.GetLineStart(baseLine + 2));

            Assert.AreEqual(new LinePosition(baseLine + 1, 1), source.GetLinePosition(baseOffset + 0));
            Assert.AreEqual(new LinePosition(baseLine + 2, 1), source.GetLinePosition(baseOffset + 6));
            Assert.AreEqual(new LinePosition(baseLine + 3, 1), source.GetLinePosition(baseOffset + 13));
            Assert.AreEqual(new LinePosition(baseLine + 2, 5), source.GetLinePosition(baseOffset + 10)); // '2' in "line2"

            Assert.AreEqual(baseLine + 0, source.GetLineIndex(baseOffset + 0));
            Assert.AreEqual(baseLine + 1, source.GetLineIndex(baseOffset + 10));
            Assert.AreEqual(baseLine + 2, source.GetLineIndex(baseOffset + 17));

            Assert.AreEqual("line1", source.GetLineText(baseLine + 0));
            Assert.AreEqual("line2", source.GetLineText(baseLine + 1));
            Assert.AreEqual("line3", source.GetLineText(baseLine + 2));
        }

        [TestCase(0)]
        [TestCase(500)]
        public void GetTextRoundTripsUnderBaseOffset(int baseOffset)
        {
            var source = new SourceText(Fixture, "f.hlsl", baseOffset);
            var span = TextSpan.FromBounds(baseOffset + 6, baseOffset + 11);
            Assert.AreEqual("line2", source.GetText(span));
        }

        [Test]
        public void GetTextClampsSpanPastLength()
        {
            var source = new SourceText(Fixture, "f.hlsl");
            var span = TextSpan.FromBounds(15, 1000);
            Assert.AreEqual("ne3", source.GetText(span));
        }

        [Test]
        public void GetLineTextTrimsLineTerminators()
        {
            var source = new SourceText(Fixture, "f.hlsl");
            Assert.AreEqual("line1", source.GetLineText(0));
            Assert.AreEqual("line2", source.GetLineText(1));
            Assert.AreEqual("line3", source.GetLineText(2));
        }

        [Test]
        public void CarriageReturnLineFeedIsOneLineBreak()
        {
            var source = new SourceText("a\r\nb", "f.hlsl");
            Assert.AreEqual(2, source.LineCount);
            Assert.AreEqual(new LinePosition(2, 1), source.GetLinePosition(3));
        }

        [Test]
        public void ToStringReturnsFileName()
        {
            var source = new SourceText("abc", "f.hlsl");
            Assert.AreEqual("f.hlsl", source.ToString());
        }
    }
}
