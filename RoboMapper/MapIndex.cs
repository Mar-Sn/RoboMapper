using System;
using System.Collections.Generic;
using System.Text;

namespace RoboMapper
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class, AllowMultiple = true)]
    public class MapIndex: Attribute
    {
        public string IndexName { get; }
        public Type? CustomParser { get; }

        public MapIndex(string indexName)
        {
            IndexName = indexName;
        }
        
        public MapIndex(string indexName, Type customParser)
        {
            CustomParser = customParser;
            IndexName = indexName;
        }
    }
}
