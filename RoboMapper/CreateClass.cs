using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace RoboMapper
{
    public class CreateClass
    {
        public CreateClass()
        {
        }

        public Task<string> Generate(Type @in, Type @out)
        {
            return Task.Run(() =>
            {
                var members = new List<MemberDeclarationSyntax>();
                var className = "Mapped" + Guid.NewGuid().ToString().Replace("-", "");

                var l = GetInnerMappers(@out, @in);
                var @namespace = NamespaceDeclaration(ParseName("RoboMapper")).NormalizeWhitespace();
                @namespace = @namespace.AddUsings(UsingDirective(ParseName("System")));
                var classDeclaration = ClassDeclaration(className);
                classDeclaration = classDeclaration.AddModifiers(Token(SyntaxKind.PublicKeyword));

                classDeclaration = classDeclaration.AddBaseListTypes(
                    SimpleBaseType(ParseTypeName($"IMapper<{@in.FullName}, {@out.FullName}>")));

                if (l.Any())
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
                                                        IdentifierName(e.Item1.FullName),
                                                        Token(SyntaxKind.CommaToken),
                                                        IdentifierName(e.Item2.FullName)
                                                    }))))
                                .WithVariables(
                                    SingletonSeparatedList<VariableDeclaratorSyntax>(
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
                                                        IdentifierName(e.Item1.FullName),
                                                        Token(SyntaxKind.CommaToken),
                                                        IdentifierName(e.Item2.FullName)
                                                    }))))
                                .WithVariables(
                                    SingletonSeparatedList<VariableDeclaratorSyntax>(
                                        VariableDeclarator(
                                            Identifier(Sanitize($"_mapper{e.Item2.FullName}to{e.Item1.FullName}"))))));
                    }));

                    var assignments = l.Select(e => ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            IdentifierName(Sanitize($"_mapper{e.Item1.FullName}to{e.Item2.FullName}")),
                            IdentifierName(Sanitize($"mapper{e.Item1.FullName}to{e.Item2.FullName}"))))
                    ).ToList();
                    
                    assignments.AddRange(l.Select(e => ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            IdentifierName(Sanitize($"_mapper{e.Item2.FullName}to{e.Item1.FullName}")),
                            IdentifierName(Sanitize($"mapper{e.Item1.FullName}to{e.Item2.FullName}"))))
                    )); 

                    members.Add(ConstructorDeclaration(
                            Identifier(className))
                        .WithModifiers(
                            TokenList(
                                Token(SyntaxKind.PublicKeyword)))
                        .WithParameterList(
                            ParameterList(
                                SeparatedList(l.Select(e =>
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
                                                                    IdentifierName(e.Item1.FullName),
                                                                    Token(SyntaxKind.CommaToken),
                                                                    IdentifierName(e.Item2.FullName)
                                                                }))));
                                }).ToArray())))
                        .WithBody(
                            Block(assignments))
                        .WithSemicolonToken(
                            MissingToken(SyntaxKind.SemicolonToken)));
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

        private string Sanitize(string str)
        {
            return str.Replace(" ", "").Replace(".", "");
        }

        private List<(Type, Type)> GetInnerMappers(Type @out, Type @in)
        {
            var list = new List<(Type, Type)>();
            foreach (var field in @out.GetMembers().Where(e => e is PropertyInfo))
            {
                var mapIndex = field.GetCustomAttributes<MapIndex>();
                if (!mapIndex.Any()) throw new Exception($"field {field.Name} of class {@out.FullName} has no index!");

                var propertyInfo = field as PropertyInfo;
                if (mapIndex == null || propertyInfo == null)
                {
                    throw new Exception("fields should have mapIndex present if class is defined Mappable");
                }

                if (propertyInfo.PropertyType.BaseType == typeof(object))
                {
                    //this is not a primitive

                    var fieldOut = @in.GetMembers().Where(e => e is PropertyInfo).First(e => e.GetCustomAttribute<MapIndex>().IndexName == mapIndex.First().IndexName) as PropertyInfo;

                    list.Add((propertyInfo.PropertyType, fieldOut.PropertyType));
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
                            SingletonSeparatedList<VariableDeclaratorSyntax>(
                                VariableDeclarator(
                                        Identifier("m")
                                    )
                                    .WithInitializer(
                                        EqualsValueClause(
                                            ObjectCreationExpression(
                                                    IdentifierName(@out.FullName)
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
                var mapIndex = field.GetCustomAttributes<MapIndex>();
                if (!mapIndex.Any()) throw new Exception($"field {field.Name} of class {@in.FullName} has no index!");

                var propertyInfo = field as PropertyInfo;
                if (mapIndex == null || propertyInfo == null)
                {
                    throw new Exception("fields should have mapIndex present if class is defined Mappable");
                }

                var fieldOut = @in.GetMembers().Where(e => e is PropertyInfo).First(e => e.GetCustomAttribute<MapIndex>().IndexName == mapIndex.First().IndexName) as PropertyInfo;


                if (propertyInfo.PropertyType == typeof(int)
                    || propertyInfo.PropertyType == typeof(double)
                    || propertyInfo.PropertyType == typeof(DateTime)
                    || propertyInfo.PropertyType == typeof(DateTimeOffset)
                    || propertyInfo.PropertyType == typeof(string)
                    || propertyInfo.PropertyType == typeof(bool)
                    || propertyInfo.PropertyType == typeof(char)
                    || propertyInfo.PropertyType == typeof(decimal)
                    || propertyInfo.PropertyType == typeof(long)
                    || propertyInfo.PropertyType == typeof(sbyte)
                    || propertyInfo.PropertyType == typeof(short)
                    || propertyInfo.PropertyType == typeof(uint)
                    || propertyInfo.PropertyType == typeof(ulong)
                    || propertyInfo.PropertyType == typeof(ushort)
                    || propertyInfo.PropertyType == typeof(float))
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
                                    IdentifierName(fieldOut.Name)
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
                                        IdentifierName(Sanitize($"_mapper{propertyInfo.PropertyType.FullName}to{fieldOut.PropertyType.FullName}")),
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
                        SingletonSeparatedList<ParameterSyntax>(
                            Parameter(
                                    Identifier(inputParameter)
                                )
                                .WithType(
                                    IdentifierName(@in.FullName)
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

        private string FromBody(Type @in, Type @out, string inputParameter)
        {
            var strBuilder = new StringBuilder();
            var inMappable = @in.GetCustomAttributes<Mappable>();
            if (!inMappable.Any()) throw new Exception("This is not a mappable");

            strBuilder.Append("{" + $"var @out = new {@out.FullName}(); ");

            foreach (var field in @in.GetMembers().Where(e => e is PropertyInfo))
            {
                var mapIndex = field.GetCustomAttributes<MapIndex>();
                if (!mapIndex.Any()) throw new Exception($"field {field.Name} of class {@in.FullName} has no index!");

                var propertyInfo = field as PropertyInfo;
                if (mapIndex == null || propertyInfo == null)
                {
                    throw new Exception("fields should have mapIndex present if class is defined Mappable");
                }
            }

            foreach (var field in @out.GetMembers().Where(e => e is PropertyInfo))
            {
                var mapIndex = field.GetCustomAttributes<MapIndex>();
                if (!mapIndex.Any()) throw new Exception($"field {field.Name} of class {@in.FullName} has no index!");

                var propertyInfo = field as PropertyInfo;
                if (mapIndex == null || propertyInfo == null)
                {
                    throw new Exception("fields should have mapIndex present if class is defined Mappable");
                }

                var fieldOut = @in.GetMembers().Where(e => e is PropertyInfo).First(e => e.GetCustomAttribute<MapIndex>().IndexName == mapIndex.First().IndexName);

                strBuilder.Append($"@out.{field.Name} = obj.{fieldOut.Name};");
            }

            strBuilder.AppendLine(" return @out;}");
            return strBuilder.ToString();
        }
    }
}