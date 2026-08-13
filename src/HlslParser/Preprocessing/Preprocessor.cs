using System;
using System.Collections.Generic;
using HlslParser.Diagnostics;
using HlslParser.Lexing;
using HlslParser.Text;

namespace HlslParser.Preprocessing
{
    /// <summary>
    /// Consumes the flat token stream produced by <see cref="Lexer.Tokenize"/> and applies
    /// <c>#define</c>/<c>#undef</c> macro expansion and <c>#if</c>/<c>#ifdef</c>/<c>#ifndef</c>/
    /// <c>#elif</c>/<c>#else</c>/<c>#endif</c> conditional compilation, producing a transformed
    /// token list (directives stripped, macros expanded, dead branches dropped) ready for a
    /// future declaration/statement parser to consume directly.
    /// </summary>
    public sealed class Preprocessor
    {
        private readonly SourceText _source;
        private readonly DiagnosticSink _diagnostics;
        private readonly MacroTable _macros = new();
        private readonly MacroExpander _expander;
        private readonly ConditionalStack _conditionals = new();
        private readonly ConstantExpressionEvaluator _evaluator;
        private readonly List<IncludeDirective> _includes = new();

        public Preprocessor(SourceText source, DiagnosticSink diagnostics)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _diagnostics = diagnostics ?? new DiagnosticSink(source);
            _expander = new MacroExpander(_source, _macros, _diagnostics);
            _evaluator = new ConstantExpressionEvaluator(_source, _macros, _expander, _diagnostics);
        }

        /// <summary><c>#include</c> directives recognized during the most recent
        /// <see cref="Process"/> call, in source order — never opened, never resolved.</summary>
        public IReadOnlyList<IncludeDirective> Includes => _includes;

        public List<Token> Process(List<Token> tokens)
        {
            if (tokens == null) throw new ArgumentNullException(nameof(tokens));

            _includes.Clear();
            var output = new List<Token>(tokens.Count);
            var index = 0;

            while (index < tokens.Count)
            {
                var token = tokens[index];

                if (token.Kind == HlslTokenKind.EndOfFile)
                {
                    output.Add(token);
                    break;
                }

                var before = index;

                if (token.Kind == HlslTokenKind.Hash && token.IsAtStartOfLine)
                {
                    ProcessDirective(tokens, ref index);
                }
                else if (_conditionals.IsLive)
                {
                    _expander.TryExpand(tokens, ref index, output);
                }
                else
                {
                    index++; // dead #if/#ifdef/#ifndef branch: drop silently, no expansion, no diagnostics
                }

                if (index == before) index++; // forward-progress guard
            }

            if (_conditionals.HasUnterminated) _conditionals.ReportUnterminated(_diagnostics);

            if (output.Count == 0 || output[output.Count - 1].Kind != HlslTokenKind.EndOfFile)
            {
                var eofSpan = new TextSpan(_source.BaseOffset + _source.Length, 0);
                output.Add(new Token { Kind = HlslTokenKind.EndOfFile, Span = eofSpan, ValueSpan = eofSpan, Source = _source });
            }

            return output;
        }

        /// <summary>Dispatches one <c>#</c>-directive. <paramref name="index"/> starts on the
        /// <c>Hash</c> token and is advanced past the whole directive line (including any
        /// following-line continuation already spliced away by the lexer).</summary>
        private void ProcessDirective(List<Token> tokens, ref int index)
        {
            var hashToken = tokens[index];
            index++;

            // a bare '#' alone on its line is a legal no-op
            if (index >= tokens.Count || tokens[index].Kind == HlslTokenKind.EndOfFile || tokens[index].IsAtStartOfLine) return;

            var keywordToken = tokens[index];
            index++;

            var bodyStart = index;
            var lineEnd = SkipToLineEnd(tokens, index);

            // The keyword can lex as Keyword rather than Identifier ("if"/"else" are ordinary
            // HLSL keywords too), since the lexer has no idea it's on a directive line.
            var keyword = keywordToken.Kind is HlslTokenKind.Identifier or HlslTokenKind.Keyword ? keywordToken.Text : null;

            switch (keyword)
            {
                case "define": ProcessDefine(tokens, bodyStart, lineEnd, hashToken); break;
                case "undef": ProcessUndef(tokens, bodyStart, lineEnd, hashToken); break;
                case "if": ProcessIf(tokens, bodyStart, lineEnd, hashToken); break;
                case "ifdef": ProcessIfdefCore(tokens, bodyStart, lineEnd, hashToken, false); break;
                case "ifndef": ProcessIfdefCore(tokens, bodyStart, lineEnd, hashToken, true); break;
                case "elif": ProcessElif(tokens, bodyStart, lineEnd, hashToken); break;
                case "else": ProcessElse(hashToken); break;
                case "endif": ProcessEndif(hashToken); break;
                case "include": ProcessInclude(tokens, bodyStart, lineEnd, hashToken); break;
                default: ProcessUnknownDirective(hashToken, keywordToken); break;
            }

            index = lineEnd;
        }

