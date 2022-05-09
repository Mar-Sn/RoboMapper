using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace RoboMapper.Roslyn
{
    public class Using
    {
        public string Name { get; set; } = null!;

        public UsingDirectiveSyntax Generate()
        {
            return UsingDirective(ParseName(Name));
        }
    }
}