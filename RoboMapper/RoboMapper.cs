using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using RoboMapper.Roslyn;

namespace RoboMapper
{
    public static class RoboMapper
    {
        public static ILogger Logger = null!;
        
        private static readonly Dictionary<Type, object> Mappers = new Dictionary<Type, object>();

        private static bool _initLock = false;
        public static void Init(ILogger logger)
        {
            if (!_initLock)
            {
                _initLock = true;
            }
            else
            {
                return;
            }
            Logger = logger;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            var mappables = FindMappables(assemblies);

            FindParsers(assemblies);

            var nameSpace = new Namespace();
            nameSpace.AddUsing("System.Linq");
            var classes = mappables.Values.Select(e =>
            {
                if (e.Count >= 2)
                {
                    return new GenerateIMapper(nameSpace, e[0], e[1]);
                }

                throw new Exception($"Unable to find mapper counterpart, where should I map to? {e[0].FullName}");
            }).ToList();
            classes.AddRange(mappables.Values.Select(e =>
            {
                if (e.Count >= 2)
                {
                    return new GenerateIMapper(nameSpace, e[1], e[0]);
                }

                throw new Exception($"Unable to find mapper counterpart, where should I map to? {e[0].FullName}");
            }));
            
            nameSpace.Classes = classes;

            var generated = nameSpace.Generate().NormalizeWhitespace().ToFullString();

            var compilation = CreateAssemblyFromString(generated, assemblies, nameSpace.AllKnownTypes);

            TryLoadAssemblyToMappers(compilation);
        }

        public static void Define<T1, T2>()
        {
            //placeholder for now  
        }
        public static void Define<T1>()
        {
            //placeholder for now  
        }

        public static void Define<T1, T2, T3>()
        {
            //placeholder for now  
        }

        private static void TryLoadAssemblyToMappers(CSharpCompilation compilation)
        {
            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);

            if (!result.Success)
            {
                var failures = result.Diagnostics.Where(diagnostic =>
                    diagnostic.IsWarningAsError ||
                    diagnostic.Severity == DiagnosticSeverity.Error);

                foreach (var diagnostic in failures)
                {
                    Logger.LogError("{diagnostic.Id}: {diagnostic.GetMessage()}", diagnostic.Id, diagnostic.GetMessage());
                }

                throw new Exception("RoboMapper is not able to compile classes");
            }

            LoadGeneratedAssembly(ms);
        }

        private static CSharpCompilation CreateAssemblyFromString(string fullString, IEnumerable<Assembly> assemblies, IEnumerable<Type> usings)
        {
            var syntaxTree = SyntaxFactory.ParseSyntaxTree(SourceText.From(fullString));

            var list = new List<PortableExecutableReference>();
            foreach (var assembly in assemblies.Where(e => e.IsDynamic == false && string.IsNullOrWhiteSpace(e.Location) == false))
            {
                list.Add(MetadataReference.CreateFromFile(assembly.Location));
            }

            foreach (var type in usings)
            {
                list.Add(MetadataReference.CreateFromFile(type.Assembly.Location));
            }

            var compilation = CSharpCompilation.Create("mapper_generator")
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddReferences(list)
                .AddSyntaxTrees(syntaxTree);
            return compilation;
        }


        private static Dictionary<string, List<Type>> FindMappables(Assembly[] assemblies)
        {
            var dictionary = new Dictionary<string, List<Type>>();

            foreach (var assembly in assemblies)
            {
                var mps = assembly.GetTypes().Where(e => e.GetCustomAttribute<Mappable>() != null);

                foreach (var @type in mps)
                {
                    var mappables = @type.GetCustomAttribute<Mappable>();
                    if (mappables != null)
                    {
                        foreach (var mappable in mappables.UniqueName)
                        {
                            if (!dictionary.ContainsKey(mappable))
                            {
                                dictionary.Add(mappable, new List<Type>());
                            }

                            dictionary[mappable].Add(@type);
                        }
                    }
                }
            }

            return dictionary;
        }

        private static void FindParsers(Assembly[] assemblies)
        {
            foreach (var assembly in assemblies)
            {
                var mps = assembly.GetTypes().Where(e => e.GetCustomAttribute<MapParser>() != null);

                foreach (var @type in mps)
                {
                    Mappers.TryAdd(type.GetInterfaces().First(e => e.Name.Contains("IMapper")), Activator.CreateInstance(type)!);
                }
            }
        }

        private static void LoadGeneratedAssembly(MemoryStream ms)
        {
            ms.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(ms.ToArray());

            var processQueue = new Queue<Type>(assembly.GetTypes());

            while (processQueue.Count > 0)
            {
                var @type = processQueue.Dequeue();
                if (type.GetConstructors().Any(e => e.GetParameters().Length > 0))
                {
                    var constructorArgs = type.GetConstructors().SelectMany(e => e.GetParameters());
                    var injectedArgs = new List<object>();
                    var hasFullArgsSet = true;
                    foreach (var arg in constructorArgs)
                    {
                        if (Mappers.TryGetValue(arg.ParameterType, out var mapper))
                        {
                            injectedArgs.Add(mapper);
                        }
                        else
                        {
                            hasFullArgsSet = false;
                            processQueue.Enqueue(@type);
                            break;
                        }
                    }

                    if (hasFullArgsSet)
                    {
                        Mappers.Add(type.GetInterfaces()[0], Activator.CreateInstance(type, args: injectedArgs.ToArray())!);
                    }
                }
                else
                {
                    Mappers.Add(type.GetInterfaces()[0], Activator.CreateInstance(type)!);
                }
            }

            Logger.LogInformation("loaded all assemblies");
        }

        public static IMapper<TFrom, TTo> GetMapper<TFrom, TTo>()
        {
            if (Mappers.TryGetValue(typeof(IMapper<TFrom, TTo>), out var mapper))
            {
                return (mapper as IMapper<TFrom, TTo>)!;
            }

            throw new Exception("cannot create mapper since objects are not linked with Mappable");
        }

        public static object? GetMapper(Type @in, Type @out)
        {
            foreach (var mapper in Mappers)
            {
                var args = mapper.Key.GetGenericArguments();
                if (args[0] == @in && args[1] == @out)
                {
                    return mapper.Value;
                }
            }

            return null;
        }

        public static IEnumerable<(Type, object)> GetMappers()
        {
            return Mappers.Select(e => (e.Key, e.Value));
        }
    }
}