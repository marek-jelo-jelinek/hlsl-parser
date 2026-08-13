using HlslParser.Lexing;
using HlslParser.Syntax;

namespace HlslParser.Parsing
{
    public partial class Parser
    {
        private TypedefDeclarationNode ParseTypedefDeclaration(int start)
        {
            Advance();
            var underlyingType = ParseTypeName();
            var aliasToken = Expect(HlslTokenKind.Identifier, "typedef alias name");
            var ranks = ParseArrayRanks();
            Expect(HlslTokenKind.Semicolon, "';'");
            return new TypedefDeclarationNode(SpanFrom(start), underlyingType, aliasToken.Text, ranks);
        }
    }
}