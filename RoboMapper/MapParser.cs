using System;

namespace RoboMapper
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class MapParser : Attribute
    {
        public string Name { get; }

        public MapParser(string name)
        {
            Name = name;
        }    
    }
}
