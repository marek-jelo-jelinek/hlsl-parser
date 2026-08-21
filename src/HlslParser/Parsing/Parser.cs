using System;
using System.Collections.Generic;
using HlslParser.Diagnostics;
using HlslParser.Lexing;
using HlslParser.Syntax;
using HlslParser.Text;

namespace HlslParser.Parsing
{
    /// <summary>
    /// Recursive-descent parser over the full HLSL/Cg grammar: struct, cbuffer/tbuffer, typedef,
    /// global variables (with register/packoffset/semantics), function declarations (with
    /// attributes and semantics), the full statement grammar (blocks, if/else, for/while/do-while,
    /// switch/case, return/discard/break/continue, local declarations), and a
    /// one-method-per-precedence-level expression ladder
    /// (assignment → ternary → logical → bitwise → equality → relational → shift → additive →
    /// multiplicative → unary/cast → postfix → primary).
    /// </summary>
    public partial class Parser
    {
        private readonly SourceText _source;
        private readonly List<Token> _tokens;
        private int _index;
        private bool _reportedEndOfFile;

        public Parser(SourceText source, List<Token> tokens, DiagnosticSink diagnostics)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
            if (_tokens.Count == 0) throw new ArgumentException("Token list must contain at least an EndOfFile token.", nameof(tokens));
            Diagnostics = diagnostics ?? new DiagnosticSink(source);
        }

        public DiagnosticSink Diagnostics { get; }
        
        private Token Current => _tokens[_index];

        private Token Peek(int offset)
        {
            var index = _index + offset;
            if (index < 0) index = 0;
            if (index >= _tokens.Count) index = _tokens.Count - 1;
            return _tokens[index];
        }

        private bool AtEnd => Current.Kind == HlslTokenKind.EndOfFile;

        private Token Advance()
        {
            var token = Current;
            if (_index < _tokens.Count - 1) _index++;
            return token;
        }

        private bool Match(HlslTokenKind kind)
        {
            if (Current.Kind != kind) return false;
            Advance();
            return true;
        }

        private Token Expect(HlslTokenKind kind, string description, string diagnosticId = DiagnosticIds.ExpectedToken)
        {
            if (Current.Kind == kind) return Advance();

            if (AtEnd)
            {
                if (!_reportedEndOfFile)
                {
                    _reportedEndOfFile = true;
                    Diagnostics.Error(DiagnosticIds.UnexpectedEndOfFile, Current.Span, "Unexpected end of file; expected " + description + ".");
                }
            }
            else
            {
                Diagnostics.Error(diagnosticId, Current.Span, "Expected " + description + " but found '" + Current.Text + "'.");
            }

            return Token.Missing(kind, new TextSpan(Current.Span.Start, 0), Current.Line, Current.Column);
        }

        private bool IsKeyword(string text)
        {
            return Current.Kind == HlslTokenKind.Keyword && Current.Text == text;
        }

        private bool IsContextualIdentifier(string text)
        {
            return Current.Kind == HlslTokenKind.Identifier && Current.Text == text;
        }

        private TextSpan SpanFrom(int start)
        {
            var last = _tokens[_index > 0 ? _index - 1 : 0];
            return TextSpan.FromBounds(start, Math.Max(start, last.Span.End));
        }

        /// <summary>Advances past tokens until a recovery point (or EOF), consuming a trailing
        /// <c>;</c> into the returned span. Does not consume a trailing <c>}</c> or the token that
        /// starts the next declaration — those are left for the caller's loop to re-dispatch on.</summary>
        private TextSpan SkipToRecoveryPoint()
        {
            var start = Current.Span.Start;
            while (!AtEnd && !IsRecoveryPoint(Current)) Advance();
            if (!AtEnd && Current.Kind == HlslTokenKind.Semicolon) Advance();
            return SpanFrom(start);
        }

        private static bool IsRecoveryPoint(Token token)
        {
            if (token.Kind == HlslTokenKind.Semicolon) return true;
            if (token.Kind == HlslTokenKind.CloseBrace) return true;
            if (token.Kind == HlslTokenKind.Keyword && HlslKeywords.IsDeclarationKeyword(token.Text)) return true;
            // A statement-starting control-flow keyword (if/for/while/...) is also a resync point —
            // needed now that this method doubles as the recovery boundary inside statement blocks,
            // not just at the top level.
            if (token.Kind == HlslTokenKind.Keyword && HlslKeywords.IsControlFlowKeyword(token.Text)) return true;
            return IsTopLevelTypeStart(token);
        }

        /// <summary>Statement-level counterpart of <see cref="SkipToRecoveryPoint"/>: additionally
        /// stops at anything that could start an expression statement. Unlike
        /// <see cref="IsRecoveryPoint"/>, which deliberately doesn't treat a bare identifier as a
        /// resync point (common mid-declaration noise), inside a block a bare identifier routinely
        /// IS the next statement — without this, recovery would swallow it.</summary>
        private TextSpan SkipToStatementRecoveryPoint()
        {
            var start = Current.Span.Start;
            while (!AtEnd && !IsRecoveryPoint(Current) && !CanStartExpression(Current)) Advance();
            if (!AtEnd && Current.Kind == HlslTokenKind.Semicolon) Advance();
            return SpanFrom(start);
        }

