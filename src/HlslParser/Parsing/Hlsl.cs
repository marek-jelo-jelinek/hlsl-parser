using System;
using System.Collections.Generic;
using HlslParser.Diagnostics;
using HlslParser.Lexing;
using HlslParser.Preprocessing;
using HlslParser.Syntax;
using HlslParser.Text;

namespace HlslParser.Parsing
{
    /// <summary>The result of parsing an HLSL/Cg source: a never-null tree plus every diagnostic
    /// collected while lexing, preprocessing, and parsing it.</summary>
    public sealed class HlslParseResult
    {
        public HlslParseResult(SourceText source, HlslNode root, IReadOnlyList<Token> tokens, IReadOnlyList<Diagnostic> diagnostics,
            IReadOnlyList<IncludeDirective> includes)
        {
            Source = source;
            Root = root;
            Tokens = tokens;
            Diagnostics = diagnostics;
            Includes = includes;
        }

        public SourceText Source { get; }

        /// <summary>The parsed <see cref="CompilationUnitNode"/>. Never null, even for empty or
        /// entirely-garbage input — malformed content still yields a partial tree.</summary>
        public HlslNode Root { get; }

        /// <summary>The post-preprocessing token stream actually parsed (directives stripped,
        /// macros expanded, dead conditional branches dropped).</summary>
        public IReadOnlyList<Token> Tokens { get; }

        /// <summary>The union of every diagnostic from the lexer, the preprocessor, and the
        /// parser — all three stages share one <see cref="DiagnosticSink"/>.</summary>
        public IReadOnlyList<Diagnostic> Diagnostics { get; }

        /// <summary>Pass-through of <see cref="Preprocessing.Preprocessor.Includes"/>.</summary>
        public IReadOnlyList<IncludeDirective> Includes { get; }

        /// <summary><c>#pragma</c> directives preserved on <see cref="Root"/>.</summary>
        public IReadOnlyList<PragmaDirectiveNode> Pragmas => Root is CompilationUnitNode unit ? unit.Pragmas : Array.Empty<PragmaDirectiveNode>();

        public bool HasErrors
        {
            get
            {
                foreach (var diagnostic in Diagnostics)
                {
                    if (diagnostic.Severity == DiagnosticSeverity.Error) return true;
                }

                return false;
            }
        }
    }

    /// <summary>Public entry point for parsing HLSL/Cg source.</summary>
    public static class Hlsl
    {
        /// <summary>Parses standalone HLSL/Cg source text (a whole <c>.hlsl</c>/<c>.cginc</c>/
        /// <c>.compute</c> file). A null <paramref name="text"/> is treated as empty.</summary>
        public static HlslParseResult Parse(string text, string fileName = null)
        {
            return Parse(new SourceText(text ?? string.Empty, fileName));
        }

        /// <summary>
        /// Runs <paramref name="source"/> through the full pipeline: <see cref="Lexer"/> →
        /// <see cref="Preprocessor"/> → <see cref="Parser"/>, all three sharing one
        /// <see cref="DiagnosticSink"/>. The preprocessor always runs unconditionally — real
        /// source can be saturated with macros even in isolated files.
        /// </summary>
        public static HlslParseResult Parse(SourceText source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var diagnostics = new DiagnosticSink(source);
            var tokens = new Lexer(source, diagnostics).Tokenize();

            var preprocessor = new Preprocessor(source, diagnostics);
            var processed = preprocessor.Process(tokens);

            var parser = new Parser(source, processed, diagnostics);
            var root = parser.ParseCompilationUnit(preprocessor.Pragmas);

            return new HlslParseResult(source, root, processed.AsReadOnly(), diagnostics.Diagnostics, preprocessor.Includes);
        }

        /// <summary>
        /// Parses HLSL/Cg source embedded inside another file (e.g. a ShaderLab
        /// <c>ProgramBlockNode.Body</c>), so diagnostics and node spans/line-columns report
        /// positions in the original outer file rather than local 0-based coordinates.
        /// </summary>
        /// <param name="body"></param>
        /// <param name="baseOffset">Absolute character offset of <paramref name="body"/>[0] in the
        /// outer file, e.g. <c>block.BodySpan.Start</c>.</param>
        /// <param name="fileName"></param>
        /// <param name="baseLine">Zero-based line index of <paramref name="body"/>[0] in the outer
        /// file, e.g. <c>outerSource.GetLineIndex(block.BodySpan.Start)</c>. Required alongside
        /// <paramref name="baseOffset"/> for correct line numbers — see <see cref="SourceText.BaseLine"/>.</param>
        public static HlslParseResult ParseEmbedded(string body, int baseOffset, string fileName, int baseLine = 0)
        {
            return Parse(new SourceText(body ?? string.Empty, fileName, baseOffset, baseLine));
        }
    }
}