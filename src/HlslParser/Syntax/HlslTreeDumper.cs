using System.Text;
using HlslParser.Text;

namespace HlslParser.Syntax
{
    /// <summary>
    /// Renders the tree as indented text. Handy for eyeballing parser output, and as the
    /// comparison format for golden-file tests (see <c>tests/HlslParser.Tests/Syntax/
    /// HlslTreeDumperTests.cs</c>).
    /// </summary>
    public sealed class HlslTreeDumper : HlslVisitor
    {
        private readonly StringBuilder _builder = new();
        private readonly SourceText _source;
        private int _depth;

        public HlslTreeDumper(SourceText source)
        {
            _source = source;
        }

        public static string Dump(HlslNode node, SourceText source)
        {
            var dumper = new HlslTreeDumper(source);
            dumper.Visit(node);
            return dumper.ToString();
        }

        public override string ToString()
        {
            return _builder.ToString();
        }

        private void Write(HlslNode node, string text)
        {
            _builder.Append(' ', _depth * 2);
            _builder.Append(text);
            if (_source != null && node != null)
            {
                var position = _source.GetLinePosition(node.Span.Start);
                _builder.Append("   @").Append(position.Line).Append(':').Append(position.Column);
            }

            _builder.Append('\n');
        }

        private void Descend(HlslNode node)
        {
            _depth++;
            DefaultVisit(node);
            _depth--;
        }

        private static string FormatModifiers(System.Collections.Generic.IReadOnlyList<string> modifiers)
        {
            return modifiers.Count == 0 ? "" : " [" + string.Join(" ", modifiers) + "]";
        }

        public override void VisitCompilationUnit(CompilationUnitNode node)
        {
            Write(node, "CompilationUnit (" + node.Declarations.Count + ")");
            Descend(node);
        }

        public override void VisitError(ErrorNode node)
        {
            Write(node, "Error: " + node.Message);
        }

        public override void VisitPragmaDirective(PragmaDirectiveNode node)
        {
            Write(node, "Pragma " + node.Name + (node.Arguments.Count > 0 ? " " + string.Join(" ", node.Arguments) : ""));
        }

        public override void VisitTypeName(TypeNameNode node)
        {
            Write(node, "TypeName " + node.Name + (node.IsUserType ? " (user)" : ""));
            Descend(node);
        }

        public override void VisitArrayRank(ArrayRankNode node)
        {
            var content = !node.HasContent ? "[]" : node.ConstantSize.HasValue ? "[" + node.ConstantSize.Value + "]" : "[<expr>]";
            Write(node, "ArrayRank " + content);
        }

        public override void VisitAttribute(AttributeNode node)
        {
            Write(node, "Attribute [" + node.Name + "]");
            Descend(node);
        }

        public override void VisitAttributeArgument(AttributeArgumentNode node)
        {
            Write(node, "Argument " + node.RawText);
            Descend(node);
        }

        public override void VisitStructDeclaration(StructDeclarationNode node)
        {
            Write(node, "Struct " + node.Name + (node.IsMissingBody ? " <missing body>" : ""));
            Descend(node);
        }

        public override void VisitStructField(StructFieldNode node)
        {
            Write(node, "Field" + FormatModifiers(node.Modifiers));
            Descend(node);
        }

        public override void VisitCbufferDeclaration(CbufferDeclarationNode node)
        {
            Write(node, "Cbuffer " + node.Name + (node.IsMissingBody ? " <missing body>" : ""));
            Descend(node);
        }

        public override void VisitTypedefDeclaration(TypedefDeclarationNode node)
        {
            Write(node, "Typedef " + node.AliasName);
            Descend(node);
        }

        public override void VisitGlobalVariableDeclaration(GlobalVariableDeclarationNode node)
        {
            Write(node, "GlobalVariable" + FormatModifiers(node.Modifiers));
            Descend(node);
        }

        public override void VisitVariableDeclarator(VariableDeclaratorNode node)
        {
            Write(node, "Declarator " + node.Name);
            Descend(node);
        }

        public override void VisitRegisterClause(RegisterClauseNode node)
        {
            Write(node, "Register " + node.RegisterSlot + (node.RegisterSpace != null ? ", " + node.RegisterSpace : ""));
        }

        public override void VisitPackoffsetClause(PackoffsetClauseNode node)
        {
            Write(node, "Packoffset " + node.Offset + (node.ComponentSwizzle != null ? "." + node.ComponentSwizzle : ""));
        }