        /// <summary>Beyond a recognized declaration keyword, also treats the start of the next
        /// global-variable/function declaration (a type/modifier keyword, or an attribute's
        /// leading <c>[</c>) as a resync point, since that's overwhelmingly the common real-world
        /// recovery target.</summary>
        private static bool IsTopLevelTypeStart(Token token)
        {
            if (token.Kind == HlslTokenKind.OpenBracket) return true;
            if (token.Kind == HlslTokenKind.Keyword) return HlslKeywords.IsTypeKeyword(token.Text) || HlslKeywords.IsModifierKeyword(token.Text);
            return false;
        }

        /// <summary>Whether <paramref name="token"/> could plausibly start a type-or-modifier-led
        /// declaration. Deliberately admits any bare identifier (the only way to recognize
        /// user-defined type names like <c>MyStruct instance;</c> without a symbol table).</summary>
        private static bool StartsTypeOrModifier(Token token)
        {
            if (token.Kind == HlslTokenKind.Identifier) return true;
            if (token.Kind == HlslTokenKind.Keyword) return HlslKeywords.IsTypeKeyword(token.Text) || HlslKeywords.IsModifierKeyword(token.Text);
            return false;
        }
        
        public CompilationUnitNode ParseCompilationUnit(IEnumerable<PragmaDirectiveNode> pragmas)
        {
            var start = Current.Span.Start;
            var declarations = new List<HlslNode>();
            while (!AtEnd)
            {
                var before = _index;
                declarations.Add(ParseTopLevelDeclaration());
                if (_index == before) Advance();
            }

            return new CompilationUnitNode(SpanFrom(start), declarations, pragmas);
        }

        private HlslNode ParseTopLevelDeclaration()
        {
            var start = Current.Span.Start;
            var attributes = ParseAttributeList();

            if (IsKeyword("struct"))
            {
                WarnIfAttributesPresent(attributes, "a struct declaration");
                return ParseStructDeclaration(start);
            }

            if (IsKeyword("cbuffer") || IsKeyword("tbuffer"))
            {
                WarnIfAttributesPresent(attributes, "a buffer declaration");
                return ParseBufferDeclaration(start);
            }

            if (IsKeyword("typedef"))
            {
                WarnIfAttributesPresent(attributes, "a typedef declaration");
                return ParseTypedefDeclaration(start);
            }

            if (StartsTypeOrModifier(Current))
            {
                var modifiers = ParseModifierList();
                var type = ParseTypeName();
                var nameToken = Expect(HlslTokenKind.Identifier, "declaration name");

                if (Current.Kind == HlslTokenKind.OpenParen) return ParseFunctionDeclaration(start, attributes, modifiers, type, nameToken);

                var declarators = ParseDeclaratorList(nameToken);
                WarnIfAttributesPresent(attributes, "a variable declaration");
                return new GlobalVariableDeclarationNode(SpanFrom(start), modifiers, type, declarators);
            }

            Diagnostics.Error(DiagnosticIds.ExpectedDeclaration, Current.Span, "Expected a declaration but found '" + Current.Text + "'.");
            return new ErrorNode(SkipToRecoveryPoint(), "Unrecognized top-level content.");
        }

        private void WarnIfAttributesPresent(List<AttributeNode> attributes, string constructDescription)
        {
            if (attributes.Count == 0) return;
            Diagnostics.Warning(DiagnosticIds.MalformedAttribute, attributes[0].Span,
                "Attributes are not valid on " + constructDescription + ".");
        }
        
        private void ParseTrailingAnnotations(out SemanticClauseNode semantic, out RegisterClauseNode register, out PackoffsetClauseNode packoffset)
        {
            semantic = null;
            register = null;
            packoffset = null;

            while (Current.Kind == HlslTokenKind.Colon)
            {
                var colonStart = Current.Span.Start;
                Advance();

                if (IsContextualIdentifier("register"))
                {
                    var clause = ParseRegisterClauseBody(colonStart);
                    if (register != null) Diagnostics.Warning(DiagnosticIds.MalformedRegisterClause, clause.Span, "Duplicate 'register' clause.");
                    register = clause;
                }
                else if (IsContextualIdentifier("packoffset"))
                {
                    var clause = ParsePackoffsetClauseBody(colonStart);
                    if (packoffset != null)
                        Diagnostics.Warning(DiagnosticIds.MalformedPackoffsetClause, clause.Span, "Duplicate 'packoffset' clause.");
                    packoffset = clause;
                }
                else if (Current.Kind == HlslTokenKind.Identifier)
                {
                    var nameToken = Advance();
                    var clause = new SemanticClauseNode(TextSpan.FromBounds(colonStart, nameToken.Span.End), nameToken.Text);
                    if (semantic != null) Diagnostics.Warning(DiagnosticIds.MalformedSemantic, clause.Span, "Duplicate semantic.");
                    semantic = clause;
                }
                else
                {
                    Diagnostics.Error(DiagnosticIds.MalformedSemantic, Current.Span,
                        "Expected a semantic name, 'register', or 'packoffset' after ':'.");
                    break;
                }
            }
        }

