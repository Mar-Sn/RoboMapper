using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace RoboMapper.Roslyn
{
    public class SingleSetRoslynBuilder
    {
        public static ExpressionStatementSyntax ListWithImapper(string outName, string inName, string mapperName)
        {
            return ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("m"),
                        IdentifierName(outName)),
                    InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            InvocationExpression(
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            IdentifierName("obj"),
                                            IdentifierName(inName)),
                                        IdentifierName("Select")))
                                .WithArgumentList(
                                    ArgumentList(
                                        SingletonSeparatedList(
                                            Argument(
                                                SimpleLambdaExpression(
                                                        Parameter(
                                                            Identifier("e")))
                                                    .WithExpressionBody(
                                                        InvocationExpression(
                                                                MemberAccessExpression(
                                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                                    IdentifierName(mapperName),
                                                                    IdentifierName("Map")))
                                                            .WithArgumentList(
                                                                ArgumentList(
                                                                    SingletonSeparatedList(
                                                                        Argument(
                                                                            IdentifierName("e")))))))))),
                            IdentifierName("ToList")))));
        }
        
        public static ExpressionStatementSyntax EnumerableWithIMapper(string outName, string inName, string mapperName)
        {
            return ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("m"),
                        IdentifierName(outName)),
                    InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName("obj"),
                                    IdentifierName(inName)),
                                IdentifierName("Select")))
                        .WithArgumentList(
                            ArgumentList(
                                SingletonSeparatedList(
                                    Argument(
                                        SimpleLambdaExpression(
                                                Parameter(
                                                    Identifier("e")))
                                            .WithExpressionBody(
                                                InvocationExpression(
                                                        MemberAccessExpression(
                                                            SyntaxKind.SimpleMemberAccessExpression,
                                                            IdentifierName(mapperName),
                                                            IdentifierName("Map")))
                                                    .WithArgumentList(
                                                        ArgumentList(
                                                            SingletonSeparatedList(
                                                                Argument(
                                                                    IdentifierName("e"))))))))))));
        }
    }
}