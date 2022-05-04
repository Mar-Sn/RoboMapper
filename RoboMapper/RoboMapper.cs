using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace RoboMapper
{
    public static class RoboMapper
    {
        private static readonly Dictionary<Type, object> Mappers = new Dictionary<Type, object>();

        static RoboMapper()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            var dictionary = FindMappables(assemblies);

            var tasks = GenerateIMappers(dictionary);

            var fullString = string.Join("", tasks.Select(e => e.Result));

            var compilation = CreateAssemblyFromString(fullString, assemblies);

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
                    Console.Error.WriteLine("{0}: {1}", diagnostic.Id, diagnostic.GetMessage());
                }
            }
            else
            {
                LoadGeneratedAssembly(ms);
            }
        }

        private static CSharpCompilation CreateAssemblyFromString(string fullString, IEnumerable<Assembly> assemblies)
        {
            var syntaxTree = SyntaxFactory.ParseSyntaxTree(SourceText.From(fullString));
            
            var list = new List<PortableExecutableReference>();
            foreach (var assembly in assemblies.Where(e => e.IsDynamic == false && string.IsNullOrWhiteSpace(e.Location) == false))
            {
                list.Add(MetadataReference.CreateFromFile(assembly.Location));
            }
            
            var compilation = CSharpCompilation.Create("mapper_generator")
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddReferences(list)
                .AddSyntaxTrees(syntaxTree);
            return compilation;
        }

        private static List<Task<string>> GenerateIMappers(Dictionary<string, List<Type>> dictionary)
        {
            var tasks = new List<Task<string>>();

            foreach (var entry in dictionary)
            {
                tasks.Add(new CreateClass().Generate(entry.Value[0], entry.Value[1]));
                tasks.Add(new CreateClass().Generate(entry.Value[1], entry.Value[0]));
            }

            Task.WhenAll(tasks).GetAwaiter().GetResult(); //block until all is done
            return tasks;
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

            return dictionary;
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
                        Mappers.Add(type.GetInterfaces()[0], Activator.CreateInstance(type, args: injectedArgs.ToArray()));
                    }
                }
                else
                {
                    Mappers.Add(type.GetInterfaces()[0], Activator.CreateInstance(type));
                }
            }
        }

        public static void Init(){}
        
        public static IMapper<TFrom, TTo> GetMapper<TFrom, TTo>()
        {
            if (Mappers.TryGetValue(typeof(IMapper<TFrom, TTo>), out var mapper))
            {
                return mapper as IMapper<TFrom, TTo>;
            }
            throw new Exception("cannot create mapper since objects are not linked with Mappable");
        }

        public static void RegisterFieldMapping<TIn, TOut>(IMapper<TIn, TOut> inToOut)
        {
            
        }
    }
}