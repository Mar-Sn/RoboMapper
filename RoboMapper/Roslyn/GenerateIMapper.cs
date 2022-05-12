using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace RoboMapper.Roslyn
{
    public class GenerateIMapper
    {
        public string Name { get; }
        public Type A { get; }
        public Type B { get; }

        public MapperInterface MapperInterface { get; }

        public List<Field> Fields { get; }

        public Contructor Contructor { get; }

        public List<MapMethod> Methods { get; } = new List<MapMethod>();

        public GenerateIMapper(Type a, Type b)
        {
            Name = "Mapped" + Guid.NewGuid().ToString().Replace("-", "");
            A = a;
            B = b;

            var innerMappers = GetInnerMappers(a, b);

            var fields = innerMappers.Select(e => new Field
            {
                A = e.Item1,
                B = e.Item2
            }).ToList();

            Fields = fields;
            MapperInterface = new MapperInterface
            {
                A = A,
                B = B
            };
            Contructor = new Contructor(Name, fields);

            var aMemberInfos =
                a.GetMembers()
                    .Where(e => e is PropertyInfo && !e.GetCustomAttributes<MapIgnore>().Any())
                    .ToDictionary(e => e.GetCustomAttribute<MapIndex>()!.IndexName, e => e);

            var bMemberInfos =
                b.GetMembers()
                    .Where(e => e is PropertyInfo && !e.GetCustomAttributes<MapIgnore>().Any())
                    .ToDictionary(e => e.GetCustomAttribute<MapIndex>()!.IndexName, e => e);

            List<SingleSet> aSets;
            List<SingleSet> bSets;
            try
            {
                aSets =
                    aMemberInfos
                        .Select(e => (e.Value, bMemberInfos[e.Key]))
                        .Select(e => new SingleSet(this, e.Value, e.Item2))
                        .ToList();

                bSets =
                    bMemberInfos
                        .Select(e => (e.Value, bMemberInfos[e.Key]))
                        .Select(e => new SingleSet(this, e.Value, e.Item2))
                        .ToList();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            Methods.Add(new MapMethod(b, a, aSets));

            Methods.Add(new MapMethod(a, b, bSets));
        }

        public ClassDeclarationSyntax Generate()
        {
            var clazz = ClassDeclaration(Name);
            clazz = clazz.AddBaseListTypes(MapperInterface.Generate());
            clazz = clazz.AddModifiers(Token(SyntaxKind.PublicKeyword));
            var members = new List<MemberDeclarationSyntax>();
            members.AddRange(Fields.Select(e => e.Generate()));
            members.Add(Contructor.Generate());
            members.AddRange(Methods.Select(e => e.Generate()));
            
            clazz = clazz.AddMembers(members.ToArray());
            return clazz;
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


                if (CreateClass.CanMapOneToOne(propertyInfo.PropertyType) == false)
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

                        if (CreateClass.IsNullable(propertyInfoIn))
                        {
                            var genericArg = propertyInfoIn.GetGenericArguments().FirstOrDefault();
                            if (genericArg != null)
                            {
                                propertyInfoIn = genericArg;
                            }
                        }

                        if (CreateClass.IsNullable(propertyInfoOut))
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

        public Field GetMapper(MemberInfo a, MemberInfo b)
        {
            return Fields.Single(e => e.A == a.DeclaringType && e.B == b.DeclaringType);
        }

        public override string ToString()
        {
            return Generate().NormalizeWhitespace().ToFullString();
        }
    }
}