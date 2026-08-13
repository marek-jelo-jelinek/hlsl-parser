using System.Collections.Generic;
using HlslParser.Diagnostics;
using HlslParser.Lexing;
using HlslParser.Syntax;

namespace HlslParser.Parsing
{
    public partial class Parser
    {
        private BlockStatementNode ParseBlock()
        {
            var start = Current.Span.Start;
            Advance();

            var statements = new List<HlslNode>();
            while (!AtEnd && Current.Kind != HlslTokenKind.CloseBrace)
            {
                var before = _index;
                statements.Add(ParseStatement());
                if (_index == before) Advance();
            }

            if (AtEnd)
            {
                Diagnostics.Error(DiagnosticIds.UnterminatedBlock, Current.Span, "Unterminated block; expected '}'.");
            }
            else
            {
                Advance();
            }

            return new BlockStatementNode(SpanFrom(start), statements);
        }

        private HlslNode ParseStatement()
        {
            switch (Current.Kind)
            {
                case HlslTokenKind.OpenBrace:
                    return ParseBlock();
                case HlslTokenKind.Semicolon:
                    return new EmptyStatementNode(Advance().Span);
                case HlslTokenKind.Keyword when Current.Text == "if":
                    return ParseIfStatement();
                case HlslTokenKind.Keyword when Current.Text == "for":
                    return ParseForStatement();
                case HlslTokenKind.Keyword when Current.Text == "while":
                    return ParseWhileStatement();
                case HlslTokenKind.Keyword when Current.Text == "do":
                    return ParseDoStatement();
                case HlslTokenKind.Keyword when Current.Text == "switch":
                    return ParseSwitchStatement();
                case HlslTokenKind.Keyword when Current.Text == "return":
                    return ParseReturnStatement();
                case HlslTokenKind.Keyword when Current.Text == "discard":
                    return ParseDiscardStatement();
                case HlslTokenKind.Keyword when Current.Text == "break":
                    return ParseBreakStatement();
                case HlslTokenKind.Keyword when Current.Text == "continue":
                    return ParseContinueStatement();
            }

            if (StartsTypeOrModifier(Current) && LooksLikeLocalDeclaration()) return ParseDeclarationStatement();
            if (CanStartExpression(Current)) return ParseExpressionStatement();

            Diagnostics.Error(DiagnosticIds.ExpectedStatement, Current.Span, "Expected a statement but found '" + Current.Text + "'.");
            return new ErrorNode(SkipToStatementRecoveryPoint(), "Unexpected token in statement position.");
        }

        /// <summary> Disambiguate a local declaration (<c>Type name = ...;</c>) from an expression statement whose leading token also looks type-shaped, e.g. a constructor call
        /// <c>float4(1,2,3,4);</c> or a plain call <c>foo();</c>. Non-consuming lookahead: skip modifiers, the type-candidate token (plus a best-effort skip over a single-level
        /// <c>&lt;...&gt;</c> template-argument list), and check whether an identifier follows — that pattern is unique to a declaration. </summary>
        private bool LooksLikeLocalDeclaration()
        {
            var offset = 0;
            while (Peek(offset).Kind == HlslTokenKind.Keyword && HlslKeywords.IsModifierKeyword(Peek(offset).Text)) offset++;

            var typeToken = Peek(offset);
            if (typeToken.Kind != HlslTokenKind.Keyword && typeToken.Kind != HlslTokenKind.Identifier) return false;
            offset++;

            // Only a resource-type keyword can legitimately be templated (mirrors ParseTypeName's
            // own gating) — a bare identifier followed by '<' is a relational comparison
            // (`a < b`), not the start of `SomeType<T>`; HLSL has no user-defined generics.
            if (typeToken.Kind == HlslTokenKind.Keyword && HlslKeywords.IsResourceKeyword(typeToken.Text) &&
                Peek(offset).Kind == HlslTokenKind.LessThan)
            {
                var depth = 0;
                do
                {
                    if (Peek(offset).Kind == HlslTokenKind.LessThan) depth++;
                    else if (Peek(offset).Kind == HlslTokenKind.GreaterThan) depth--;
                    offset++;
                } while (depth > 0 && Peek(offset).Kind != HlslTokenKind.EndOfFile && Peek(offset).Kind != HlslTokenKind.Semicolon);
            }

            return Peek(offset).Kind == HlslTokenKind.Identifier;
        }

