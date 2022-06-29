using System;
using System.Collections.Generic;
using System.Text;

namespace RoboMapper
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class, AllowMultiple = true)]
    public class MapIndex: Attribute
    {
        public string IndexName { get; }
        public string? CustomParser { get; }

        public MapIndex(string indexName)
        {
            IndexName = indexName;
        }
        
        public MapIndex(string indexName, string customParser)
        {
            CustomParser = customParser;
            IndexName = indexName;
        }
    }
}
