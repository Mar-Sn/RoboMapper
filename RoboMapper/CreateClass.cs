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
                            Block(l.Select(e =>
                            {
                                return ExpressionStatement(
                                    AssignmentExpression(
                                        SyntaxKind.SimpleAssignmentExpression,
                                        IdentifierName(Sanitize($"_mapper{e.Item1.FullName}to{e.Item2.FullName}")),
                                        IdentifierName(Sanitize($"mapper{e.Item1.FullName}to{e.Item2.FullName}"))));
                            }).ToArray()))
                        .WithSemicolonToken(
                            MissingToken(SyntaxKind.SemicolonToken)));
                }

                var fromToBody = ParseStatement(FromBody(@in, @out, "obj"));
                Debug.WriteLine(fromToBody);

                // Create a method
                var fromTo = MethodDeclaration(ParseTypeName(@out.FullName), "Map")
                    .AddModifiers(Token(SyntaxKind.PublicKeyword))
                    .AddParameterListParameters(Parameter(Identifier("obj")).WithType(ParseTypeName(@in.FullName)))
                    .WithBody(Block(fromToBody));

                var toFromBody = ParseStatement(FromBody(@out, @in, "obj"));
                members.Add(fromTo);
                
                Debug.WriteLine(toFromBody);

                var toFrom = MethodDeclaration(ParseTypeName(@in.FullName), "Map")
                    .AddModifiers(Token(SyntaxKind.PublicKeyword))
                    .AddParameterListParameters(Parameter(Identifier("obj")).WithType(ParseTypeName(@out.FullName)))
                    .WithBody(Block(toFromBody));
                
                members.Add(toFrom);

                classDeclaration = classDeclaration.AddMembers(members.ToArray());

                @namespace = @namespace.AddMembers(classDeclaration);
                
                return @namespace
                    .NormalizeWhitespace()
                    .ToFullString();
            });
        }

        private string Sanitize(string str)
        {
            return str.Replace(" ", "").Replace(".","");
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