        /// <summary>Finds the end of the current directive's token run: everything up to (not
        /// including) the next token that starts a new physical/logical line, or end of file.
        /// Shared by every directive handler above.</summary>
        private static int SkipToLineEnd(List<Token> tokens, int start)
        {
            var i = start;
            while (i < tokens.Count && tokens[i].Kind != HlslTokenKind.EndOfFile && !tokens[i].IsAtStartOfLine) i++;
            return i;
        }

        private static List<Token> Slice(List<Token> tokens, int start, int end)
        {
            var result = new List<Token>(end - start);
            for (var i = start; i < end; i++) result.Add(tokens[i]);
            return result;
        }

        private static TextSpan DirectiveSpan(Token hashToken, List<Token> tokens, int bodyStart, int bodyEnd)
        {
            var end = bodyEnd > bodyStart ? tokens[bodyEnd - 1].Span.End : hashToken.Span.End;
            return TextSpan.FromBounds(hashToken.Span.Start, end);
        }

        private void ProcessDefine(List<Token> tokens, int bodyStart, int bodyEnd, Token hashToken)
        {
            if (!_conditionals.IsLive) return;

            var i = bodyStart;
            if (i >= bodyEnd || tokens[i].Kind != HlslTokenKind.Identifier)
            {
                _diagnostics.Error(DiagnosticIds.MalformedMacroDefinition, hashToken.Span, "#define must be followed by a macro name.");
                return;
            }

            var nameToken = tokens[i];
            var name = nameToken.Text;
            i++;

            // Function-like only if '(' is strictly adjacent to the name — any gap (even one
            // space) makes this an object-like macro whose replacement text happens to start
            // with '(...)'.
            var isFunctionLike = i < bodyEnd && tokens[i].Kind == HlslTokenKind.OpenParen && tokens[i].Span.Start == nameToken.Span.End;

            var parameters = new List<string>();
            if (isFunctionLike)
            {
                i++;
                if (i < bodyEnd && tokens[i].Kind == HlslTokenKind.CloseParen)
                {
                    i++; // zero-parameter macro: "NAME()"
                }
                else
                {
                    while (true)
                    {
                        if (i >= bodyEnd || tokens[i].Kind != HlslTokenKind.Identifier)
                        {
                            // Covers a missing parameter name and an attempted variadic '...'
                            // parameter alike — variadic macros are out of scope for this phase.
                            _diagnostics.Error(DiagnosticIds.MalformedMacroDefinition, hashToken.Span,
                                "Malformed parameter list in #define '" + name + "'.");
                            return;
                        }

                        parameters.Add(tokens[i].Text);
                        i++;

                        if (i < bodyEnd && tokens[i].Kind == HlslTokenKind.Comma)
                        {
                            i++;
                            continue;
                        }

                        if (i < bodyEnd && tokens[i].Kind == HlslTokenKind.CloseParen)
                        {
                            i++;
                            break;
                        }

                        _diagnostics.Error(DiagnosticIds.MalformedMacroDefinition, hashToken.Span,
                            "Malformed parameter list in #define '" + name + "' — expected ',' or ')'.");
                        return;
                    }
                }
            }

            var replacementTokens = Slice(tokens, i, bodyEnd);
            var definitionSpan = DirectiveSpan(hashToken, tokens, bodyStart, bodyEnd);
            var macro = new MacroDefinition(name, isFunctionLike ? MacroKind.FunctionLike : MacroKind.ObjectLike, parameters, replacementTokens,
                definitionSpan);

            MacroExpander.ValidateReplacementList(macro, _diagnostics);
            _macros.Define(macro, _diagnostics);
        }

        private void ProcessUndef(List<Token> tokens, int bodyStart, int bodyEnd, Token hashToken)
        {
            if (!_conditionals.IsLive) return;

            if (bodyStart >= bodyEnd || tokens[bodyStart].Kind != HlslTokenKind.Identifier)
            {
                _diagnostics.Error(DiagnosticIds.MalformedUndefDirective, hashToken.Span, "#undef must be followed by an identifier.");
                return;
            }

            _macros.Undefine(tokens[bodyStart].Text); // trailing tokens ignored leniently
        }

