using System.Collections.Generic;
using System.Text;
using HlslParser.Diagnostics;
using HlslParser.Lexing;
using HlslParser.Text;

namespace HlslParser.Preprocessing
{
    /// <summary>
    /// Expands macro invocations in a token list: object-like and function-like substitution,
    /// <c>#</c> stringize, <c>##</c> token-paste, and recursive re-expansion of the result — with
    /// a hide-set recursion guard so self-referential and mutually-recursive macros terminate.
    /// </summary>
    internal sealed class MacroExpander
    {
        private const int MaxExpansionDepth = 64;

        private readonly SourceText _source;
        private readonly MacroTable _macros;
        private readonly DiagnosticSink _diagnostics;

        /// <summary>Names currently being expanded, scoped to one top-level
        /// <see cref="ExpandLine"/>/<see cref="TryExpand"/> call tree — every macro added here is
        /// removed again before that call returns, so this never leaks across unrelated later
        /// expansions of the same name elsewhere in the file.</summary>
        private readonly HashSet<string> _expandingMacros = new();

        public MacroExpander(SourceText source, MacroTable macros, DiagnosticSink diagnostics)
        {
            _source = source;
            _macros = macros;
            _diagnostics = diagnostics;
        }

        /// <summary>Fully macro-expands a whole token list (a <c>#if</c>/<c>#elif</c> condition,
        /// or a macro argument), returning the expanded tokens.</summary>
        public List<Token> ExpandLine(IReadOnlyList<Token> tokens)
        {
            var output = new List<Token>(tokens.Count);
            var index = 0;
            while (index < tokens.Count) ExpandInto(tokens, ref index, output);
            return output;
        }

        /// <summary>Attempts to expand the macro invocation (if any) starting at
        /// <paramref name="tokens"/>[<paramref name="index"/>], appending resulting tokens to
        /// <paramref name="output"/> and advancing <paramref name="index"/> past whatever was
        /// consumed. Used by <see cref="Preprocessor"/>'s main loop over ordinary code. Returns
        /// false (and advances by exactly one token) when the token at <paramref name="index"/>
        /// isn't a macro invocation.</summary>
        public bool TryExpand(IReadOnlyList<Token> tokens, ref int index, List<Token> output)
        {
            return ExpandInto(tokens, ref index, output);
        }

        /// <summary>Checks a newly-parsed macro's replacement list for structural <c>#</c>/<c>##</c>
        /// misuse — called once by <see cref="Preprocessor.ProcessDefine"/> per <c>#define</c>,
        /// not on every expansion, since these are properties of the definition itself, not of
        /// any particular call site.</summary>
        internal static void ValidateReplacementList(MacroDefinition macro, DiagnosticSink diagnostics)
        {
            var tokens = macro.ReplacementTokens;
            for (var i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];

                if (token.Kind == HlslTokenKind.Hash)
                {
                    var followedByParameter = macro.Kind == MacroKind.FunctionLike && i + 1 < tokens.Count &&
                                              TryGetParameterIndex(macro, tokens[i + 1].Text, out _);
                    if (!followedByParameter)
                    {
                        diagnostics.Error(DiagnosticIds.MalformedStringizeOperator, token.Span,
                            "'#' in a macro replacement list must be immediately followed by a parameter name in a function-like macro.");
                    }
                }
                else if (token.Kind == HlslTokenKind.HashHash)
                {
                    if (i == 0 || i == tokens.Count - 1)
                    {
                        diagnostics.Error(DiagnosticIds.MalformedTokenPaste, token.Span,
                            "'##' cannot appear at the start or end of a macro replacement list.");
                    }
                }
            }
        }

        private bool ExpandInto(IReadOnlyList<Token> tokens, ref int index, List<Token> output)
        {
            var token = tokens[index];

            if (token.Kind != HlslTokenKind.Identifier || !_macros.TryGet(token.Text, out var macro) || _expandingMacros.Contains(macro.Name))
            {
                output.Add(token);
                index++;
                return false;
            }

            if (macro.Kind == MacroKind.ObjectLike)
            {
                index++;
                ExpandMacro(macro, null, token, output);
                return true;
            }

            // Function-like: only an invocation if the very next token is '(' — any gap
            // (even one space) leaves the name as an ordinary, unexpanded identifier.
            if (index + 1 >= tokens.Count || tokens[index + 1].Kind != HlslTokenKind.OpenParen)
            {
                output.Add(token);
                index++;
                return false;
            }

            if (!TrySplitArguments(tokens, index + 2, out var arguments, out var afterCloseParenIndex))
            {
                _diagnostics.Error(DiagnosticIds.UnterminatedMacroInvocation, token.Span,
                    "Unterminated invocation of function-like macro '" + macro.Name + "' — missing ')'.");
                output.Add(token);
                index++;
                return false;
            }

            if (!ArgumentCountMatches(macro, arguments))
            {
                _diagnostics.Error(DiagnosticIds.MacroArgumentCountMismatch, token.Span,
                    "Macro '" + macro.Name + "' expects " + macro.Parameters.Count + " argument(s), got " + arguments.Count + ".");
                output.Add(token);
                index++;
                return false;
            }

            index = afterCloseParenIndex;
            ExpandMacro(macro, arguments, token, output);
            return true;
        }

