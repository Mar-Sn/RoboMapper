using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace RoboMapper.Roslyn
{
    public class MapMethod
    {
        public Type ReturnType { get; }
        public Type Argument { get; }
        public List<SingleSet> SingleSets { get; }

        public MapMethod(Type returnType, Type argument, List<SingleSet> singleSets)
        {
            ReturnType = returnType;
            Argument = argument;
            SingleSets = singleSets;
        }

        public MethodDeclarationSyntax Generate()
        {
            var localList = new List<StatementSyntax>
            {
                LocalDeclarationStatement(
                    VariableDeclaration(
                            IdentifierName(
                                Identifier(
                                    TriviaList(),
                                    SyntaxKind.VarKeyword,
                                    "var",
                                    "var",
                                    TriviaList()
                                )
                            )
                        )
                        .WithVariables(
                            SingletonSeparatedList(
                                VariableDeclarator(
                                        Identifier("m")
                                    )
                                    .WithInitializer(
                                        EqualsValueClause(
                                            ObjectCreationExpression(
                                                    IdentifierName(ReturnType.FullName)
                                                )
                                                .WithArgumentList(
                                                    ArgumentList()
                                                )
                                        )
                                    )
                            )
                        )
                )
            };
            
            localList.AddRange(SingleSets.Select(e => e.Generate()));
            
            return MethodDeclaration(
                    IdentifierName(ReturnType.FullName),
                    Identifier("Map")
                )
                .WithModifiers(
                    TokenList(
                        Token(SyntaxKind.PublicKeyword)
                    )
                )
                .WithParameterList(
                    ParameterList(
                        SingletonSeparatedList(
                            Parameter(
                                    Identifier("obj")
                                )
                                .WithType(
                                    IdentifierName(Argument.FullName)
                                )
                        )
                    )
                )
                .WithBody(
                    Block(
                        localList
                    )
                );
        }
    }
}