using System;
using System.Collections.Generic;
using Tesearis.HlslParser.Diagnostics;
using Tesearis.HlslParser.Lexing;
using Tesearis.HlslParser.Syntax;

namespace Tesearis.HlslParser.Parsing
{
    public partial class Parser
    {
        private CbufferDeclarationNode ParseBufferDeclaration(int start)
        {
            Advance(); // 'cbuffer' or 'tbuffer'
            var nameToken = Expect(HlslTokenKind.Identifier, "buffer name", DiagnosticIds.MalformedCbufferDeclaration);
            var register = TryParseCbufferRegister();

            if (Current.Kind != HlslTokenKind.OpenBrace)
            {
                Diagnostics.Error(DiagnosticIds.ExpectedToken, Current.Span, "Expected '{' but found '" + Current.Text + "'.");
                return new CbufferDeclarationNode(SpanFrom(start), nameToken.Text, Array.Empty<HlslNode>(), register, true);
            }

            Advance();
            var members = new List<HlslNode>();
            while (!AtEnd && Current.Kind != HlslTokenKind.CloseBrace)
            {
                var before = _index;
                var member = ParseCbufferMember();
                if (member != null) members.Add(member);
                if (_index == before) Advance();
            }

            if (AtEnd)
            {
                Diagnostics.Error(DiagnosticIds.UnterminatedCbuffer, Current.Span, "Unterminated buffer body; expected '}'.");
            }
            else
            {
                Advance();
            }

            Match(HlslTokenKind.Semicolon);
            return new CbufferDeclarationNode(SpanFrom(start), nameToken.Text, members, register, false);
        }

        private RegisterClauseNode TryParseCbufferRegister()
        {
            if (Current.Kind != HlslTokenKind.Colon) return null;
            var colonStart = Current.Span.Start;
            Advance();

            if (IsContextualIdentifier("register")) return ParseRegisterClauseBody(colonStart);

            Diagnostics.Error(DiagnosticIds.MalformedCbufferDeclaration, Current.Span,
                "Expected 'register' after ':' but found '" + Current.Text + "'.");
            return null;
        }

        private HlslNode ParseCbufferMember()
        {
            if (StartsTypeOrModifier(Current))
            {
                var start = Current.Span.Start;
                var modifiers = ParseModifierList();
                var type = ParseTypeName();
                var nameToken = Expect(HlslTokenKind.Identifier, "field name");
                var declarators = ParseDeclaratorList(nameToken);
                return new GlobalVariableDeclarationNode(SpanFrom(start), modifiers, type, declarators);
            }

            Diagnostics.Error(DiagnosticIds.ExpectedDeclaration, Current.Span, "Expected a cbuffer member but found '" + Current.Text + "'.");
            return new ErrorNode(SkipToRecoveryPoint(), "Unexpected token in cbuffer body.");
        }
    }
}