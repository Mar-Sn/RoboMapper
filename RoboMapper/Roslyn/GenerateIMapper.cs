using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace RoboMapper.Roslyn
{
    public class GenerateIMapper
    {
        public readonly Namespace Namespace;
        public string Name { get; }
        public Type A { get; }
        public Type B { get; }

        public MapperInterface MapperInterface { get; }

        public Fields Fields { get; } = new Fields();

        public Contructor Contructor { get; private set; }

        public List<MapMethod> Methods { get; } = new List<MapMethod>();

        public GenerateIMapper(Namespace @namespace, Type a, Type b)
        {
            Namespace = @namespace;
            Name = "Mapped" + Guid.NewGuid().ToString().Replace("-", "");
            A = a;
            B = b;
            MapperInterface = new MapperInterface
            {
                A = A,
                B = B
            };
        }

        public ClassDeclarationSyntax Generate()
        {
            Contructor = new Contructor(Name, Fields);

            SetMethodData();

            var clazz = ClassDeclaration(Name);
            clazz = clazz.AddBaseListTypes(MapperInterface.Generate());
            clazz = clazz.AddModifiers(Token(SyntaxKind.PublicKeyword));
            var members = new List<MemberDeclarationSyntax>();
            members.AddRange(Methods.Select(e => e.Generate()));
            members.Add(Contructor.Generate());
            members.AddRange(Fields.Values.Select(e => e.Generate()));
            members.Reverse();

            clazz = clazz.AddMembers(members.ToArray());
            return clazz;
        }

        private void SetMethodData()
        {
            var aMemberInfos =
                A.GetMembers()
                    .Where(e => e is PropertyInfo && !e.GetCustomAttributes<MapIgnore>().Any())
                    .ToDictionary(e => e.GetCustomAttribute<MapIndex>()!.IndexName, e => e);

            var bMemberInfos =
                B.GetMembers()
                    .Where(e => e is PropertyInfo && !e.GetCustomAttributes<MapIgnore>().Any())
                    .ToDictionary(e =>
                    {
                        var customAttribute = e.GetCustomAttribute<MapIndex>();
                        if (customAttribute != null)
                        {
                            return customAttribute.IndexName;
                        }

                        throw new Exception($"unable find corresponding field for {e.Name}");
                    }, e => e);

            List<SingleSet> aSets;
            List<SingleSet> bSets;
            try
            {
                aSets =
                    aMemberInfos
                        .Where(e => e.Value.GetCustomAttribute<MapIndex>().Optional == false || bMemberInfos.ContainsKey(e.Key))
                        .Select(e =>
                        {
                            if(bMemberInfos.TryGetValue(e.Key, out var value))
                            {
                                return (e.Value, value);
                            }

                            throw new Exception($"unable to find counterpart of {e.Key}");
                        })
                        .Select(e => new SingleSet(this, e.Value, e.Item2))
                        .ToList();

                bSets =
                    bMemberInfos
                        .Where(e => e.Value.GetCustomAttribute<MapIndex>().Optional == false || aMemberInfos.ContainsKey(e.Key))
                        .Select(e =>
                        {
                            if(aMemberInfos.TryGetValue(e.Key, out var value))
                            {
                                return (e.Value, value);
                            }

                            throw new Exception($"unable to find counterpart of {e.Key}");
                        })
                        .Select(e => new SingleSet(this, e.Value, e.Item2))
                        .ToList();
            }
            catch (Exception e)
            {
                RoboMapper.Logger.LogError("Could not generate single sets", e);
                throw;
            }

            Methods.Add(new MapMethod(B, A, aSets));

            Methods.Add(new MapMethod(A, B, bSets));
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


                if (RoboHelper.CanMapOneToOne(propertyInfo.PropertyType) == false)
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
                        RoboMapper.Logger.LogInformation("Field {propertyInfo.PropertyType.FullName} cannot be mapped one-to-one", propertyInfo.PropertyType.FullName);
                        var fieldOut = _out.GetMembers().Where(e => e is PropertyInfo).First(e => e.GetCustomAttribute<MapIndex>()?.IndexName == mapIndex.First().IndexName) as PropertyInfo;

                        var propertyInfoIn = propertyInfo.PropertyType;
                        var propertyInfoOut = fieldOut!.PropertyType;

                        if (RoboHelper.IsNullable(propertyInfoIn))
                        {
                            var genericArg = propertyInfoIn.GetGenericArguments().FirstOrDefault();
                            if (genericArg != null)
                            {
                                propertyInfoIn = genericArg;
                            }
                        }

                        if (RoboHelper.IsNullable(propertyInfoOut))
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

        public Field? GetMapper(Type? a, Type? b)
        {
            if (a != null && b != null)
            {
                return Fields.TryGet(a, b);
            }

            return null;
        }

        public override string ToString() => Name;

        public void RegisterParser(Type customParser)
        {
            //TODO improve way to handle this
            var mapper = customParser.GetInterfaces().FirstOrDefault(e => e.FullName?.Contains("IMapper") ?? false);
            if (mapper != null)
            {
                var genericArguments = mapper.GetGenericArguments();
                RoboMapper.Logger.LogDebug("Adding new Field to {Name} args: {genericArguments[0]}, {genericArguments[1]}", Name, genericArguments[0], genericArguments[1]);
                if (GetMapper(genericArguments[0], genericArguments[1]) == null)
                {
                    Fields.TryAdd(
                        new Field(genericArguments[0], genericArguments[1]) //at this point we just assume its correct and load it
                    );
                }

                Contructor = new Contructor(Name, Fields); //simply recreate based on fields
            }
            else
            {
                throw new Exception("registered parser is not of type IMapper");
            }
        }

        public void IncludeMapper(Type a, Type b)
        {
            var mapper = Namespace.Classes.SingleOrDefault(e => e.A == a && e.B == b);
            if (mapper == null)
            {
                mapper = Namespace.Classes.SingleOrDefault(e => e.A == b && e.B == a);
            }

            if (mapper != null)
            {
                Fields.TryAdd(
                    new Field(mapper.A, mapper.B) //at this point we just assume its correct and load it
                );
            }
        }
    }
}