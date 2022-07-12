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

        private static readonly Dictionary<string, object> Mappers = new Dictionary<string, object>();
        private static readonly Dictionary<Type, (object, Type, Type)> Parsers = new Dictionary<Type, (object, Type, Type)>();

        private static bool _initLock;
        private static readonly Namespace NameSpace = new Namespace();
        private static readonly List<IGenerateMapper> Classes = new List<IGenerateMapper>();
        private static string? _generated;

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
            NameSpace.AddUsing("System.Linq");
            NameSpace.Classes = Classes;
        }

        public static IMapper<TFrom, TTo> GetMapper<TFrom, TTo>()
        {
            if (Mappers.TryGetValue(typeof(IMapper<TFrom, TTo>).FullTypedName(), out var mapper))
            {
                return (mapper as IMapper<TFrom, TTo>)!;
            }

            throw new Exception("cannot create mapper since objects are not linked with Mappable");
        }

        public static object? GetMapper(Type @in, Type @out)
        {
            foreach (var mapper in Mappers)
            {
                var args = mapper.Value.GetType().GetInterfaces().First().GetGenericArguments();
                if (args[0] == @in && args[1] == @out)
                {
                    return mapper.Value;
                }
            }

            return null;
        }

        public static IEnumerable<(Type, object)> GetMappers()
        {
            return Mappers.Select(e => (e.Value.GetType(), e.Value));
        }

        public static void Bind<T1, T2>()
        {
            Classes.Add(new GenerateIMapper(NameSpace, typeof(T1), typeof(T2)));
            Classes.Add(new GenerateIMapper(NameSpace, typeof(T2), typeof(T1)));
        }

        public static void Bind<T1, T2>(Action<DeclareMapParser> parsers) where T1 : class where T2 : class
        {
            var declareMapParser = new DeclareMapParser();
            parsers(declareMapParser);
            var p = declareMapParser.Parsers;
            foreach (var parser in p)
            {
                var instance = Activator.CreateInstance(parser.Item1);
                var field1Instance = Activator.CreateInstance(typeof(T1)) as T1;
                var field2Instance = Activator.CreateInstance(typeof(T2)) as T2;
                if (field1Instance == null)
                {
                    throw new Exception($"{typeof(T1).FullTypedName()} could not be instantiated, it requires an empty construct");
                }
                if (field2Instance == null)
                {
                    throw new Exception($"{typeof(T2).FullTypedName()} could not be instantiated, it requires an empty construct");
                }

                var field1Type = (field1Instance.GetType().GetMembers().Where(e => e.MemberType == MemberTypes.Property).FirstOrDefault(e => e.Name == parser.Item2) as PropertyInfo)?.PropertyType;
                if (field1Type == null)
                {
                    throw new Exception($"could not find member {parser.Item2} in {typeof(T1).FullTypedName()}");
                }
                var field2Type = (field2Instance.GetType().GetMembers().Where(e => e.MemberType == MemberTypes.Property).FirstOrDefault(e => e.Name == parser.Item3) as PropertyInfo)?.PropertyType;
                if (field2Type == null)
                {
                    throw new Exception($"could not find member {parser.Item3} in {typeof(T2).FullTypedName()}");
                }
                
                Parsers.TryAdd(parser.Item1, (instance, field1Type, field2Type)!);
                Mappers.TryAdd($"RoboMapper.IMapper<{field1Type},{field2Type}>", instance);
                Mappers.TryAdd($"RoboMapper.IMapper<{field2Type},{field1Type}>", instance);
            }

            Classes.Add(new GenerateIMapper(NameSpace, typeof(T1), typeof(T2)));
            Classes.Add(new GenerateIMapper(NameSpace, typeof(T2), typeof(T1)));
        }

        public static void LoadAssembly()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            _generated = NameSpace.Generate().NormalizeWhitespace().ToFullString();
            var compilation = CreateAssemblyFromString(_generated, assemblies, NameSpace.AllKnownTypes);

            TryLoadAssemblyToMappers(compilation);
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
                        if (Mappers.TryGetValue(arg.ParameterType.FullTypedName(), out var mapper))
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
                        var @interface = type.GetInterfaces().First();
                        var genArgs = @interface.GetGenericArguments();
                        Mappers.Add($"RoboMapper.IMapper<{genArgs[0]},{genArgs[1]}>", Activator.CreateInstance(type, args: injectedArgs.ToArray())!);
                    }
                }
                else
                {
                    var @interface = type.GetInterfaces().First();
                    var genArgs = @interface.GetGenericArguments();
                    Mappers.Add($"RoboMapper.IMapper<{genArgs[0]},{genArgs[1]}>", Activator.CreateInstance(type)!);
                }
            }

            Logger.LogInformation("loaded all assemblies");
        }

        public static object GetParser(Type type, Type type1)
        {
            return Parsers.First(
                e =>
                    e.Value.Item2 == type && e.Value.Item3 == type1 ||
                    e.Value.Item3 == type && e.Value.Item2 == type1).Value.Item1;
        }
    }

    public class DeclareMapParser
    {
        public readonly List<(Type, string, string)> Parsers = new List<(Type, string, string)>();

        public void MapWith<TParser>(string field1, string field2)
        {
            Parsers.Add((typeof(TParser), field1, field2));
        }
    }
}