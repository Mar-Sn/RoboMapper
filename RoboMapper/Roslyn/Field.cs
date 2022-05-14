using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace RoboMapper.Roslyn
{
    public class Field
    {
        public Type A { get; set; }
        public Type B { get; set; }

        public string Name => RoboHelper.Sanitize($"_mapper{A.FullName}to{B.FullName}");

        public Field(Type a, Type b)
        {
            A = a;
            B = b;
        }

        public FieldDeclarationSyntax Generate()
        {
            return FieldDeclaration(
                VariableDeclaration(
                        GenericName(
                                Identifier("IMapper"))
                            .WithTypeArgumentList(
                                TypeArgumentList(
                                    SeparatedList<TypeSyntax>(
                                        new SyntaxNodeOrToken[]
                                        {
                                            IdentifierName(A.FullName!),
                                            Token(SyntaxKind.CommaToken),
                                            IdentifierName(B.FullName!)
                                        }))))
                    .WithVariables(
                        SingletonSeparatedList(
                            VariableDeclarator(
                                Identifier(Name)))));
        }

        public override string ToString() => Name;
    }
}