        private RegisterClauseNode ParseRegisterClauseBody(int colonStart)
        {
            Advance();
            Expect(HlslTokenKind.OpenParen, "'('", DiagnosticIds.MalformedRegisterClause);
            var slotToken = Expect(HlslTokenKind.Identifier, "register slot (e.g. 'b0')", DiagnosticIds.MalformedRegisterClause);

            string space = null;
            if (Match(HlslTokenKind.Comma))
            {
                space = Expect(HlslTokenKind.Identifier, "register space (e.g. 'space1')", DiagnosticIds.MalformedRegisterClause).Text;
            }

            Expect(HlslTokenKind.CloseParen, "')'", DiagnosticIds.MalformedRegisterClause);
            return new RegisterClauseNode(SpanFrom(colonStart), slotToken.Text, space);
        }

        private PackoffsetClauseNode ParsePackoffsetClauseBody(int colonStart)
        {
            Advance();
            Expect(HlslTokenKind.OpenParen, "'('", DiagnosticIds.MalformedPackoffsetClause);
            var offsetToken = Expect(HlslTokenKind.Identifier, "packoffset offset (e.g. 'c0')", DiagnosticIds.MalformedPackoffsetClause);

            string swizzle = null;
            if (Match(HlslTokenKind.Dot))
            {
                swizzle = Expect(HlslTokenKind.Identifier, "component swizzle (e.g. 'x')", DiagnosticIds.MalformedPackoffsetClause).Text;
            }

            Expect(HlslTokenKind.CloseParen, "')'", DiagnosticIds.MalformedPackoffsetClause);
            return new PackoffsetClauseNode(SpanFrom(colonStart), offsetToken.Text, swizzle);
        }

        /// <summary>Shared single-clause <c>: SEMANTIC</c> parse used by a function's return
        /// semantic and a parameter's semantic (neither accepts register/packoffset).</summary>
        private SemanticClauseNode TryParseSemantic()
        {
            if (Current.Kind != HlslTokenKind.Colon) return null;
            var colonStart = Current.Span.Start;
            Advance();

            if (Current.Kind == HlslTokenKind.Identifier)
            {
                var nameToken = Advance();
                return new SemanticClauseNode(TextSpan.FromBounds(colonStart, nameToken.Span.End), nameToken.Text);
            }

            Diagnostics.Error(DiagnosticIds.MalformedSemantic, Current.Span, "Expected a semantic name after ':'.");
            return null;
        }

        /// <summary>An <c>= expression</c> initializer on a declarator or parameter default value.
        /// No terminator tracking needed: an expression production never itself consumes a bare
        /// top-level <c>,</c> or <c>;</c> (call/index argument lists are scoped by their own
        /// brackets), so parsing naturally stops where the declarator/parameter-list terminator
        /// begins. A leading <c>{</c> instead starts a brace/aggregate initializer list (e.g.
        /// <c>static const float2 offsets[4] = { float2(0,0), ... };</c>) rather than an ordinary
        /// expression.</summary>
        private InitializerNode TryParseInitializerExpression()
        {
            if (!Match(HlslTokenKind.Equals)) return null;

            var start = Current.Span.Start;
            var expression = Current.Kind == HlslTokenKind.OpenBrace ? ParseInitializerList() : ParseAssignment();
            return new InitializerNode(SpanFrom(start), expression);
        }

        /// <summary>A <c>{ expr, expr, ... }</c> brace initializer list. Each element may itself be
        /// a nested <see cref="InitializerListExpressionNode"/> (e.g. <c>float2x2 m = {{1,0},{0,1}};</c>
        /// or an array-of-struct initializer) — mirrors the comma-list/stuck-cursor-guard pattern
        /// used by <see cref="ParserExpressions.ParseInvocation"/>'s argument list.</summary>
        private HlslNode ParseInitializerList()
        {
            var start = Current.Span.Start;
            Advance(); // '{'

            var elements = new List<HlslNode>();
            if (Current.Kind != HlslTokenKind.CloseBrace)
            {
                while (true)
                {
                    var before = _index;
                    elements.Add(Current.Kind == HlslTokenKind.OpenBrace ? ParseInitializerList() : ParseAssignment());
                    if (!Match(HlslTokenKind.Comma)) break;
                    if (Current.Kind == HlslTokenKind.CloseBrace) break; // trailing comma, e.g. {1,2,3,}
                    if (_index == before) Advance();
                }
            }

            Expect(HlslTokenKind.CloseBrace, "'}'", DiagnosticIds.MalformedInitializerList);
            return new InitializerListExpressionNode(SpanFrom(start), elements);
        }
    }
}