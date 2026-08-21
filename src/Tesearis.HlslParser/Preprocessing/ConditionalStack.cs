using System.Collections.Generic;
using Tesearis.HlslParser.Diagnostics;
using Tesearis.HlslParser.Text;

namespace Tesearis.HlslParser.Preprocessing
{
    internal sealed class ConditionalFrame
    {
        /// <summary>Has any branch at this level (the opening <c>#if</c>/<c>#ifdef</c>/<c>#ifndef</c>,
        /// or a later <c>#elif</c>) already fired.</summary>
        public bool BranchTaken;

        /// <summary>Whether the branch currently being scanned at this level is live.</summary>
        public bool IsCurrentBranchLive;

        /// <summary>Whether the enclosing context was live when this frame was pushed — a dead
        /// outer region suppresses every inner branch regardless of its own condition.</summary>
        public bool EnclosingLive;

        public bool SawElse;

        /// <summary>Span of the opening <c>#if</c>/<c>#ifdef</c>/<c>#ifndef</c>, used to anchor
        /// an <see cref="DiagnosticIds.UnterminatedConditional"/> diagnostic if this frame is
        /// still open at end of file.</summary>
        public TextSpan OpeningSpan;
    }

    /// <summary>
    /// Tracks nested <c>#if</c>/<c>#ifdef</c>/<c>#ifndef</c>/<c>#elif</c>/<c>#else</c>/<c>#endif</c>
    /// state for one preprocessing run.
    /// </summary>
    internal sealed class ConditionalStack
    {
        private readonly List<ConditionalFrame> _frames = new();

        /// <summary>True if nothing is currently suppressing output: the stack is empty, or every enclosing level and the current branch at every level are all live.</summary>
        public bool IsLive
        {
            get
            {
                if (_frames.Count == 0) return true;
                var top = _frames[_frames.Count - 1];
                return top.EnclosingLive && top.IsCurrentBranchLive;
            }
        }

        public bool HasUnterminated => _frames.Count > 0;

        /// <summary>True if a following <c>#elif</c>'s condition should actually be evaluated:
        /// the enclosing context is live, no branch at this level has fired yet, and an
        /// <c>#else</c> hasn't already been seen at this level. False (including an empty stack —
        /// an unbalanced <c>#elif</c>, reported separately by <see cref="ElifOrElse"/>) means the
        /// condition's tokens must be skipped wholesale without invoking the evaluator. Note this
        /// is deliberately narrower than <see cref="IsLive"/>: e.g. after <c>#if 0</c>,
        /// <see cref="IsLive"/> is false but a following <c>#elif</c> should still be evaluated,
        /// since the enclosing context is live and no branch has fired yet.</summary>
        public bool ShouldEvaluateNextBranch
        {
            get
            {
                if (_frames.Count == 0) return false;
                var top = _frames[_frames.Count - 1];
                return top.EnclosingLive && !top.BranchTaken && !top.SawElse;
            }
        }

        /// <summary>Opens a new <c>#if</c>/<c>#ifdef</c>/<c>#ifndef</c> nesting level.
        /// <paramref name="conditionResult"/> is only meaningful when the enclosing context is
        /// live — it's ANDed with the enclosing liveness either way, so passing an arbitrary
        /// value (e.g. <c>false</c>) for a condition that was deliberately left unevaluated
        /// because the enclosing region is dead is safe.</summary>
        public void PushIf(bool conditionResult, TextSpan span)
        {
            var enclosingLive = IsLive;
            _frames.Add(new ConditionalFrame
            {
                EnclosingLive = enclosingLive,
                IsCurrentBranchLive = enclosingLive && conditionResult,
                BranchTaken = enclosingLive && conditionResult,
                SawElse = false,
                OpeningSpan = span
            });
        }

        /// <summary>Handles an <c>#elif</c> (<paramref name="isElse"/> false) or <c>#else</c>
        /// (<paramref name="isElse"/> true) at the current nesting level.</summary>
        public void ElifOrElse(bool isElse, bool conditionResult, TextSpan span, DiagnosticSink diagnostics)
        {
            if (_frames.Count == 0)
            {
                diagnostics.Error(isElse ? DiagnosticIds.UnbalancedElse : DiagnosticIds.UnbalancedElif, span,
                    (isElse ? "#else" : "#elif") + " with no open #if/#ifdef/#ifndef.");
                return;
            }

            var top = _frames[_frames.Count - 1];

            if (top.SawElse)
            {
                diagnostics.Error(DiagnosticIds.ElifOrElseAfterElse, span,
                    (isElse ? "#else" : "#elif") + " after an #else at the same nesting level.");
                top.IsCurrentBranchLive = false;
                return;
            }

            if (isElse) top.SawElse = true;

            var live = top.EnclosingLive && !top.BranchTaken && (isElse || conditionResult);
            top.IsCurrentBranchLive = live;
            if (live) top.BranchTaken = true;
        }

        /// <summary>Closes the current nesting level.</summary>
        public void PopEndIf(TextSpan span, DiagnosticSink diagnostics)
        {
            if (_frames.Count == 0)
            {
                diagnostics.Error(DiagnosticIds.UnbalancedEndIf, span, "#endif with no open #if/#ifdef/#ifndef.");
                return;
            }

            _frames.RemoveAt(_frames.Count - 1);
        }

        /// <summary>Reports one <see cref="DiagnosticIds.UnterminatedConditional"/> per frame
        /// still open at end of file, anchored to each frame's opening span.</summary>
        public void ReportUnterminated(DiagnosticSink diagnostics)
        {
            foreach (var frame in _frames)
            {
                diagnostics.Error(DiagnosticIds.UnterminatedConditional, frame.OpeningSpan,
                    "Unterminated #if/#ifdef/#ifndef: no matching #endif before end of file.");
            }
        }
    }
}