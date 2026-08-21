namespace Tesearis.HlslParser.Syntax
{
    public abstract class HlslVisitor
    {
        public virtual void DefaultVisit(HlslNode node)
        {
            if (node == null) return;
            foreach (var child in node.Children)
            {
                child?.Accept(this);
            }
        }

        public void Visit(HlslNode node)
        {
            node?.Accept(this);
        }

        public virtual void VisitCompilationUnit(CompilationUnitNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitError(ErrorNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitPragmaDirective(PragmaDirectiveNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitTypeName(TypeNameNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitArrayRank(ArrayRankNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitAttribute(AttributeNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitAttributeArgument(AttributeArgumentNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitStructDeclaration(StructDeclarationNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitStructField(StructFieldNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitCbufferDeclaration(CbufferDeclarationNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitTypedefDeclaration(TypedefDeclarationNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitGlobalVariableDeclaration(GlobalVariableDeclarationNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitVariableDeclarator(VariableDeclaratorNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitRegisterClause(RegisterClauseNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitPackoffsetClause(PackoffsetClauseNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitSemanticClause(SemanticClauseNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitInitializer(InitializerNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitFunctionDeclaration(FunctionDeclarationNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitParameter(ParameterNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitBlock(BlockStatementNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitExpressionStatement(ExpressionStatementNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitDeclarationStatement(DeclarationStatementNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitIfStatement(IfStatementNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitForStatement(ForStatementNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitWhileStatement(WhileStatementNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitDoStatement(DoStatementNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitSwitchStatement(SwitchStatementNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitSwitchSection(SwitchSectionNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitSwitchLabel(SwitchLabelNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitReturnStatement(ReturnStatementNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitDiscardStatement(DiscardStatementNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitBreakStatement(BreakStatementNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitContinueStatement(ContinueStatementNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitEmptyStatement(EmptyStatementNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitLiteralExpression(LiteralExpressionNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitIdentifierExpression(IdentifierExpressionNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitParenthesizedExpression(ParenthesizedExpressionNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitCastExpression(CastExpressionNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitUnaryExpression(UnaryExpressionNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitBinaryExpression(BinaryExpressionNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitConditionalExpression(ConditionalExpressionNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitAssignmentExpression(AssignmentExpressionNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitInvocationExpression(InvocationExpressionNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitElementAccessExpression(ElementAccessExpressionNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitMemberAccessExpression(MemberAccessExpressionNode node)
        {
            DefaultVisit(node);
        }

        public virtual void VisitInitializerListExpression(InitializerListExpressionNode node)
        {
            DefaultVisit(node);
        }
    }
}