        private DeclarationStatementNode ParseDeclarationStatement()
        {
            var start = Current.Span.Start;
            var modifiers = ParseModifierList();
            var type = ParseTypeName();
            var nameToken = Expect(HlslTokenKind.Identifier, "variable name");
            var declarators = ParseDeclaratorList(nameToken);
            return new DeclarationStatementNode(SpanFrom(start), modifiers, type, declarators);
        }

        private ExpressionStatementNode ParseExpressionStatement()
        {
            var start = Current.Span.Start;
            var expression = ParseExpression();
            Expect(HlslTokenKind.Semicolon, "';'");
            return new ExpressionStatementNode(SpanFrom(start), expression);
        }

        private IfStatementNode ParseIfStatement()
        {
            var start = Current.Span.Start;
            Advance();
            Expect(HlslTokenKind.OpenParen, "'('");
            var condition = ParseExpression();
            Expect(HlslTokenKind.CloseParen, "')'");
            var thenStatement = ParseStatement();

            HlslNode elseStatement = null;
            if (IsKeyword("else"))
            {
                Advance();
                elseStatement = ParseStatement();
            }

            return new IfStatementNode(SpanFrom(start), condition, thenStatement, elseStatement);
        }

        private ForStatementNode ParseForStatement()
        {
            var start = Current.Span.Start;
            Advance();
            Expect(HlslTokenKind.OpenParen, "'('");

            HlslNode initializer = null;
            if (Current.Kind != HlslTokenKind.Semicolon)
            {
                initializer = StartsTypeOrModifier(Current) && LooksLikeLocalDeclaration() ? ParseForDeclarationClause() : ParseExpressionStatement();
            }
            else
            {
                Advance(); // bare ';' — no initializer
            }

            HlslNode condition = null;
            if (Current.Kind != HlslTokenKind.Semicolon) condition = ParseExpression();
            Expect(HlslTokenKind.Semicolon, "';'");

            HlslNode incrementor = null;
            if (Current.Kind != HlslTokenKind.CloseParen) incrementor = ParseExpression();
            Expect(HlslTokenKind.CloseParen, "')'");

            var body = ParseStatement();
            return new ForStatementNode(SpanFrom(start), initializer, condition, incrementor, body);
        }

        /// <summary>A <c>for</c> head's declaration clause reuses <see cref="ParseDeclarationStatement"/> exactly — its declarator list is already <c>;</c>-terminated, matching what the for-loop head needs.</summary>
        private DeclarationStatementNode ParseForDeclarationClause()
        {
            return ParseDeclarationStatement();
        }

        private WhileStatementNode ParseWhileStatement()
        {
            var start = Current.Span.Start;
            Advance();
            Expect(HlslTokenKind.OpenParen, "'('");
            var condition = ParseExpression();
            Expect(HlslTokenKind.CloseParen, "')'");
            var body = ParseStatement();
            return new WhileStatementNode(SpanFrom(start), condition, body);
        }

        private DoStatementNode ParseDoStatement()
        {
            var start = Current.Span.Start;
            Advance();
            var body = ParseStatement();

            if (IsKeyword("while"))
            {
                Advance();
            }
            else
            {
                Diagnostics.Error(DiagnosticIds.ExpectedToken, Current.Span, "Expected 'while' but found '" + Current.Text + "'.");
            }

            Expect(HlslTokenKind.OpenParen, "'('");
            var condition = ParseExpression();
            Expect(HlslTokenKind.CloseParen, "')'");
            Expect(HlslTokenKind.Semicolon, "';'");
            return new DoStatementNode(SpanFrom(start), body, condition);
        }

