using System.Collections.Generic;
using HlslParser.Lexing;
using HlslParser.Text;

namespace HlslParser.Preprocessing
{
    internal enum MacroKind
    {
        ObjectLike,
        FunctionLike
    }

    /// <summary>An immutable <c>#define</c>d macro: its parameter list (if function-like) and its raw, unexpanded replacement token list.</summary>
    internal sealed class MacroDefinition
    {
        public MacroDefinition(string name, MacroKind kind, IReadOnlyList<string> parameters, IReadOnlyList<Token> replacementTokens,
            TextSpan definitionSpan)
        {
            Name = name;
            Kind = kind;
            Parameters = parameters ?? EmptyParameters;
            ReplacementTokens = replacementTokens ?? EmptyTokens;
            DefinitionSpan = definitionSpan;
        }

        private static readonly string[] EmptyParameters = new string[0];
        private static readonly Token[] EmptyTokens = new Token[0];

        public string Name { get; }
        public MacroKind Kind { get; }

        /// <summary>Parameter names in declaration order. Empty for <see cref="MacroKind.ObjectLike"/>.</summary>
        public IReadOnlyList<string> Parameters { get; }

        /// <summary>Raw, unexpanded replacement token list as written after the macro's name
        /// (and parameter list, for function-like macros).</summary>
        public IReadOnlyList<Token> ReplacementTokens { get; }

        public TextSpan DefinitionSpan { get; }

        /// <summary>Structural equality used by the standard C-preprocessor redefinition rule:
        /// redefining a macro with an identical kind/parameter-list/replacement-token-sequence is
        /// silently allowed; anything else is a <see cref="Diagnostics.DiagnosticIds.MacroRedefinition"/>
        /// warning. Token identity is compared by spelling (<see cref="Token.Text"/>) and kind,
        /// not by source position — two macros defined at different offsets with the same text
        /// are still "identical".</summary>
        public bool IsIdenticalTo(MacroDefinition other)
        {
            if (other == null) return false;
            if (Kind != other.Kind) return false;

            if (Parameters.Count != other.Parameters.Count) return false;
            for (var i = 0; i < Parameters.Count; i++)
            {
                if (Parameters[i] != other.Parameters[i]) return false;
            }

            if (ReplacementTokens.Count != other.ReplacementTokens.Count) return false;
            for (var i = 0; i < ReplacementTokens.Count; i++)
            {
                var a = ReplacementTokens[i];
                var b = other.ReplacementTokens[i];
                if (a.Kind != b.Kind || a.Text != b.Text) return false;
            }

            return true;
        }
    }
}