using System;
using System.Collections.Generic;
using Tesearis.HlslParser.Diagnostics;
using Tesearis.HlslParser.Lexing;
using Tesearis.HlslParser.Syntax;

namespace Tesearis.HlslParser.Parsing
{
    public partial class Parser
    {
        private StructDeclarationNode ParseStructDeclaration(int start)
        {
            Advance();
            var nameToken = Expect(HlslTokenKind.Identifier, "struct name", DiagnosticIds.MalformedStructDeclaration);

            if (Current.Kind != HlslTokenKind.OpenBrace)
            {
                Diagnostics.Error(DiagnosticIds.ExpectedToken, Current.Span, "Expected '{' but found '" + Current.Text + "'.");
                return new StructDeclarationNode(SpanFrom(start), nameToken.Text, Array.Empty<HlslNode>(), true);
            }

            Advance();
            var fields = new List<HlslNode>();
            while (!AtEnd && Current.Kind != HlslTokenKind.CloseBrace)
            {
                var before = _index;
                var member = ParseStructMember();
                if (member != null) fields.Add(member);
                if (_index == before) Advance();
            }

            if (AtEnd)
            {
                Diagnostics.Error(DiagnosticIds.UnterminatedStruct, Current.Span, "Unterminated struct body; expected '}'.");
            }
            else
            {
                Advance();
            }

            Match(HlslTokenKind.Semicolon); // trailing ';' tolerated leniently, no diagnostic if absent
            return new StructDeclarationNode(SpanFrom(start), nameToken.Text, fields, false);
        }

        private HlslNode ParseStructMember()
        {
            if (StartsTypeOrModifier(Current))
            {
                var start = Current.Span.Start;
                var modifiers = ParseModifierList();
                var type = ParseTypeName();
                var nameToken = Expect(HlslTokenKind.Identifier, "field name");
                var declarators = ParseDeclaratorList(nameToken);
                return new StructFieldNode(SpanFrom(start), modifiers, type, declarators);
            }

            Diagnostics.Error(DiagnosticIds.ExpectedDeclaration, Current.Span, "Expected a struct field but found '" + Current.Text + "'.");
            return new ErrorNode(SkipToRecoveryPoint(), "Unexpected token in struct body.");
        }
    }
}