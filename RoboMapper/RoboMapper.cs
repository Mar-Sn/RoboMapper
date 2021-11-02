using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RoboMapper
{
    public static class RoboMapper
    {
        private static readonly Dictionary<string, Dictionary<Type, WrappedObject>> Links = new Dictionary<string, Dictionary<Type, WrappedObject>>();
        private static readonly Dictionary<Type, List<string>> TypeToGroup = new Dictionary<Type, List<string>>();

        static RoboMapper()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                var mps = assembly.GetTypes();

                foreach (var type in mps)
                {
                    if (type.GetCustomAttribute<Mappable>() != null)
                    {
                        RegisterType(type);
                    }
                }
            }
        }

        internal static void RegisterType(Type type)
        {
            if (TypeToGroup.ContainsKey(type)) return;
            var mappableType = type;
            var mappable = mappableType.GetCustomAttributes<Mappable>();
            if (!mappable.Any()) throw new Exception("This is not a mappable");
            //found a mappable
            //var instance = Activator.CreateInstance(type);
            var instance = new WrappedObject(Activator.CreateInstance(mappableType));


            foreach (var field in mappableType.GetMembers().Where(e => e is PropertyInfo))
            {
                var mapIndex = field.GetCustomAttributes<MapIndex>();
                if (!mapIndex.Any()) throw new Exception($"field {field.Name} of class {mappableType.Name} has no index!");

                var propertyInfo = field as PropertyInfo;
                if (mapIndex == null || propertyInfo == null)
                {
                    throw new Exception("fields should have mapIndex present if class is defined Mappable");
                }



                var getterSetter = new GetterSetter(instance)
                {
                    Setter = (backingObject, args) => propertyInfo.GetSetMethod().Invoke(backingObject, args),
                    Getter = propertyInfo.GetMethod
                };

                instance.Fields.Add(mapIndex.First().IndexName, getterSetter); //bind 
            }

            foreach (var uniqueName in mappable.First().UniqueName)
            {
                if (!Links.ContainsKey(uniqueName))
                {
                    Links.Add(uniqueName, new Dictionary<Type, WrappedObject>());
                }
                Links[uniqueName].Add(mappableType, instance);

                if (!TypeToGroup.ContainsKey(mappableType))
                {
                    TypeToGroup.Add(mappableType, new List<string> { uniqueName });
                }
                else
                {
                    TypeToGroup[mappableType].Add(uniqueName);
                }
            }
        }

        internal static void RegisterType<T>()
        {
            if (TypeToGroup.ContainsKey(typeof(T))) return;
            var mappableType = typeof(T);
            RegisterType(mappableType);
        }


        public static IMapper<TFrom, TTo> GetMapper<TFrom, TTo>()
        {
            RegisterType<TFrom>();
            RegisterType<TTo>();
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
