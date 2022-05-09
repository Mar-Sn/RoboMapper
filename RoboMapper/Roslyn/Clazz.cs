using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace RoboMapper.Roslyn
{
    public class Clazz
    {
        public string Name { get; set; } = null!;
        
        public MapperInterface MapperInterface { get; set; }
        
        public List<Field> Fields { get; set; } = new List<Field>();
        
        public Contructor Contructor { get; set; }

        public List<Method> Methods { get; set; } = new List<Method>();


        public ClassDeclarationSyntax Generate()
        {
            var clazz= ClassDeclaration(Name);
            clazz.AddBaseListTypes(MapperInterface.Generate());
            return clazz;
        }
        
    }
}