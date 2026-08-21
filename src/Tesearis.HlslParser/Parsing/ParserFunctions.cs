using System.Collections.Generic;
using Tesearis.HlslParser.Diagnostics;
using Tesearis.HlslParser.Lexing;
using Tesearis.HlslParser.Syntax;

namespace Tesearis.HlslParser.Parsing
{
    public partial class Parser
    {
        private FunctionDeclarationNode ParseFunctionDeclaration(int start, List<AttributeNode> attributes, List<string> modifiers,
            TypeNameNode returnType, Token nameToken)
        {
            var parameters = ParseParameterList();
            var returnSemantic = TryParseSemantic();
            var body = ParseFunctionBody();
            return new FunctionDeclarationNode(SpanFrom(start), attributes, modifiers, returnType, nameToken.Text, parameters, returnSemantic, body);
        }

        private List<HlslNode> ParseParameterList()
        {
            Expect(HlslTokenKind.OpenParen, "'('");
            var result = new List<HlslNode>();

            if (Current.Kind != HlslTokenKind.CloseParen)
            {
                while (true)
                {
                    var before = _index;
                    result.Add(ParseParameter());
                    if (!Match(HlslTokenKind.Comma)) break;
                    if (_index == before) Advance();
                }
            }

            if (AtEnd)
            {
                Diagnostics.Error(DiagnosticIds.UnterminatedParameterList, Current.Span, "Unterminated parameter list; expected ')'.");
            }
            else
            {
                Expect(HlslTokenKind.CloseParen, "')'");
            }

            return result;
        }

        private HlslNode ParseParameter()
        {
            var start = Current.Span.Start;
            var modifiers = ParseModifierList();

            if (!StartsTypeOrModifier(Current))
            {
                Diagnostics.Error(DiagnosticIds.MalformedFunctionDeclaration, Current.Span, "Expected a parameter but found '" + Current.Text + "'.");

                // Narrower local skip than SkipToRecoveryPoint — ')' (not just ';'/'}'/decl-keyword) is the natural boundary for "give up on this one parameter" here.
                var depth = 0;
                while (!AtEnd)
                {
                    if (Current.Kind == HlslTokenKind.OpenParen)
                    {
                        depth++;
                    }
                    else if (Current.Kind == HlslTokenKind.CloseParen)
                    {
                        if (depth == 0) break;
                        depth--;
                    }
                    else if (depth == 0 && Current.Kind == HlslTokenKind.Comma)
                    {
                        break;
                    }

                    Advance();
                }

                return new ErrorNode(SpanFrom(start), "Unexpected token in parameter list.");
            }

            var type = ParseTypeName();
            var name = Current.Kind == HlslTokenKind.Identifier ? Advance().Text : string.Empty; // legal unnamed prototype parameter — no diagnostic
            var ranks = ParseArrayRanks();
            var semantic = TryParseSemantic();
            var defaultValue = TryParseInitializerExpression();
            return new ParameterNode(SpanFrom(start), modifiers, type, name, ranks, semantic, defaultValue);
        }

        private HlslNode ParseFunctionBody()
        {
            if (Current.Kind == HlslTokenKind.OpenBrace) return ParseBlock();
            if (Match(HlslTokenKind.Semicolon)) return null; // legitimate forward declaration
            Diagnostics.Error(DiagnosticIds.ExpectedToken, Current.Span, "Expected '{' or ';' but found '" + Current.Text + "'.");
            return null;
        }
    }
}