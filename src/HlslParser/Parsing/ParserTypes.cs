using System.Collections.Generic;
using HlslParser.Diagnostics;
using HlslParser.Lexing;
using HlslParser.Syntax;

namespace HlslParser.Parsing
{
    public partial class Parser
    {
        private TypeNameNode ParseTypeName()
        {
            var start = Current.Span.Start;
            string name;
            HlslKeywordCategory category;

            if (Current.Kind == HlslTokenKind.Keyword)
            {
                HlslKeywords.TryGetCanonical(Current.Text, out name, out category);
                Advance();
            }
            else if (Current.Kind == HlslTokenKind.Identifier)
            {
                name = Current.Text;
                category = HlslKeywordCategory.None;
                Advance();
            }
            else
            {
                Diagnostics.Error(DiagnosticIds.MissingTypeName, Current.Span, "Expected a type name but found '" + Current.Text + "'.");
                name = string.Empty;
                category = HlslKeywordCategory.None;
            }

            var typeArguments = new List<TypeNameNode>();
            if (Current.Kind == HlslTokenKind.LessThan && (category & HlslKeywordCategory.ResourceType) != 0)
            {
                typeArguments = ParseTypeArgumentList();
            }

            return new TypeNameNode(SpanFrom(start), name, category, typeArguments);
        }

        private List<TypeNameNode> ParseTypeArgumentList()
        {
            Advance();
            var args = new List<TypeNameNode>();
            while (true)
            {
                var before = _index;
                args.Add(ParseTypeName());
                if (!Match(HlslTokenKind.Comma)) break;
                if (_index == before) Advance();
            }

            Expect(HlslTokenKind.GreaterThan, "'>'");
            return args;
        }
    }
}