using System;

namespace Tesearis.HlslParser.Text
{
    /// <summary>
    /// A half-open character range <c>[Start, End)</c> within a <see cref="SourceText"/>.
    /// </summary>
    public readonly struct TextSpan : IEquatable<TextSpan>
    {
        public TextSpan(int start, int length)
        {
            if (start < 0) throw new ArgumentOutOfRangeException(nameof(start));
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            Start = start;
            Length = length;
        }

        public int Start { get; }
        public int Length { get; }
        public int End => Start + Length;
        public bool IsEmpty => Length == 0;

        /// <summary>Builds a span from an inclusive start and exclusive end. <paramref name="end"/>
        /// less than <paramref name="start"/> clamps to a zero-length span at <paramref name="start"/>
        /// rather than throwing.</summary>
        public static TextSpan FromBounds(int start, int end)
        {
            return new TextSpan(start, Math.Max(0, end - start));
        }

        /// <summary>The smallest span covering both inputs, regardless of whether they overlap or
        /// touch.</summary>
        public static TextSpan Union(TextSpan a, TextSpan b)
        {
            return FromBounds(Math.Min(a.Start, b.Start), Math.Max(a.End, b.End));
        }

        /// <summary>True for <c>Start &lt;= position &lt; End</c> — <see cref="End"/> itself is
        /// outside the span, consistent with the half-open range.</summary>
        public bool Contains(int position)
        {
            return position >= Start && position < End;
        }

        /// <summary>True when <paramref name="span"/> lies entirely within this span, endpoints
        /// included (an equal span contains itself).</summary>
        public bool Contains(TextSpan span)
        {
            return span.Start >= Start && span.End <= End;
        }

        /// <summary>True when the two spans share at least one position; touching-but-not-
        /// overlapping spans (e.g. <c>[0,2)</c> and <c>[2,4)</c>) are not overlapping.</summary>
        public bool OverlapsWith(TextSpan span)
        {
            return Math.Max(Start, span.Start) < Math.Min(End, span.End);
        }

        public bool Equals(TextSpan other)
        {
            return Start == other.Start && Length == other.Length;
        }

        public override bool Equals(object obj)
        {
            return obj is TextSpan other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return Start * 397 ^ Length;
            }
        }

        public static bool operator ==(TextSpan left, TextSpan right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TextSpan left, TextSpan right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return "[" + Start + ".." + End + ")";
        }
    }
}