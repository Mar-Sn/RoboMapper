using System.Reflection;
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
            if (CanMapOneToOne(A, B) || CanMapNullableOneToOne(A, B))
            {
                return SimpleOneToOne();
            }

            if (CreateClass.IsNullable(A.DeclaringType!, B.DeclaringType!) && CreateClass.FieldHasCustomerParser(B.DeclaringType!))
            {
                return NullableWithMapper();
            }

            return WithMapper();
        }

        private bool CanMapNullableOneToOne(MemberInfo memberInfo, MemberInfo memberInfo1)
        {
            return CreateClass.IsNullable(memberInfo.DeclaringType!) 
                   && CreateClass.IsNullable(memberInfo1.DeclaringType!) 
                   && CanMapOneToOne(memberInfo, memberInfo1);
        }

        private bool CanMapOneToOne(MemberInfo memberInfo, MemberInfo memberInfo1)
        {
            return memberInfo.DeclaringType == memberInfo1.DeclaringType;
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
            if (CreateClass.IsNullable(A.DeclaringType!) && PropertyInfoB.PropertyType != typeof(string))
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
    }
}