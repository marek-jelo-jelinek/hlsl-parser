using System;
using System.Collections.Generic;
using HlslParser.Text;

namespace HlslParser.Diagnostics
{
    /// <summary>
    /// Where every diagnostic produced while lexing/parsing flows through.
    /// </summary>
    public sealed class DiagnosticSink
    {
        private readonly List<Diagnostic> _diagnostics = new();

        public DiagnosticSink(SourceText source)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public SourceText Source { get; }

        public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

        public bool HasErrors
        {
            get
            {
                foreach (var diagnostic in _diagnostics)
                {
                    if (diagnostic.Severity == DiagnosticSeverity.Error) return true;
                }

                return false;
            }
        }

        public void Report(DiagnosticSeverity severity, string id, TextSpan span, string message)
        {
            _diagnostics.Add(new Diagnostic(severity, id, message, span, Source));
        }

        public void Error(string id, TextSpan span, string message)
        {
            Report(DiagnosticSeverity.Error, id, span, message);
        }

        public void Warning(string id, TextSpan span, string message)
        {
            Report(DiagnosticSeverity.Warning, id, span, message);
        }

        public void Info(string id, TextSpan span, string message)
        {
            Report(DiagnosticSeverity.Info, id, span, message);
        }
    }
}