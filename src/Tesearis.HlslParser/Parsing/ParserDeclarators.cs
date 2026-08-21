using System.Collections.Generic;
using Tesearis.HlslParser.Lexing;
using Tesearis.HlslParser.Syntax;

namespace Tesearis.HlslParser.Parsing
{
    public partial class Parser
    {
        private List<VariableDeclaratorNode> ParseDeclaratorList(Token firstNameToken)
        {
            var declarators = new List<VariableDeclaratorNode> { ParseDeclaratorTail(firstNameToken) };
            while (Match(HlslTokenKind.Comma))
            {
                var before = _index;
                var nameToken = Expect(HlslTokenKind.Identifier, "variable name");
                declarators.Add(ParseDeclaratorTail(nameToken));
                if (_index == before) break;
            }

            Expect(HlslTokenKind.Semicolon, "';'");
            return declarators;
        }

        private VariableDeclaratorNode ParseDeclaratorTail(Token nameToken)
        {
            var start = nameToken.Span.Start;
            var ranks = ParseArrayRanks();
            ParseTrailingAnnotations(out var semantic, out var register, out var packoffset);
            var initializer = TryParseInitializerExpression();
            return new VariableDeclaratorNode(SpanFrom(start), nameToken.Text, ranks, semantic, register, packoffset, initializer);
        }
    }
}