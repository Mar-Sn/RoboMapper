using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace RoboMapper
{
    public class CreateClass
    {
        public Task<string> Generate(Type @in, Type @out)
        {
            return Task.Run(() =>
            {
                var members = new List<MemberDeclarationSyntax>();
                var className = "Mapped" + Guid.NewGuid().ToString().Replace("-", "");

                var innerMappers = GetInnerMappers(@out, @in);
                var @namespace = NamespaceDeclaration(ParseName("RoboMapper")).NormalizeWhitespace();
                @namespace = @namespace.AddUsings(UsingDirective(ParseName("System")));
                var classDeclaration = ClassDeclaration(className);
                classDeclaration = classDeclaration.AddModifiers(Token(SyntaxKind.PublicKeyword));

                classDeclaration = classDeclaration.AddBaseListTypes(
                    SimpleBaseType(ParseTypeName($"IMapper<{@in.FullName}, {@out.FullName}>")));

                if (innerMappers.Any())
                {
                    AddFields(members, innerMappers);

                    var assignments = innerMappers.Select(e => ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            IdentifierName(Sanitize($"_mapper{e.Item1.FullName}to{e.Item2.FullName}")),
                            IdentifierName(Sanitize($"mapper{e.Item1.FullName}to{e.Item2.FullName}"))))
                    ).ToList();
                    
                    assignments.AddRange(innerMappers.Select(e => ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            IdentifierName(Sanitize($"_mapper{e.Item2.FullName}to{e.Item1.FullName}")),
                            IdentifierName(Sanitize($"mapper{e.Item1.FullName}to{e.Item2.FullName}"))))
                    )); 

                    AddConstructor(members, className, innerMappers, assignments);
                }

                members.Add(GenMethod(@in, @out, "obj"));
                members.Add(GenMethod(@out, @in, "obj"));

                classDeclaration = classDeclaration.AddMembers(members.ToArray());

                @namespace = @namespace.AddMembers(classDeclaration);

                return @namespace
                    .NormalizeWhitespace()
                    .ToFullString();
            });
        }

        private void AddConstructor(List<MemberDeclarationSyntax> members, string className, List<(Type, Type)> innerMappers, List<ExpressionStatementSyntax> assignments)
        {
            members.Add(ConstructorDeclaration(
                    Identifier(className))
                .WithModifiers(
                    TokenList(
                        Token(SyntaxKind.PublicKeyword)))
                .WithParameterList(
                    ParameterList(
                        SeparatedList(innerMappers.Select(e =>
                        {
                            return
                                Parameter(
                                        Identifier(Sanitize($"mapper{e.Item1.FullName}to{e.Item2.FullName}")))
                                    .WithType(
                                        GenericName(
                                                Identifier("IMapper"))
                                            .WithTypeArgumentList(
                                                TypeArgumentList(
                                                    SeparatedList<TypeSyntax>(
                                                        new SyntaxNodeOrToken[]
                                                        {
                                                            IdentifierName(e.Item1.FullName!),
                                                            Token(SyntaxKind.CommaToken),
                                                            IdentifierName(e.Item2.FullName!)
                                                        }))));
                        }).ToArray())))
                .WithBody(
                    Block(assignments))
                .WithSemicolonToken(
                    MissingToken(SyntaxKind.SemicolonToken)));
        }

        private void AddFields(List<MemberDeclarationSyntax> members, List<(Type, Type)> l)
        {
            members.AddRange(l.Select(e =>
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
                                                IdentifierName(e.Item1.FullName!),
                                                Token(SyntaxKind.CommaToken),
                                                IdentifierName(e.Item2.FullName!)
                                            }))))
                        .WithVariables(
                            SingletonSeparatedList(
                                VariableDeclarator(
                                    Identifier(Sanitize($"_mapper{e.Item1.FullName}to{e.Item2.FullName}"))))));
            }));

            members.AddRange(l.Select(e =>
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
                                                IdentifierName(e.Item1.FullName!),
                                                Token(SyntaxKind.CommaToken),
                                                IdentifierName(e.Item2.FullName!)
                                            }))))
                        .WithVariables(
                            SingletonSeparatedList(
                                VariableDeclarator(
                                    Identifier(Sanitize($"_mapper{e.Item2.FullName}to{e.Item1.FullName}"))))));
            }));
        }

        private string Sanitize(string str)
        {
            return str.Replace(" ", "").Replace(".", "");
        }

        private List<(Type, Type)> GetInnerMappers(Type @out, Type @in)
        {
            var list = new List<(Type, Type)>();
            foreach (var field in @out.GetMembers().Where(e => e is PropertyInfo))
            {
                if (field.GetCustomAttributes<MapIgnore>().Any()) continue;
                var mapIndex = field.GetCustomAttributes<MapIndex>().ToList();
                if (!mapIndex.Any()) throw new Exception($"field {field.Name} of class {@out.FullName} has no index!");

                var propertyInfo = field as PropertyInfo;
                if (mapIndex == null || propertyInfo == null)
                {
                    throw new Exception("fields should have mapIndex present if class is defined Mappable");
                }

                if (CanMapOneToOne(propertyInfo.PropertyType) == false)
                {
                    //this is not a primitive

                    var fieldOut = @in.GetMembers().Where(e => e is PropertyInfo).First(e => e.GetCustomAttribute<MapIndex>().IndexName == mapIndex.First().IndexName) as PropertyInfo;

                    list.Add((propertyInfo.PropertyType, fieldOut!.PropertyType));
                }
            }

            return list;
        }


        private MethodDeclarationSyntax GenMethod(Type @in, Type @out, string inputParameter)
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
                                                    IdentifierName(@out.FullName!)
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

            foreach (var field in @out.GetMembers().Where(e => e is PropertyInfo))
            {
                if(field.GetCustomAttributes<MapIgnore>().Any()) continue;
                var mapIndex = field.GetCustomAttributes<MapIndex>().ToList();
                if (!mapIndex.Any()) throw new Exception($"field {field.Name} of class {@in.FullName} has no index!");

                var propertyInfo = field as PropertyInfo;
                if (mapIndex == null || propertyInfo == null)
                {
                    throw new Exception("fields should have mapIndex present if class is defined Mappable");
                }

                var fieldOut = @in.GetMembers().Where(e => e is PropertyInfo).First(e => e.GetCustomAttribute<MapIndex>().IndexName == mapIndex.First().IndexName) as PropertyInfo;


                if (CanMapOneToOne(propertyInfo.PropertyType))
                {
                    localList.Add(
                        ExpressionStatement(
                            AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName("m"),
                                    IdentifierName(field.Name)
                                ),
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName("obj"),
                                    IdentifierName(fieldOut!.Name)
                                )
                            )
                        ));
                }
                else
                {
                    localList.Add(ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName("m"),
                                IdentifierName(field.Name)
                            ),
                            InvocationExpression(
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        IdentifierName(Sanitize($"_mapper{propertyInfo.PropertyType.FullName}to{fieldOut!.PropertyType.FullName}")),
                                        IdentifierName("Map")
                                    )
                                )
                                .WithArgumentList(
                                    ArgumentList(
                                        SingletonSeparatedList(
                                            Argument(
                                                MemberAccessExpression(
                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                    IdentifierName("obj"),
                                                    IdentifierName(fieldOut.Name)
                                                )
                                            )
                                        )
                                    )
                                )
                        )));
                }
            }

            localList.Add(ReturnStatement(
                IdentifierName("m")
            ));

            return MethodDeclaration(
                    IdentifierName(@out.FullName),
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
                                    Identifier(inputParameter)
                                )
                                .WithType(
                                    IdentifierName(@in.FullName!)
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

        private static bool CanMapOneToOne(Type type) => type == typeof(int)
                                                         || type == typeof(double)
                                                         || type == typeof(DateTime)
                                                         || type == typeof(DateTimeOffset)
                                                         || type == typeof(string)
                                                         || type == typeof(bool)
                                                         || type == typeof(char)
                                                         || type == typeof(decimal)
                                                         || type == typeof(long)
                                                         || type == typeof(sbyte)
                                                         || type == typeof(short)
                                                         || type == typeof(uint)
                                                         || type == typeof(ulong)
                                                         || type == typeof(ushort)
                                                         || type == typeof(float);
    }
}