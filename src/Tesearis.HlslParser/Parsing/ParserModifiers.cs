using System.Collections.Generic;
using Tesearis.HlslParser.Lexing;

namespace Tesearis.HlslParser.Parsing
{
    public partial class Parser
    {
        private List<string> ParseModifierList()
        {
            var modifiers = new List<string>();
            while (Current.Kind == HlslTokenKind.Keyword && HlslKeywords.IsModifierKeyword(Current.Text))
            {
                modifiers.Add(Current.Text);
                Advance();
            }

            return modifiers;
        }
    }
}