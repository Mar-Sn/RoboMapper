using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace RoboMapper.Roslyn
{
    public class Using
    {
        public string Name { get; } = null!;

        public Using(string name)
        {
            Name = name;
        }

        public UsingDirectiveSyntax Generate()
        {
            return UsingDirective(ParseName(Name));
        }
    }
}