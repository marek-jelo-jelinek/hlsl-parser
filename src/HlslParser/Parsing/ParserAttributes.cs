using System.Collections.Generic;
using HlslParser.Diagnostics;
using HlslParser.Lexing;
using HlslParser.Syntax;

namespace HlslParser.Parsing
{
    public partial class Parser
    {
        private List<AttributeNode> ParseAttributeList()
        {
            var attributes = new List<AttributeNode>();
            while (Current.Kind == HlslTokenKind.OpenBracket) attributes.Add(ParseAttribute());
            return attributes;
        }

        private AttributeNode ParseAttribute()
        {
            var start = Current.Span.Start;
            Advance();

            string name;
            if (Current.Kind == HlslTokenKind.Identifier || Current.Kind == HlslTokenKind.Keyword)
            {
                name = Current.Text;
                Advance();
            }
            else
            {
                Diagnostics.Error(DiagnosticIds.MalformedAttribute, Current.Span, "Expected an attribute name but found '" + Current.Text + "'.");
                name = string.Empty;
            }

            var arguments = new List<AttributeArgumentNode>();
            if (Match(HlslTokenKind.OpenParen))
            {
                if (Current.Kind != HlslTokenKind.CloseParen)
                {
                    while (true)
                    {
                        var before = _index;
                        arguments.Add(ParseAttributeArgument());
                        if (!Match(HlslTokenKind.Comma)) break;
                        if (_index == before) Advance();
                    }
                }

                Expect(HlslTokenKind.CloseParen, "')'", DiagnosticIds.MalformedAttribute);
            }

            Expect(HlslTokenKind.CloseBracket, "']'", DiagnosticIds.MalformedAttribute);
            return new AttributeNode(SpanFrom(start), name, arguments);
        }

        private AttributeArgumentNode ParseAttributeArgument()
        {
            var start = Current.Span.Start;
            var expression = ParseAssignment();
            var span = SpanFrom(start);
            return new AttributeArgumentNode(span, expression, _source.GetText(span));
        }
    }
}