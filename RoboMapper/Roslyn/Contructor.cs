using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace RoboMapper.Roslyn
{
    public class Contructor
    {
        private readonly string _className;
        private readonly List<Field> _innerMappers;

        public Contructor(string className, List<Field> innerMappers)
        {
            _className = className;
            _innerMappers = innerMappers;
        }

        public ConstructorDeclarationSyntax Generate()
        {
            var assignments = _innerMappers.Select(e => ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    IdentifierName(e.Name),
                    IdentifierName(e.Name.Replace("_",""))))
            ).ToList();                                  
            
            return ConstructorDeclaration(
                    Identifier(_className))
                .WithModifiers(
                    TokenList(
                        Token(SyntaxKind.PublicKeyword)))
                .WithParameterList(
                    ParameterList(
                        SeparatedList(_innerMappers.Select(e =>
                        {
                            return
                                Parameter(
                                        Identifier(e.Name.Replace("_", "")))
                                    .WithType(
                                        GenericName(
                                                Identifier("IMapper"))
                                            .WithTypeArgumentList(
                                                TypeArgumentList(
                                                    SeparatedList<TypeSyntax>(
                                                        new SyntaxNodeOrToken[]
                                                        {
                                                            IdentifierName(e.A.FullName!),
                                                            Token(SyntaxKind.CommaToken),
                                                            IdentifierName(e.B.FullName!)
                                                        }))));
                        }).ToArray())))
                .WithBody(
                    Block(assignments))
                .WithSemicolonToken(
                    MissingToken(SyntaxKind.SemicolonToken));
        }
    }
}