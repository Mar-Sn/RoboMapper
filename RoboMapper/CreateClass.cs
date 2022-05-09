#nullable enable
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
        
        public Task<(string, List<Type>)> Generate(Type @in, Type @out)
        {
            return Task.Run(() =>
            {
                var members = new List<MemberDeclarationSyntax>();
                var className = "Mapped" + Guid.NewGuid().ToString().Replace("-", "");

                var innerMappers = GetInnerMappers(@out, @in);
                
                var @namespace = NamespaceDeclaration(ParseName("RoboMapper")).NormalizeWhitespace();
                @namespace = @namespace.AddUsings(UsingDirective(ParseName("System")));
                foreach (var innerMapper in innerMappers)
                {
                    @namespace = @namespace.AddUsings(UsingDirective(ParseName(innerMapper.Item1.Assembly.GetName().Name)));
                    @namespace = @namespace.AddUsings(UsingDirective(ParseName(innerMapper.Item2.Assembly.GetName().Name)));
                }
                var classDeclaration = ClassDeclaration(className);
                classDeclaration = classDeclaration.AddModifiers(Token(SyntaxKind.PublicKeyword));

                classDeclaration = classDeclaration.AddBaseListTypes(
                    SimpleBaseType(ParseTypeName($"IMapper<{@in.FullName}, {@out.FullName}>")));

                if (innerMappers.Any())
                {
                    AddFields(members, innerMappers);

                    var uniqueFields = innerMappers.Select(e => $"mapper{e.Item1.Name}to{e.Item2.Name}").ToList();
                    uniqueFields.AddRange(innerMappers.Select(e => $"mapper{e.Item2.Name}to{e.Item1.Name}"));
                    uniqueFields = uniqueFields.Distinct().ToList();
                    
                    var assignments = uniqueFields.Select(e => ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            IdentifierName(Sanitize($"_{e}")),
                            IdentifierName(Sanitize(e))))
                    ).ToList();

                    AddConstructor(members, className, innerMappers, assignments);
                }

                members.Add(GenMethod(@in, @out, "obj"));
                members.Add(GenMethod(@out, @in, "obj"));

                classDeclaration = classDeclaration.AddMembers(members.ToArray());

                @namespace = @namespace.AddMembers(classDeclaration);

                var types = innerMappers.Select(e => e.Item1).ToList();
                types.AddRange(innerMappers.Select(e => e.Item2));
                return (@namespace
                    .NormalizeWhitespace()
                    .ToFullString(), types);
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
                                        Identifier(Sanitize($"mapper{e.Item1.Name}to{e.Item2.Name}")))
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
                                    Identifier(Sanitize($"_mapper{e.Item1.Name}to{e.Item2.Name}"))))));
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
                                    Identifier(Sanitize($"_mapper{e.Item2.Name}to{e.Item1.Name}"))))));
            }));
        }

        private string Sanitize(string str)
        {
            return str
                .Replace(" ", "")
                .Replace("`", "")
                .Replace(".", "");
        }

        private List<(Type, Type)> GetInnerMappers(Type @out, Type @in)
        {
            void InnerForeach(Type _in, Type _out, MemberInfo field, List<(Type, Type)> valueTuples)
            {
                if (field.GetCustomAttributes<MapIgnore>().Any()) return;
                var mapIndex = field.GetCustomAttributes<MapIndex>().ToList();
                if (!mapIndex.Any()) throw new Exception($"field {field.Name} of class {_in.FullName} has no index!");

                var propertyInfo = field as PropertyInfo;
                if (mapIndex == null || propertyInfo == null)
                {
                    throw new Exception("fields should have mapIndex present if class is defined Mappable");
                }


                if (CanMapOneToOne(propertyInfo.PropertyType) == false)
                {
                    //this is not a primitive
                    try
                    {
                        // if (IsNullable())
                        // {
                        //     //just a simple check to see if the type is of Imapper
                        //     // var parserType = mapIndex.First().CustomParser;
                        //     // var interfaces = parserType.GetInterfaces();
                        //     // if (interfaces.Any(e => e.FullName.StartsWith("RoboMapper.IMapper")))
                        //     // {
                        //     //     //TODO is there a better way to check?
                        //     //     //just load the in and out of the parser. No checking required as its enforced by interface
                        //     //     var types = interfaces.First(e => e.FullName.StartsWith("RoboMapper.IMapper")).GenericTypeArguments;
                        //     //     list.Add((types[0], types[1]));
                        //     // }
                        //     // else
                        //     // {
                        //     //     throw new ArgumentException("a parser can only be of type IMapper");
                        //     // }
                        // }
                        // else
                        // {
                        var fieldOut = _out.GetMembers().Where(e => e is PropertyInfo).First(e => e.GetCustomAttribute<MapIndex>()?.IndexName == mapIndex.First().IndexName) as PropertyInfo;

                        var propertyInfoIn = propertyInfo.PropertyType;
                        var propertyInfoOut = fieldOut!.PropertyType;

                        if (IsNullable(propertyInfoIn))
                        {
                            var genericArg = propertyInfoIn.GetGenericArguments().FirstOrDefault();
                            if (genericArg != null)
                            {
                                propertyInfoIn = genericArg;
                            }
                        }

                        if (IsNullable(propertyInfoOut))
                        {
                            var genericArg = propertyInfoOut.GetGenericArguments().FirstOrDefault();
                            if (genericArg != null)
                            {
                                propertyInfoOut = genericArg;
                            }
                        }
                        
                        valueTuples.Add((propertyInfoIn, propertyInfoOut)!);
                        //}
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"Unable to map {mapIndex.First().IndexName} of object {_out.FullName} <-> {_in.FullName}", e);
                    }
                }
            }

            var list = new List<(Type, Type)>();
            foreach (var field in @out.GetMembers().Where(e => e is PropertyInfo))
            {
                InnerForeach(@out, @in, field, list);
            }

            foreach (var field in @in.GetMembers().Where(e => e is PropertyInfo))
            {
                InnerForeach(@in, @out, field, list);
            }

            return list.DistinctBy(e => e.Item1.FullName).DistinctBy(e => e.Item2.FullName).ToList();
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
                if (field.GetCustomAttributes<MapIgnore>().Any()) continue;
                var mapIndex = field.GetCustomAttributes<MapIndex>().ToList();
                if (!mapIndex.Any()) throw new Exception($"field {field.Name} of class {@in.FullName} has no index!");

                var propertyInfo = field as PropertyInfo;
                if (mapIndex == null || propertyInfo == null)
                {
                    throw new Exception("fields should have mapIndex present if class is defined Mappable");
                }

                var fieldOut = @in.GetMembers().Where(e => e is PropertyInfo).First(e => e.GetCustomAttribute<MapIndex>()?.IndexName == mapIndex.First().IndexName) as PropertyInfo;
                Console.Write($"f {propertyInfo.Name} <-> {fieldOut.Name}");

                if (CanMapOneToOne(propertyInfo.PropertyType) && CanMapOneToOne(fieldOut.PropertyType)
                    || IsNullableOneToOne(propertyInfo.PropertyType, fieldOut.PropertyType))
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
                else if (IsNullable(propertyInfo.PropertyType, fieldOut.PropertyType) && FieldHasCustomerParser(propertyInfo))
                {
                    var inArguments = propertyInfo.PropertyType.GetGenericArguments();
                    if (inArguments.Length == 0)
                    {
                        inArguments = new[] { propertyInfo.PropertyType };
                    }

                    var outArguments = fieldOut.PropertyType.GetGenericArguments();
                    if (outArguments.Length == 0)
                    {
                        outArguments = new[] { fieldOut.PropertyType };
                    }

                    var mapperName = $"_mapper{inArguments[0].Name}to{outArguments[0].Name}";
                    if (IsNullable(fieldOut.PropertyType) && fieldOut.PropertyType != typeof(string))
                    {
                        localList.Add(GenerateNullableAssignmentWithValue(field, mapperName));
                    }
                    else
                    {
                        localList.Add(GenerateNullableAssignment(field, mapperName));
                    }
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
                                        IdentifierName(Sanitize($"_mapper{propertyInfo.PropertyType.Name}to{fieldOut!.PropertyType.Name}")),
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

                Console.WriteLine("");
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

        private static ExpressionStatementSyntax GenerateNullableAssignment(MemberInfo field, string mapperName)
        {
            return ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("m"),
                        IdentifierName(field.Name)),
                    ConditionalExpression(
                        BinaryExpression(
                            SyntaxKind.NotEqualsExpression,
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName("obj"),
                                IdentifierName(field.Name)),
                            LiteralExpression(
                                SyntaxKind.NullLiteralExpression)),
                        InvocationExpression(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(mapperName),
                                    IdentifierName("Map")))
                            .WithArgumentList(
                                ArgumentList(
                                    SingletonSeparatedList(
                                        Argument(
                                            MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                IdentifierName("obj"),
                                                IdentifierName(field.Name)))))),
                        LiteralExpression(
                            SyntaxKind.NullLiteralExpression)))
            );
        }
        
        private static ExpressionStatementSyntax GenerateNullableAssignmentWithValue(MemberInfo field, string mapperName)
        {
            return ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("m"),
                        IdentifierName(field.Name)),
                    ConditionalExpression(
                        BinaryExpression(
                            SyntaxKind.NotEqualsExpression,
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName("obj"),
                                IdentifierName(field.Name)),
                            LiteralExpression(
                                SyntaxKind.NullLiteralExpression)),
                        InvocationExpression(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(mapperName),
                                    IdentifierName("Map")))
                            .WithArgumentList(
                                ArgumentList(
                                    SingletonSeparatedList(
                                        Argument(
                                            MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                MemberAccessExpression(
                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                    IdentifierName("obj"),
                                                    IdentifierName(field.Name)),
                                                IdentifierName("Value")))))),
                        LiteralExpression(
                            SyntaxKind.NullLiteralExpression)))
            );
        }

        private bool IsNullableOneToOne(Type a, Type b)
        {
            var nullable = IsNullable(a, b);
            var aGenerics = a.GetGenericArguments();
            var bGenerics = b.GetGenericArguments();
            Console.Write($" n:{nullable}");
            var typeMatch = aGenerics.FirstOrDefault() == bGenerics.FirstOrDefault();
            Console.Write($" t:{typeMatch}");
            return nullable && typeMatch;
        }

        private bool FieldHasCustomerParser(PropertyInfo type)
        {
            var customParser = type.GetCustomAttribute<MapIndex>()?.CustomParser != null;
            Console.Write($" c:{customParser}");
            return customParser;
        }

        private bool IsNullable(Type a)
        {
            var aIsNullable = a.IsGenericType && a.GetGenericTypeDefinition() == typeof(Nullable<>) || a == typeof(string);
            return aIsNullable;
        }

        private bool IsNullable(Type a, Type b)
        {
            var aIsNullable = a.IsGenericType && a.GetGenericTypeDefinition() == typeof(Nullable<>) || a == typeof(string);
            var bIsNullable = b.IsGenericType && b.GetGenericTypeDefinition() == typeof(Nullable<>) || b == typeof(string);
            ;
            return aIsNullable && bIsNullable;
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
                                                         || type == typeof(float)
                                                         || BasicNullableCheck(type);

        private static bool BasicNullableCheck(Type type) => type == typeof(int?)
                                                             || type == typeof(double?)
                                                             || type == typeof(DateTime?)
                                                             || type == typeof(DateTimeOffset?)
                                                             || type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>) && Nullable.GetUnderlyingType(type) == typeof(string)
                                                             || type == typeof(bool?)
                                                             || type == typeof(char?)
                                                             || type == typeof(decimal?)
                                                             || type == typeof(long?)
                                                             || type == typeof(sbyte?)
                                                             || type == typeof(short?)
                                                             || type == typeof(uint?)
                                                             || type == typeof(ulong?)
                                                             || type == typeof(ushort?)
                                                             || type == typeof(float?);
    }
}