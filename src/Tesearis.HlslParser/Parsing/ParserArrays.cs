using System.Collections.Generic;
using Tesearis.HlslParser.Diagnostics;
using Tesearis.HlslParser.Lexing;
using Tesearis.HlslParser.Syntax;

namespace Tesearis.HlslParser.Parsing
{
    public partial class Parser
    {
        private List<ArrayRankNode> ParseArrayRanks()
        {
            var ranks = new List<ArrayRankNode>();
            while (Current.Kind == HlslTokenKind.OpenBracket) ranks.Add(ParseArrayRank());
            return ranks;
        }

        private ArrayRankNode ParseArrayRank()
        {
            var start = Current.Span.Start;
            Advance();
            var hasContent = Current.Kind != HlslTokenKind.CloseBracket;
            int? constantSize = null;

            if (hasContent)
            {
                if (Current.Kind == HlslTokenKind.IntegerLiteral && Peek(1).Kind == HlslTokenKind.CloseBracket)
                {
                    constantSize = (int)Current.IntegerValue;
                    Advance();
                }
                else
                {
                    var depth = 1;
                    while (!AtEnd && depth > 0)
                    {
                        if (Current.Kind == HlslTokenKind.OpenBracket)
                        {
                            depth++;
                        }
                        else if (Current.Kind == HlslTokenKind.CloseBracket)
                        {
                            depth--;
                            if (depth == 0) break;
                        }

                        Advance();
                    }
                }
            }

            Expect(HlslTokenKind.CloseBracket, "']'", DiagnosticIds.MalformedArrayRank);
            return new ArrayRankNode(SpanFrom(start), hasContent, constantSize);
        }
    }
}