        /// <summary>Splits the argument list starting just after a function-like invocation's
        /// '(' (<paramref name="startIndex"/>), respecting nested parens, up to and including the
        /// matching ')'. Returns false if the end of <paramref name="tokens"/> is reached first
        /// (unterminated invocation).</summary>
        private static bool TrySplitArguments(IReadOnlyList<Token> tokens, int startIndex, out List<List<Token>> arguments,
            out int afterCloseParenIndex)
        {
            arguments = new List<List<Token>>();
            var current = new List<Token>();
            var depth = 1;
            var i = startIndex;

            while (i < tokens.Count)
            {
                var token = tokens[i];

                if (token.Kind == HlslTokenKind.EndOfFile)
                {
                    afterCloseParenIndex = i;
                    return false;
                }

                if (token.Kind == HlslTokenKind.OpenParen)
                {
                    depth++;
                    current.Add(token);
                    i++;
                    continue;
                }

                if (token.Kind == HlslTokenKind.CloseParen)
                {
                    depth--;
                    if (depth == 0)
                    {
                        arguments.Add(current);
                        afterCloseParenIndex = i + 1;
                        return true;
                    }

                    current.Add(token);
                    i++;
                    continue;
                }

                if (token.Kind == HlslTokenKind.Comma && depth == 1)
                {
                    arguments.Add(current);
                    current = new List<Token>();
                    i++;
                    continue;
                }

                current.Add(token);
                i++;
            }

            afterCloseParenIndex = i;
            return false;
        }

        /// <summary>A zero-parameter macro invoked as <c>FOO()</c> is zero arguments, not a
        /// one-empty-argument mismatch — the one special case in an otherwise exact count
        /// comparison.</summary>
        private static bool ArgumentCountMatches(MacroDefinition macro, List<List<Token>> arguments)
        {
            if (macro.Parameters.Count == 0 && arguments.Count == 1 && arguments[0].Count == 0) return true;
            return arguments.Count == macro.Parameters.Count;
        }

        private void ExpandMacro(MacroDefinition macro, List<List<Token>> arguments, Token invocationToken, List<Token> output)
        {
            if (_expandingMacros.Count >= MaxExpansionDepth)
            {
                _diagnostics.Warning(DiagnosticIds.RecursiveMacroExpansionLimitExceeded, invocationToken.Span,
                    "Macro expansion depth limit (" + MaxExpansionDepth + ") exceeded while expanding '" + macro.Name + "'.");
                output.Add(invocationToken);
                return;
            }

            _expandingMacros.Add(macro.Name);
            try
            {
                var substituted = Substitute(macro, arguments);
                var pasted = ApplyTokenPaste(substituted);
                output.AddRange(ExpandLine(pasted));
            }
            finally
            {
                _expandingMacros.Remove(macro.Name);
            }
        }

        /// <summary>Parameter substitution per C99 §6.10.3: a parameter preceded by '#' is
        /// stringized from its unexpanded argument; a parameter adjacent to '##' (either side) is
        /// substituted with its raw, unexpanded argument tokens; any other parameter occurrence
        /// is substituted with its argument's fully macro-expanded tokens. '##' tokens themselves
        /// pass through unchanged here — <see cref="ApplyTokenPaste"/> consumes them next.</summary>
        private List<Token> Substitute(MacroDefinition macro, List<List<Token>> arguments)
        {
            var replacement = macro.ReplacementTokens;
            var result = new List<Token>(replacement.Count);

            for (var i = 0; i < replacement.Count; i++)
            {
                var token = replacement[i];

                if (token.Kind == HlslTokenKind.Hash && macro.Kind == MacroKind.FunctionLike && i + 1 < replacement.Count &&
                    TryGetParameterIndex(macro, replacement[i + 1].Text, out var stringizeIndex))
                {
                    result.Add(Stringize(token, replacement[i + 1], arguments[stringizeIndex]));
                    i++; // also consumed the parameter token
                    continue;
                }

                if (token.Kind == HlslTokenKind.Identifier && TryGetParameterIndex(macro, token.Text, out var paramIndex))
                {
                    var adjacentToPaste = (i > 0 && replacement[i - 1].Kind == HlslTokenKind.HashHash) ||
                                          (i + 1 < replacement.Count && replacement[i + 1].Kind == HlslTokenKind.HashHash);
                    var argument = arguments[paramIndex];
                    result.AddRange(adjacentToPaste ? argument : ExpandLine(argument));
                    continue;
                }

                result.Add(token);
            }

            return result;
        }

