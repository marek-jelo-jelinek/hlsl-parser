using System;
using Tesearis.HlslParser.Diagnostics;
using Tesearis.HlslParser.Text;
using NUnit.Framework;

namespace Tesearis.HlslParser.Tests.Diagnostics
{
    [TestFixture]
    public class DiagnosticSinkTests
    {
        [Test]
        public void ConstructorThrowsOnNullSource()
        {
            Assert.Throws<ArgumentNullException>(() => new DiagnosticSink(null));
        }

        [Test]
        public void ReportErrorAppendsAndDoesNotThrow()
        {
            var source = new SourceText("abc", "f.hlsl");
            var sink = new DiagnosticSink(source);

            Assert.DoesNotThrow(() =>
                sink.Report(DiagnosticSeverity.Error, "HL9999", new TextSpan(0, 1), "boom"));

            Assert.AreEqual(1, sink.Diagnostics.Count);
            Assert.AreEqual(DiagnosticSeverity.Error, sink.Diagnostics[0].Severity);
            Assert.IsTrue(sink.HasErrors);
        }

        [Test]
        public void WarningAndInfoConvenienceMethodsAppend()
        {
            var source = new SourceText("abc", "f.hlsl");
            var sink = new DiagnosticSink(source);

            sink.Warning("HL0002", new TextSpan(0, 1), "warn");
            sink.Info("HL0000", new TextSpan(0, 1), "info");

            Assert.AreEqual(2, sink.Diagnostics.Count);
            Assert.IsFalse(sink.HasErrors);
        }

        [Test]
        public void DiagnosticToStringFormatsUnityConsoleStyle()
        {
            var source = new SourceText("line1\nline2", "f.hlsl");
            var sink = new DiagnosticSink(source);
            sink.Error("HL0001", new TextSpan(6, 1), "bad token");

            Assert.AreEqual("f.hlsl(2,1): error HL0001: bad token", sink.Diagnostics[0].ToString());
        }

        [Test]
        public void DiagnosticPositionReflectsNonZeroBaseOffset()
        {
            var source = new SourceText("line1\nline2", "f.hlsl", baseOffset: 100);
            var sink = new DiagnosticSink(source);
            sink.Error("HL0001", new TextSpan(106, 1), "bad token");

            Assert.AreEqual(new LinePosition(2, 1), sink.Diagnostics[0].Position);
        }
    }
}
