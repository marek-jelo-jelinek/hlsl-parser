using System.Collections.Generic;
using HlslParser.Diagnostics;
using HlslParser.Lexing;
using HlslParser.Syntax;
using HlslParser.Text;

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
            if (Current.Kind == HlslTokenKind.LessThan && CanHaveTypeArguments(name, category))
            {
                typeArguments = ParseTypeArgumentList();
            }

            return new TypeNameNode(SpanFrom(start), name, category, typeArguments);
        }

        private static bool CanHaveTypeArguments(string name, HlslKeywordCategory category)
        {
            return (category & HlslKeywordCategory.ResourceType) != 0 || name is "vector" or "matrix";
        }

        private List<TypeNameNode> ParseTypeArgumentList()
        {
            Advance();
            var args = new List<TypeNameNode>();
            while (true)
            {
                var before = _index;
                args.Add(ParseTypeArgument());
                if (!Match(HlslTokenKind.Comma)) break;
                if (_index == before) Advance();
            }

            ExpectClosingAngleBracket();
            return args;
        }

        private TypeNameNode ParseTypeArgument()
        {
            if (Current.Kind == HlslTokenKind.IntegerLiteral)
            {
                var start = Current.Span.Start;
                var text = Current.Text;
                Advance();
                return new TypeNameNode(SpanFrom(start), text, HlslKeywordCategory.None, null);
            }

            return ParseTypeName();
        }

        private void ExpectClosingAngleBracket()
        {
            if (Current.Kind == HlslTokenKind.GreaterThan)
            {
                Advance();
                return;
            }

            if (Current.Kind == HlslTokenKind.GreaterThanGreaterThan)
            {
                // Split '>>' into two '>' tokens. The second '>' replaces the current token in _tokens.
                var span = Current.Span;
                var secondSpan = new TextSpan(span.Start + 1, span.Length - 1);
                _tokens[_index] = new Token
                {
                    Kind = HlslTokenKind.GreaterThan,
                    Span = secondSpan,
                    ValueSpan = secondSpan,
                    Source = _source,
                    CachedText = ">",
                    CachedLine = Current.Line,
                    CachedColumn = Current.Column + 1
                };
                return;
            }

            Expect(HlslTokenKind.GreaterThan, "'>'");
        }
    }
}