        public override void VisitSemanticClause(SemanticClauseNode node)
        {
            Write(node, "Semantic " + node.Name);
        }

        public override void VisitInitializer(InitializerNode node)
        {
            Write(node, "Initializer");
            Descend(node);
        }

        public override void VisitFunctionDeclaration(FunctionDeclarationNode node)
        {
            Write(node, "Function " + node.Name + (node.IsForwardDeclaration ? " <forward>" : ""));
            Descend(node);
        }

        public override void VisitParameter(ParameterNode node)
        {
            Write(node, "Parameter " + node.Name + FormatModifiers(node.Modifiers));
            Descend(node);
        }

        public override void VisitBlock(BlockStatementNode node)
        {
            Write(node, "Block (" + node.Statements.Count + ")");
            Descend(node);
        }

        public override void VisitExpressionStatement(ExpressionStatementNode node)
        {
            Write(node, "ExpressionStatement");
            Descend(node);
        }

        public override void VisitDeclarationStatement(DeclarationStatementNode node)
        {
            Write(node, "DeclarationStatement" + FormatModifiers(node.Modifiers));
            Descend(node);
        }

        public override void VisitIfStatement(IfStatementNode node)
        {
            Write(node, "If");
            Descend(node);
        }

        public override void VisitForStatement(ForStatementNode node)
        {
            Write(node, "For");
            Descend(node);
        }

        public override void VisitWhileStatement(WhileStatementNode node)
        {
            Write(node, "While");
            Descend(node);
        }

        public override void VisitDoStatement(DoStatementNode node)
        {
            Write(node, "Do");
            Descend(node);
        }

        public override void VisitSwitchStatement(SwitchStatementNode node)
        {
            Write(node, "Switch");
            Descend(node);
        }

        public override void VisitSwitchSection(SwitchSectionNode node)
        {
            Write(node, "Section");
            Descend(node);
        }

        public override void VisitSwitchLabel(SwitchLabelNode node)
        {
            Write(node, node.IsDefault ? "Default" : "Case");
            Descend(node);
        }

        public override void VisitReturnStatement(ReturnStatementNode node)
        {
            Write(node, "Return");
            Descend(node);
        }

        public override void VisitDiscardStatement(DiscardStatementNode node)
        {
            Write(node, "Discard");
        }

        public override void VisitBreakStatement(BreakStatementNode node)
        {
            Write(node, "Break");
        }

        public override void VisitContinueStatement(ContinueStatementNode node)
        {
            Write(node, "Continue");
        }

        public override void VisitEmptyStatement(EmptyStatementNode node)
        {
            Write(node, "Empty");
        }

        public override void VisitLiteralExpression(LiteralExpressionNode node)
        {
            Write(node, "Literal " + node.Text);
        }

        public override void VisitIdentifierExpression(IdentifierExpressionNode node)
        {
            Write(node, "Identifier " + node.Name);
        }

        public override void VisitParenthesizedExpression(ParenthesizedExpressionNode node)
        {
            Write(node, "Parenthesized");
            Descend(node);
        }

        public override void VisitCastExpression(CastExpressionNode node)
        {
            Write(node, "Cast" + FormatModifiers(node.Modifiers));
            Descend(node);
        }

        public override void VisitUnaryExpression(UnaryExpressionNode node)
        {
            Write(node, "Unary " + node.OperatorKind + (node.IsPostfix ? " (postfix)" : " (prefix)"));
            Descend(node);
        }

        public override void VisitBinaryExpression(BinaryExpressionNode node)
        {
            Write(node, "Binary " + node.OperatorKind);
            Descend(node);
        }

        public override void VisitConditionalExpression(ConditionalExpressionNode node)
        {
            Write(node, "Conditional");
            Descend(node);
        }

        public override void VisitAssignmentExpression(AssignmentExpressionNode node)
        {
            Write(node, "Assignment " + node.OperatorKind);
            Descend(node);
        }

        public override void VisitInvocationExpression(InvocationExpressionNode node)
        {
            Write(node, "Invocation");
            Descend(node);
        }

        public override void VisitElementAccessExpression(ElementAccessExpressionNode node)
        {
            Write(node, "ElementAccess");
            Descend(node);
        }

        public override void VisitMemberAccessExpression(MemberAccessExpressionNode node)
        {
            Write(node, "MemberAccess ." + node.MemberName);
            Descend(node);
        }
    }
}
