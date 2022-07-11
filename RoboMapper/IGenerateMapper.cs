using System;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoboMapper.Roslyn;

namespace RoboMapper
{
    public interface IGenerateMapper
    {
        public Type A { get; }
        public Type B { get; }
        
        public ClassDeclarationSyntax Generate();
    }
}