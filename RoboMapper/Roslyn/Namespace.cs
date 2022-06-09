using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace RoboMapper.Roslyn
{
    public class Namespace
    {
        private const string Name = "RoboMapper";
        public List<GenerateIMapper> Classes { get; set; } = new List<GenerateIMapper>();

        public HashSet<Type> AllKnownTypes = new HashSet<Type>();
        private List<Using> Usings { get; } = new List<Using>();

        public NamespaceDeclarationSyntax Generate()
        {
            var namespaceDeclarationSyntax = NamespaceDeclaration(ParseName(Name));
            var classes = Classes.Select(e => e.Generate());
            namespaceDeclarationSyntax = namespaceDeclarationSyntax.AddMembers(classes.ToArray());
            foreach (var allKnownType in AllKnownTypes)
            {
                if (allKnownType != null) Usings.Add(new Using(allKnownType.Namespace));
            }
            namespaceDeclarationSyntax = namespaceDeclarationSyntax.AddUsings(Usings.Select(e => e.Generate()).ToArray());
            return namespaceDeclarationSyntax;
        }

        public override string ToString() => Name;

        public void AddUsing(string name)
        {
            Usings.Add(new Using(name));
        }
    }
}