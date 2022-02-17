using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace RoboMapper
{
    public static class RoboMapper
    {
        private static readonly Dictionary<Type, object> _mappables = new Dictionary<Type, object>();
            
            
        private static readonly Dictionary<string, Dictionary<Type, WrappedObject>> Links = new Dictionary<string, Dictionary<Type, WrappedObject>>();
        private static readonly Dictionary<Type, List<string>> TypeToGroup = new Dictionary<Type, List<string>>();

        static RoboMapper()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
             
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
            var tasks = new List<Task<string>>();

            foreach (var entry in dictionary)
            {
                tasks.Add(new CreateClass().Generate(entry.Value[0], entry.Value[1]));    
                tasks.Add(new CreateClass().Generate(entry.Value[1], entry.Value[0]));    
            }

            Task.WhenAll(tasks).GetAwaiter().GetResult(); //block until all is done

            var fullstring = string.Join("", tasks.Select(e => e.Result));

            var syntaxTree = SyntaxFactory.ParseSyntaxTree(SourceText.From(fullstring));

            var assemblyPath = Path.ChangeExtension(Path.GetTempFileName(), "exe");

            var list = new List<PortableExecutableReference>();
            foreach (var assembly in assemblies.Where(e => e.IsDynamic == false))
            {
                list.Add(MetadataReference.CreateFromFile(assembly.Location));
            }
            
            var compilation = CSharpCompilation.Create(Path.GetFileName(assemblyPath))
                .WithOptions(new CSharpCompilationOptions(OutputKind.ConsoleApplication))
                .AddReferences(list)
                .AddSyntaxTrees(syntaxTree);

            var result = compilation.Emit(assemblyPath);
            if (result.Success)
            {
                Assembly.Load(compilation.AssemblyName);
            }
        }

        public static IMapper<TFrom, TTo> GetMapper<TFrom, TTo>()
        {
            //RegisterType<TFrom>();
            //RegisterType<TTo>();
            if (TypeToGroup.TryGetValue(typeof(TFrom), out var linkedList1) &&
                TypeToGroup.TryGetValue(typeof(TTo), out var linkedList2))
            {
                foreach (var link1 in linkedList1)
                {
                    var link2 = linkedList2.SingleOrDefault(e => e == link1);
                    if (link2 != null)
                    {
                        return new Mapper<TFrom, TTo>(Links[link1][typeof(TFrom)], Links[link2][typeof(TTo)]);
                    }
                }
            }
            throw new Exception("cannot create mapper since objects are not linked with Mappable");
        }
    }
}