        private SwitchStatementNode ParseSwitchStatement()
        {
            var start = Current.Span.Start;
            Advance();
            Expect(HlslTokenKind.OpenParen, "'('");
            var expression = ParseExpression();
            Expect(HlslTokenKind.CloseParen, "')'");

            var sections = new List<HlslNode>();
            if (Current.Kind == HlslTokenKind.OpenBrace)
            {
                Advance();
                while (!AtEnd && Current.Kind != HlslTokenKind.CloseBrace)
                {
                    var before = _index;
                    sections.Add(ParseSwitchSection());
                    if (_index == before) Advance();
                }

                if (AtEnd)
                {
                    Diagnostics.Error(DiagnosticIds.UnterminatedBlock, Current.Span, "Unterminated switch body; expected '}'.");
                }
                else
                {
                    Advance();
                }
            }
            else
            {
                Diagnostics.Error(DiagnosticIds.ExpectedToken, Current.Span, "Expected '{' but found '" + Current.Text + "'.");
            }

            return new SwitchStatementNode(SpanFrom(start), expression, sections);
        }

        private HlslNode ParseSwitchSection()
        {
            if (!IsKeyword("case") && !IsKeyword("default"))
            {
                Diagnostics.Error(DiagnosticIds.MalformedSwitchLabel, Current.Span, "Expected 'case' or 'default' but found '" + Current.Text + "'.");
                return new ErrorNode(SkipToStatementRecoveryPoint(), "Unexpected token in switch body.");
            }

            var start = Current.Span.Start;
            var labels = new List<SwitchLabelNode> { ParseSwitchLabel() };
            while (IsKeyword("case") || IsKeyword("default")) labels.Add(ParseSwitchLabel());

            var statements = new List<HlslNode>();
            while (!AtEnd && Current.Kind != HlslTokenKind.CloseBrace && !IsKeyword("case") && !IsKeyword("default"))
            {
                var before = _index;
                statements.Add(ParseStatement());
                if (_index == before) Advance();
            }

            return new SwitchSectionNode(SpanFrom(start), labels, statements);
        }

        private SwitchLabelNode ParseSwitchLabel()
        {
            var start = Current.Span.Start;
            if (IsKeyword("default"))
            {
                Advance();
                Expect(HlslTokenKind.Colon, "':'", DiagnosticIds.MalformedSwitchLabel);
                return new SwitchLabelNode(SpanFrom(start), null, true);
            }

            Advance();
            var value = ParseExpression();
            Expect(HlslTokenKind.Colon, "':'", DiagnosticIds.MalformedSwitchLabel);
            return new SwitchLabelNode(SpanFrom(start), value, false);
        }

        private ReturnStatementNode ParseReturnStatement()
        {
            var start = Current.Span.Start;
            Advance();
            HlslNode expression = null;
            if (Current.Kind != HlslTokenKind.Semicolon) expression = ParseExpression();
            Expect(HlslTokenKind.Semicolon, "';'");
            return new ReturnStatementNode(SpanFrom(start), expression);
        }

        private DiscardStatementNode ParseDiscardStatement()
        {
            var start = Current.Span.Start;
            Advance();
            Expect(HlslTokenKind.Semicolon, "';'");
            return new DiscardStatementNode(SpanFrom(start));
        }

        private BreakStatementNode ParseBreakStatement()
        {
            var start = Current.Span.Start;
            Advance();
            Expect(HlslTokenKind.Semicolon, "';'");
            return new BreakStatementNode(SpanFrom(start));
        }

        private ContinueStatementNode ParseContinueStatement()
        {
            var start = Current.Span.Start;
            Advance();
            Expect(HlslTokenKind.Semicolon, "';'");
            return new ContinueStatementNode(SpanFrom(start));
        }
    }
}