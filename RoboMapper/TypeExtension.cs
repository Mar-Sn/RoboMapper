using System;
using System.Linq;

namespace RoboMapper
{
    public static class TypeExtension
    {
        public static string FullTypedName(this Type type)
        {
            var name = type.FullName!;
            var generics = type.GetGenericArguments();
            if (generics.Length > 0)
            {
                //this is a generic type
                //to get the correct name we have to extract
                name = type.Namespace + ".";
                name += type.Name.Substring(0, type.Name.IndexOf('`'));
                name += "<" + string.Join(",", generics.Select(e => e.FullName).ToArray()) + ">";
            }
            return name;
        }     
    }
}