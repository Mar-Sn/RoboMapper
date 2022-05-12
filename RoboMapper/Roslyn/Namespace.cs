using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace RoboMapper.Roslyn
{
    public class Namespace
    {
        public List<GenerateIMapper> Classes { get; set; } = new List<GenerateIMapper>();
        public List<Using> Usings { get; set; } = new List<Using>();

        private NamespaceDeclarationSyntax? _namespaceDeclarationSyntax;
        public NamespaceDeclarationSyntax Generate()
        {
            if (_namespaceDeclarationSyntax != null) return _namespaceDeclarationSyntax;
            
            _namespaceDeclarationSyntax = NamespaceDeclaration(ParseName("RoboMapper"));
            _namespaceDeclarationSyntax = _namespaceDeclarationSyntax.AddUsings(Usings.Select(e => e.Generate()).ToArray());
            var classes = Classes.Select(e => e.Generate());
            var classesStrings = classes.Select(e => e.ToFullString());
            _namespaceDeclarationSyntax = _namespaceDeclarationSyntax.AddMembers(classes.ToArray());

            return _namespaceDeclarationSyntax;
        }

        public override string ToString()
        {
            return Generate().NormalizeWhitespace().ToFullString();
        }
    }
}