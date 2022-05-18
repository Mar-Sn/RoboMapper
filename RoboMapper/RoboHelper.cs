#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoboMapper.Roslyn;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace RoboMapper
{
    public class RoboHelper
    {
        public static string Sanitize(string str)
        {
            return str
                .Replace(" ", "")
                .Replace("`", "")
                .Replace("-", "")
                .Replace(".", "");
        }

        public static bool IsNullable(Type a)
        {
            var aIsNullable = a.IsGenericType && a.GetGenericTypeDefinition() == typeof(Nullable<>) || a == typeof(string);
            return aIsNullable;
        }

        public static bool IsNullable(Type a, Type b)
        {
            var aIsNullable = a.IsGenericType && a.GetGenericTypeDefinition() == typeof(Nullable<>) || a == typeof(string);
            var bIsNullable = b.IsGenericType && b.GetGenericTypeDefinition() == typeof(Nullable<>) || b == typeof(string);
            return aIsNullable && bIsNullable;
        }

        public static bool CanMapOneToOne(Type type) => type == typeof(int)
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