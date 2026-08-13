using System;
using System.Collections.Generic;

namespace HlslParser.Text
{
    public readonly struct LinePosition : IEquatable<LinePosition>
    {
        public LinePosition(int line, int column)
        {
            Line = line;
            Column = column;
        }

        public int Line { get; }
        public int Column { get; }

        public bool Equals(LinePosition other)
        {
            return Line == other.Line && Column == other.Column;
        }

        public override bool Equals(object obj)
        {
            return obj is LinePosition other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return Line * 397 ^ Column;
            }
        }

        public static bool operator ==(LinePosition left, LinePosition right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(LinePosition left, LinePosition right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return Line + "," + Column;
        }
    }

    public sealed class SourceText
    {
        private readonly int[] _lineStarts;

        public SourceText(string text, string fileName = null, int baseOffset = 0, int baseLine = 0)
        {
            if (baseOffset < 0) throw new ArgumentOutOfRangeException(nameof(baseOffset));
            if (baseLine < 0) throw new ArgumentOutOfRangeException(nameof(baseLine));
            Text = text ?? string.Empty;
            FileName = fileName ?? "<unknown>";
            BaseOffset = baseOffset;
            BaseLine = baseLine;
            _lineStarts = ComputeLineStarts(Text);
        }

        public string Text { get; }
        public string FileName { get; }

        /// <summary>Absolute offset of <c>Text[0]</c> in the outer file; 0 for a standalone file.</summary>
        public int BaseOffset { get; }

        /// <summary>Zero-based line index of <c>Text[0]</c> in the outer file; 0 for a standalone
        /// file. See <see cref="BaseOffset"/>.</summary>
        public int BaseLine { get; }

        public int Length => Text.Length;

        /// <summary>Number of lines in <see cref="Text"/> itself (local count — not affected by
        /// <see cref="BaseLine"/>).</summary>
        public int LineCount => _lineStarts.Length;

        /// <summary>Indexes <see cref="Text"/> directly with a LOCAL index (not offset by <see cref="BaseOffset"/>).</summary>
        public char this[int localIndex] => Text[localIndex];

        private static int[] ComputeLineStarts(string text)
        {
            var starts = new List<int>(Math.Max(8, text.Length / 32)) { 0 };
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                    starts.Add(i + 1);
                }
                else if (c == '\n')
                {
                    starts.Add(i + 1);
                }
            }

            return starts.ToArray();
        }

        private int ToLocal(int absolutePosition)
        {
            var local = absolutePosition - BaseOffset;
            if (local < 0) return 0;
            if (local > Text.Length) return Text.Length;
            return local;
        }

        private int GetLocalLineIndex(int position)
        {
            var local = ToLocal(position);
            var index = Array.BinarySearch(_lineStarts, local);
            return index >= 0 ? index : ~index - 1;
        }

        /// <summary>Zero-based line index for an ABSOLUTE offset, in the outer file's line
        /// numbering (i.e. already includes <see cref="BaseLine"/>).</summary>
        public int GetLineIndex(int position)
        {
            return GetLocalLineIndex(position) + BaseLine;
        }

        /// <summary>1-based line and column for an ABSOLUTE offset, in the outer file's line
        /// numbering.</summary>
        public LinePosition GetLinePosition(int position)
        {
            var localLineIndex = GetLocalLineIndex(position);
            var local = ToLocal(position);
            var column = local - _lineStarts[localLineIndex];
            return new LinePosition(localLineIndex + BaseLine + 1, column + 1);
        }

        /// <summary>ABSOLUTE offset of the start of the given zero-based line (in the outer file's
        /// line numbering — i.e. already includes <see cref="BaseLine"/>).</summary>
        public int GetLineStart(int lineIndex)
        {
            var localLineIndex = lineIndex - BaseLine;
            if (localLineIndex < 0) localLineIndex = 0;
            if (localLineIndex >= _lineStarts.Length) localLineIndex = _lineStarts.Length - 1;
            return BaseOffset + _lineStarts[localLineIndex];
        }

        /// <summary>Text of the given zero-based line (in the outer file's line numbering),
        /// without a trailing line terminator.</summary>
        public string GetLineText(int lineIndex)
        {
            var localLineIndex = lineIndex - BaseLine;
            if (localLineIndex < 0 || localLineIndex >= _lineStarts.Length) return string.Empty;
            var start = _lineStarts[localLineIndex];
            var end = localLineIndex + 1 < _lineStarts.Length ? _lineStarts[localLineIndex + 1] : Text.Length;
            while (end > start && (Text[end - 1] == '\n' || Text[end - 1] == '\r')) end--;
            return Text.Substring(start, end - start);
        }

        /// <summary>Substring covered by an ABSOLUTE span, clamped to the bounds of <see cref="Text"/>.</summary>
        public string GetText(TextSpan span)
        {
            var start = ToLocal(span.Start);
            var end = ToLocal(span.End);
            if (end < start) end = start;
            return Text.Substring(start, end - start);
        }

        public override string ToString()
        {
            return FileName;
        }
    }
}