        private static bool TryGetParameterIndex(MacroDefinition macro, string name, out int index)
        {
            for (var i = 0; i < macro.Parameters.Count; i++)
            {
                if (macro.Parameters[i] == name)
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        private Token Stringize(Token hashToken, Token paramToken, List<Token> argumentTokens)
        {
            // value becomes Token.Value (decoded content); text becomes Token.Text (quoted, escaped spelling)
            var value = new StringBuilder();
            var text = new StringBuilder();

            for (var i = 0; i < argumentTokens.Count; i++)
            {
                // A single space wherever there was any whitespace/comment between two argument
                // tokens in the original source, none where they were written adjacent.
                if (i > 0 && argumentTokens[i - 1].Span.End != argumentTokens[i].Span.Start)
                {
                    value.Append(' ');
                    text.Append(' ');
                }

                var spelling = argumentTokens[i].Text;
                value.Append(spelling);
                text.Append(argumentTokens[i].Kind == HlslTokenKind.StringLiteral ? EscapeForStringize(spelling) : spelling);
            }

            var span = TextSpan.FromBounds(hashToken.Span.Start, paramToken.Span.End);
            return new Token
            {
                Kind = HlslTokenKind.StringLiteral, Span = span, ValueSpan = span, Source = _source,
                CachedText = "\"" + text + "\"", CachedValue = value.ToString()
            };
        }

        /// <summary>Escapes '"' and '\' in a string-literal argument token's spelling, per the
        /// C99 stringize rule — this naturally escapes the literal's own delimiting quotes too.</summary>
        private static string EscapeForStringize(string spelling)
        {
            var builder = new StringBuilder(spelling.Length + 2);
            foreach (var c in spelling)
            {
                if (c is '"' or '\\') builder.Append('\\');
                builder.Append(c);
            }

            return builder.ToString();
        }

        /// <summary>Performs every '##' paste in <paramref name="tokens"/> left-to-right, so a
        /// chain like <c>a ## b ## c</c> associates as <c>(a##b)##c</c>.</summary>
        private List<Token> ApplyTokenPaste(List<Token> tokens)
        {
            if (tokens.Count == 0) return tokens;

            var result = new List<Token>(tokens.Count);
            var i = 0;
            while (i < tokens.Count)
            {
                if (tokens[i].Kind == HlslTokenKind.HashHash && result.Count > 0 && i + 1 < tokens.Count)
                {
                    var left = result[result.Count - 1];
                    var right = tokens[i + 1];

                    if (TryPaste(left, right, out var pasted))
                    {
                        result[result.Count - 1] = pasted;
                    }
                    else
                    {
                        _diagnostics.Error(DiagnosticIds.MalformedTokenPaste,
                            TextSpan.FromBounds(left.Span.Start, right.Span.End),
                            "'" + left.Text + "' ## '" + right.Text + "' does not paste into a single valid token.");
                        result.Add(right); // leave both operands adjacent, unpasted
                    }

                    i += 2;
                    continue;
                }

                // A stray '##' with no left/right operand here means it survived past
                // definition-time validation (e.g. an argument substitution produced an empty
                // sequence next to it) — drop it rather than emit a bare '##' into ordinary code.
                if (tokens[i].Kind == HlslTokenKind.HashHash)
                {
                    i++;
                    continue;
                }

                result.Add(tokens[i]);
                i++;
            }

            return result;
        }

        /// <summary>Concatenates two tokens' spellings and re-lexes the result; succeeds only if
        /// that yields exactly one non-<see cref="HlslTokenKind.Unknown"/> token with no
        /// diagnostics of its own.</summary>
        private bool TryPaste(Token left, Token right, out Token pasted)
        {
            var combinedText = left.Text + right.Text;
            var throwawaySource = new SourceText(combinedText, _source.FileName);
            var throwawaySink = new DiagnosticSink(throwawaySource);
            var relexed = new Lexer(throwawaySource, throwawaySink).Tokenize();

            if (relexed.Count == 2 && relexed[0].Kind != HlslTokenKind.Unknown && !throwawaySink.HasErrors)
            {
                pasted = relexed[0];
                // The paste result is synthetic, so its span is the union of the two real operand
                // spans (same convention as any other macro-expansion output). Any kind whose
                // spelling isn't a fixed punctuation constant needs CachedText set explicitly,
                // since Token.Text would otherwise read the wrong substring at that union span.
                pasted.Span = TextSpan.FromBounds(left.Span.Start, right.Span.End);
                pasted.ValueSpan = pasted.Span;
                if (PunctuationText.Get(pasted.Kind) == null) pasted.CachedText = combinedText;
                return true;
            }

            pasted = default;
            return false;
        }
    }
}