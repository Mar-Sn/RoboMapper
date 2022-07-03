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
                        .Where(e => bMemberInfos.ContainsKey(e.Key))
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
                        .Where(e => aMemberInfos.ContainsKey(e.Key))
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


        public Field? GetMapper(Type? a, Type? b)
        {
            if (a != null && b != null)
            {
                return Fields.TryGet(a, b);
            }

            return null;
        }

        public override string ToString() => Name;

        public void RegisterParser(string customParser)
        {
            //TODO improve way to handle this
            var instance = RoboMapper.GetMappers().Select(e => e.Item2).FirstOrDefault(e => e.GetType().GetCustomAttribute<MapParser>()?.Name == customParser);
            if (instance == null)
            {
                throw new Exception("registered parser is not of type IMapper");
            }
            var mapper = instance.GetType().GetInterfaces().FirstOrDefault(e => e.FullName?.Contains("IMapper") ?? false);
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