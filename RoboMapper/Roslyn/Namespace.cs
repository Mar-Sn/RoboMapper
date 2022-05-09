using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace RoboMapper.Roslyn
{
    public class Namespace
    {
        public List<Clazz> Classes { get; set; } = new List<Clazz>();
        public List<Using> Usings { get; set; } = new List<Using>();

        public NamespaceDeclarationSyntax Generate()
        {
            var @namespace = NamespaceDeclaration(ParseName("RoboMapper")).NormalizeWhitespace();
            @namespace.AddUsings(Usings.Select(e => e.Generate()).ToArray());
            @namespace.AddMembers(Classes.Select(e => e.Generate()).ToArray());
            return @namespace;
        }
    }
}