        private void ProcessIf(List<Token> tokens, int bodyStart, int bodyEnd, Token hashToken)
        {
            var enclosingLive = _conditionals.IsLive;
            var result = false;
            if (enclosingLive)
            {
                var conditionSpan = DirectiveSpan(hashToken, tokens, bodyStart, bodyEnd);
                result = _evaluator.Evaluate(Slice(tokens, bodyStart, bodyEnd), conditionSpan);
            }

            _conditionals.PushIf(result, hashToken.Span);
        }

        private void ProcessIfdefCore(List<Token> tokens, int bodyStart, int bodyEnd, Token hashToken, bool negate)
        {
            var enclosingLive = _conditionals.IsLive;
            var result = false;
            if (enclosingLive)
            {
                if (bodyStart < bodyEnd && tokens[bodyStart].Kind == HlslTokenKind.Identifier)
                {
                    var defined = _macros.IsDefined(tokens[bodyStart].Text);
                    result = negate ? !defined : defined; // trailing tokens ignored leniently
                }
                else
                {
                    _diagnostics.Error(DiagnosticIds.MalformedConditionalDirective, hashToken.Span,
                        (negate ? "#ifndef" : "#ifdef") + " must be followed by an identifier.");
                }
            }

            _conditionals.PushIf(result, hashToken.Span);
        }

        private void ProcessElif(List<Token> tokens, int bodyStart, int bodyEnd, Token hashToken)
        {
            var shouldEvaluate = _conditionals.ShouldEvaluateNextBranch;
            var result = false;
            if (shouldEvaluate)
            {
                var conditionSpan = DirectiveSpan(hashToken, tokens, bodyStart, bodyEnd);
                result = _evaluator.Evaluate(Slice(tokens, bodyStart, bodyEnd), conditionSpan);
            }

            _conditionals.ElifOrElse(false, result, hashToken.Span, _diagnostics);
        }

        private void ProcessElse(Token hashToken)
        {
            _conditionals.ElifOrElse(true, false, hashToken.Span, _diagnostics);
        }

        private void ProcessEndif(Token hashToken)
        {
            _conditionals.PopEndIf(hashToken.Span, _diagnostics);
        }

        private void ProcessInclude(List<Token> tokens, int bodyStart, int bodyEnd, Token hashToken)
        {
            if (!_conditionals.IsLive) return;

            if (bodyStart >= bodyEnd)
            {
                _diagnostics.Error(DiagnosticIds.MalformedInclude, hashToken.Span, "#include with no path.");
                return;
            }

            var first = tokens[bodyStart];

            if (first.Kind == HlslTokenKind.StringLiteral)
            {
                var directiveSpan = DirectiveSpan(hashToken, tokens, bodyStart, bodyEnd);
                _includes.Add(new IncludeDirective(first.Value, IncludeKind.Quoted, directiveSpan, first.ValueSpan));
                return;
            }

            if (first.Kind == HlslTokenKind.LessThan)
            {
                for (var i = bodyStart + 1; i < bodyEnd; i++)
                {
                    if (tokens[i].Kind != HlslTokenKind.GreaterThan) continue;

                    var pathSpan = TextSpan.FromBounds(first.Span.End, tokens[i].Span.Start);
                    var path = _source.GetText(pathSpan);
                    var directiveSpan = DirectiveSpan(hashToken, tokens, bodyStart, bodyEnd);
                    _includes.Add(new IncludeDirective(path, IncludeKind.AngleBracketed, directiveSpan, pathSpan));
                    return;
                }

                _diagnostics.Error(DiagnosticIds.MalformedInclude, hashToken.Span, "#include<...> is missing a closing '>'.");
                return;
            }

            _diagnostics.Error(DiagnosticIds.MalformedInclude, hashToken.Span,
                "#include must be followed by a quoted \"path\" or an angle-bracketed <path>.");
        }

        private void ProcessUnknownDirective(Token hashToken, Token keywordToken)
        {
            if (!_conditionals.IsLive) return; // no noise for junk inside dead code

            _diagnostics.Info(DiagnosticIds.UnknownPreprocessorDirective, hashToken.Span,
                "Unrecognized preprocessor directive '#" + keywordToken.Text + "'.");
        }
    }
}