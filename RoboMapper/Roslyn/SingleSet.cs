using System;
using System.Collections.Generic;
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
            _generateMapper.Namespace.AllKnownTypes.Add(types.Item1);
            _generateMapper.Namespace.AllKnownTypes.Add(types.Item2);
            var canMapOneToOne = CanMapOneToOne();
            var canMapNullableOneToOne = CanMapNullableOneToOne(In, Out);
            if (canMapOneToOne || canMapNullableOneToOne)
            {
                RoboMapper.Logger.LogDebug("Class: {_generateMapper.Name} Can map field {A.Name} to {B.Name} one-to-one", _generateMapper.Name, In.Name, Out.Name);
                return SimpleOneToOne();
            }

            if (IsIEnumerable())
            {
                RoboMapper.Logger.LogDebug("Class: {_generateMapper.Name} Field is type of IEnumerable {A.Name} to {B.Name}", _generateMapper.Name, In.Name, Out.Name);
                return EnumableWithMapper();
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

        private bool IsIEnumerable()
        {
            var aIsIEnumerable = PropertyInfoIn.PropertyType.IsGenericType && PropertyInfoIn.PropertyType.GetInterfaces().Any(e => e.FullName == "System.Collections.IEnumerable");
            var bIsIEnumerable = PropertyInfoOut.PropertyType.IsGenericType && PropertyInfoOut.PropertyType.GetInterfaces().Any(e => e.FullName == "System.Collections.IEnumerable");
            return aIsIEnumerable && bIsIEnumerable;
        }
        
        private bool IsIList()
        {
            var aIsIEnumerable = PropertyInfoIn.PropertyType.IsGenericType && PropertyInfoIn.PropertyType.GetInterfaces().Any(e => e.FullName == "System.Collections.IList");
            var bIsIEnumerable = PropertyInfoOut.PropertyType.IsGenericType && PropertyInfoOut.PropertyType.GetInterfaces().Any(e => e.FullName == "System.Collections.IList");
            return aIsIEnumerable && bIsIEnumerable;
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
                throw new Exception($"IMapper<{_generateMapper.A},{_generateMapper.B}> Or parser for Fields {In.Name} <-> {Out.Name} cannot be found");
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

        private StatementSyntax EnumableWithMapper()
        {
            var args = (PropertyInfoIn.PropertyType.GetGenericArguments().First(), PropertyInfoOut.PropertyType.GetGenericArguments().First());
            var mapper = IncludeInnerMapperIfNeeded(args.Item1, args.Item2);
            if (mapper == null)
            {
                throw new Exception($"Mapper could not be included of type IMapper<{args.Item1},{args.Item2} for fields {In.Name} <-> {Out.Name}");
            }

            if (IsIList())
            {
                return SingleSetRoslynBuilder.ListWithImapper(Out.Name, In.Name, mapper.Name);
            }
            
            return SingleSetRoslynBuilder.EnumerableWithIMapper(Out.Name, In.Name, mapper.Name);
        }

        private (Type, Type) GetBaseTypeIfNullable()
        {
            var inArguments = PropertyInfoIn.PropertyType.GetGenericArguments();
            if (inArguments.Length == 0 || IsNullable() == false)
            {
                inArguments = new[] { PropertyInfoIn.PropertyType };
            }

            var outArguments = PropertyInfoOut.PropertyType.GetGenericArguments();
            if (outArguments.Length == 0 || IsNullable() == false)
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

            if (IsNullable() && PropertyInfoIn.PropertyType != typeof(string))
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
                _generateMapper.IncludeParserToFieldsAndConstructor(@in, @out);
                mapper = _generateMapper.GetMapper(@in, @out) ?? _generateMapper.GetMapper(@out, @in);
            }

            if (mapper == null && In.GetCustomAttribute<MapIndex>() != null && CanMapOneToOne(@in, @out) == false && CanMapNullableOneToOne(@in, @out) == false)
            {
                //this should be a mapper, lets assume we know this and include it
                var known = RoboMapper.GetMapper(@in, @out) != null;
                
                if (known)
                {
                    _generateMapper.IncludeMapper(@in, @out);
                    mapper = _generateMapper.GetMapper(@in, @out);
                }
                else
                {
                    _generateMapper.IncludeMapper(@out, @in);
                    mapper = _generateMapper.GetMapper(@out, @in);
                }
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
                                                    IdentifierName(In.Name)),
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