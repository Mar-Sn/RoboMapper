using System;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace RoboMapper.Roslyn
{
    public class SingleSet
    {
        private readonly GenerateIMapper _generateMapper;
        public MemberInfo In { get; }

        private PropertyInfo PropertyInfoIn => (In as PropertyInfo)!;
        public MemberInfo Out { get; }

        private PropertyInfo PropertyInfoOut => (Out as PropertyInfo)!;
        
        public SingleSet(GenerateIMapper generateMapper, MemberInfo @in, MemberInfo @out)
        {
            _generateMapper = generateMapper;
            In = @in;
            Out = @out;
        }

        public StatementSyntax Generate()
        {
            var types = GetBaseTypeIfNullable();
            _generateMapper.Namespace.AllKnownTypes.Add(types.Item1.Namespace);
            _generateMapper.Namespace.AllKnownTypes.Add(types.Item2.Namespace);
            var canMapOneToOne = CanMapOneToOne();
            var canMapNullableOneToOne = CanMapNullableOneToOne(In, Out);
            if (canMapOneToOne || canMapNullableOneToOne)
            {
                RoboMapper.Logger.LogDebug("Class: {_generateMapper.Name} Can map field {A.Name} to {B.Name} one-to-one", _generateMapper.Name, In.Name, Out.Name);
                return SimpleOneToOne();
            }

            var isNullable = IsNullable();
            var fieldHasCustomerParser = FieldHasCustomerParser();
            if (isNullable && fieldHasCustomerParser)
            {
                RoboMapper.Logger.LogDebug("Class: {_generateMapper.Name} Field has custom parser {A.Name} to {B.Name}", _generateMapper.Name, In.Name, Out.Name);

                return NullableWithMapper();
            }

            return WithMapper();
        }
        
        private bool FieldHasCustomerParser()
        {
            var customParserA = PropertyInfoIn.GetCustomAttribute<MapIndex>()?.CustomParser != null;
            var customParserB = PropertyInfoOut.GetCustomAttribute<MapIndex>()?.CustomParser != null;
            return customParserA || customParserB;
        }
        
        private bool IsNullable()
        {
            var aIsNullable = PropertyInfoIn.PropertyType.IsGenericType && PropertyInfoIn.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>);
            var bIsNullable = PropertyInfoOut.PropertyType.IsGenericType && PropertyInfoOut.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>);
            return aIsNullable || bIsNullable;
        }

        private bool CanMapNullableOneToOne(MemberInfo memberInfo, MemberInfo memberInfo1)
        {
            return RoboHelper.IsNullable(memberInfo.DeclaringType!) 
                   && RoboHelper.IsNullable(memberInfo1.DeclaringType!) 
                   && CanMapOneToOne();
        }
        
        private bool CanMapNullableOneToOne(Type a, Type b)
        {
            return RoboHelper.IsNullable(a) 
                   && RoboHelper.IsNullable(b) 
                   && CanMapOneToOne();
        }

        private bool CanMapOneToOne()
        {
            return PropertyInfoIn.PropertyType == PropertyInfoOut.PropertyType;
        }
        private bool CanMapOneToOne(Type a, Type b)
        {
            return a == b;
        }

        private StatementSyntax WithMapper()
        {
            var args = GetBaseTypeIfNullable();
            var mapper = IncludeInnerMapperIfNeeded(args.Item1, args.Item2);
            if (mapper == null)
            {
                throw new Exception("Mapper could not be included");
            }

            return ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("m"),
                        IdentifierName(Out.Name)
                    ),
                    InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName(mapper.Name),
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
                                            IdentifierName(In.Name)
                                        )
                                    )
                                )
                            )
                        )
                ));
        }

        private (Type, Type) GetBaseTypeIfNullable()
        {
            var inArguments = PropertyInfoIn.PropertyType.GetGenericArguments();
            if (inArguments.Length == 0)
            {
                inArguments = new[] { PropertyInfoIn.PropertyType };
            }

            var outArguments = PropertyInfoOut.PropertyType.GetGenericArguments();
            if (outArguments.Length == 0)
            {
                outArguments = new[] { PropertyInfoOut.PropertyType };
            }

            return (inArguments.First(), outArguments.First());
        }

        private StatementSyntax NullableWithMapper()
        {
            var inArguments = PropertyInfoIn.PropertyType.GetGenericArguments();
            if (inArguments.Length == 0)
            {
                inArguments = new[] { PropertyInfoIn.PropertyType };
            }

            var outArguments = PropertyInfoOut.PropertyType.GetGenericArguments();
            if (outArguments.Length == 0)
            {
                outArguments = new[] { PropertyInfoOut.PropertyType };
            }

            if (IsNullable() && PropertyInfoOut.PropertyType != typeof(string))
            {
                return GenerateNullableAssignmentWithValue(inArguments.First(), outArguments.First());
            }

            return GenerateNullableAssignment(inArguments.First(), outArguments.First());
        }

        private ExpressionStatementSyntax GenerateNullableAssignment(Type a, Type b)
        {
            var mapper = IncludeInnerMapperIfNeeded(a, b);
            if (mapper == null)
            {
                throw new Exception("Mapper could not be included");
            }
            return ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("m"),
                        IdentifierName(Out.Name)),
                    ConditionalExpression(
                        BinaryExpression(
                            SyntaxKind.NotEqualsExpression,
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName("obj"),
                                IdentifierName(In.Name)),
                            LiteralExpression(
                                SyntaxKind.NullLiteralExpression)),
                        InvocationExpression(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(mapper.Name),
                                    IdentifierName("Map")))
                            .WithArgumentList(
                                ArgumentList(
                                    SingletonSeparatedList(
                                        Argument(
                                            MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                IdentifierName("obj"),
                                                IdentifierName(In.Name)))))),
                        LiteralExpression(
                            SyntaxKind.NullLiteralExpression)))
            );
        }

        private Field? IncludeInnerMapperIfNeeded(Type @in, Type @out)
        {
            var mapper = _generateMapper.GetMapper(@in, @out) ?? _generateMapper.GetMapper(@out, @in); //mapper might not be present already, this can be because the mapper needs to be loaded
            if (mapper == null && In.GetCustomAttribute<MapIndex>()?.CustomParser != null)
            {
                //we know that there is a parser present here. Lets envoke it
                _generateMapper.RegisterParser(In.GetCustomAttribute<MapIndex>()!.CustomParser!);
                mapper = _generateMapper.GetMapper(@in, @out) ?? _generateMapper.GetMapper(@out, @in);
            }

            if (mapper == null && In.GetCustomAttribute<MapIndex>() != null && CanMapOneToOne(@in, @out) == false && CanMapNullableOneToOne(@in, @out) == false)
            {
                //this should be a mapper, let try we know this and include it
                _generateMapper.IncludeMapper(@in, @out);
                mapper = _generateMapper.GetMapper(@in, @out) ?? _generateMapper.GetMapper(@out, @in);
            }

            return mapper;
        }

        private ExpressionStatementSyntax GenerateNullableAssignmentWithValue(Type a, Type b)
        {
            var mapper = IncludeInnerMapperIfNeeded(a, b);
            if (mapper == null)
            {
                throw new Exception("Mapper could not be included");
            }
            return ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("m"),
                        IdentifierName(Out.Name)),
                    ConditionalExpression(
                        BinaryExpression(
                            SyntaxKind.NotEqualsExpression,
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName("obj"),
                                IdentifierName(In.Name)),
                            LiteralExpression(
                                SyntaxKind.NullLiteralExpression)),
                        InvocationExpression(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(mapper.Name),
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
                                                    IdentifierName(Out.Name)),
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
                        IdentifierName(Out.Name)
                    ),
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("obj"),
                        IdentifierName(In.Name)
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