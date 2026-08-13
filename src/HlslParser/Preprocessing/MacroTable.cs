using System.Collections.Generic;
using HlslParser.Diagnostics;

namespace HlslParser.Preprocessing
{
    internal sealed class MacroTable
    {
        private readonly Dictionary<string, MacroDefinition> _macros = new();

        public bool IsDefined(string name)
        {
            return _macros.ContainsKey(name);
        }

        public bool TryGet(string name, out MacroDefinition macro)
        {
            return _macros.TryGetValue(name, out macro);
        }

        /// <summary>Defines <paramref name="macro"/>, following the standard C-preprocessor
        /// redefinition rule: an identical redefinition (same kind/parameters/replacement
        /// tokens) is silently accepted; a differing redefinition reports
        /// <see cref="DiagnosticIds.MacroRedefinition"/> as a Warning and the new definition
        /// wins.</summary>
        public void Define(MacroDefinition macro, DiagnosticSink diagnostics)
        {
            if (_macros.TryGetValue(macro.Name, out var existing) && !existing.IsIdenticalTo(macro))
            {
                diagnostics.Warning(DiagnosticIds.MacroRedefinition, macro.DefinitionSpan,
                    "Macro '" + macro.Name + "' is redefined with a different body.");
            }

            _macros[macro.Name] = macro;
        }

        /// <summary>Removes a macro. Undefining a name that isn't currently defined is silently
        /// accepted, matching standard <c>#undef</c> behavior.</summary>
        public void Undefine(string name)
        {
            _macros.Remove(name);
        }
    }
}