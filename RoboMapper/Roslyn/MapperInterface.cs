using System;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace RoboMapper.Roslyn
{
    public class MapperInterface
    {
        public Type A { get; set; }
        public Type B { get; set; }
        
        public SimpleBaseTypeSyntax Generate()
        {
            return SimpleBaseType(ParseTypeName($"IMapper<{A.FullName}, {B.FullName}>"));
        }
    }
}