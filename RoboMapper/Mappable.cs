using System;

namespace RoboMapper
{
    [AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true)]
    public class Mappable : Attribute
    {
        public Mappable(params string[] uniqueName)
        {
            UniqueName = uniqueName;
        }
        public string[] UniqueName { get; }
    }
}
