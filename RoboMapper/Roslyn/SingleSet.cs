using System;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace RoboMapper.Roslyn
{
    public class SingleSet
    {
        private readonly GenerateIMapper _generateMapper;
        public MemberInfo A { get; }

        private PropertyInfo PropertyInfoA => (A as PropertyInfo)!;
        public MemberInfo B { get; }

        private PropertyInfo PropertyInfoB => (B as PropertyInfo)!;
        
        public SingleSet(GenerateIMapper generateMapper, MemberInfo a, MemberInfo b)
        {
            _generateMapper = generateMapper;
            A = a;
            B = b;
        }

        public StatementSyntax Generate()
        {
            var canMapOneToOne = CanMapOneToOne();
            var canMapNullableOneToOne = CanMapNullableOneToOne(A, B);
            if (canMapOneToOne || canMapNullableOneToOne)
            {
                return SimpleOneToOne();
            }

            var isNullable = IsNullable();
            var fieldHasCustomerParser = FieldHasCustomerParser();
            if (isNullable && fieldHasCustomerParser)
            {
                return NullableWithMapper();
            }

            return WithMapper();
        }
        
        private bool FieldHasCustomerParser()
        {
            var customParserA = PropertyInfoA.GetCustomAttribute<MapIndex>()?.CustomParser != null;
            var customParserB = PropertyInfoB.GetCustomAttribute<MapIndex>()?.CustomParser != null;
            return customParserA || customParserB;
        }
        
        private bool IsNullable()
        {
            var aIsNullable = PropertyInfoA.PropertyType.IsGenericType && PropertyInfoA.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>);
            var bIsNullable = PropertyInfoB.PropertyType.IsGenericType && PropertyInfoB.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>);
            return aIsNullable || bIsNullable;
        }

        private bool CanMapNullableOneToOne(MemberInfo memberInfo, MemberInfo memberInfo1)
        {
            return CreateClass.IsNullable(memberInfo.DeclaringType!) 
                   && CreateClass.IsNullable(memberInfo1.DeclaringType!) 
                   && CanMapOneToOne();
        }

        private bool CanMapOneToOne()
        {
            return PropertyInfoA.PropertyType == PropertyInfoB.PropertyType;
        }

        private StatementSyntax WithMapper()
        {
            return ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("m"),
                        IdentifierName(B.Name)
                    ),
                    InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName(CreateClass.Sanitize($"_mapper{PropertyInfoA.PropertyType.Name}to{PropertyInfoB.PropertyType.Name}")),
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
                                            IdentifierName(A.Name)
                                        )
                                    )
                                )
                            )
                        )
                ));
        }

        private StatementSyntax NullableWithMapper()
        {
            var inArguments = PropertyInfoA.PropertyType.GetGenericArguments();
            if (inArguments.Length == 0)
            {
                inArguments = new[] { PropertyInfoA.PropertyType };
            }

            var outArguments = PropertyInfoB.PropertyType.GetGenericArguments();
            if (outArguments.Length == 0)
            {
                outArguments = new[] { PropertyInfoB.PropertyType };
            }

            var mapperName = CreateClass.Sanitize($"_mapper{A.Name}to{B.Name}");
            if (IsNullable() && PropertyInfoB.PropertyType != typeof(string))
            {
                return GenerateNullableAssignmentWithValue(A);
            }

            return GenerateNullableAssignment(A);
        }

        private ExpressionStatementSyntax GenerateNullableAssignment(MemberInfo field)
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
                                    IdentifierName(_generateMapper.GetMapper(A, B).Name),
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

        private ExpressionStatementSyntax GenerateNullableAssignmentWithValue(MemberInfo field)
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
                                    IdentifierName(_generateMapper.GetMapper(A, B).Name),
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

        private StatementSyntax SimpleOneToOne()
        {
            return ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("m"),
                        IdentifierName(B.Name)
                    ),
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("obj"),
                        IdentifierName(A.Name)
                    )
                )
            );
        }

        public override string ToString()
        {
            return Generate().NormalizeWhitespace().ToFullString();
        }
    }
}