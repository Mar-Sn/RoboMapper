using System;
using System.Collections.Generic;
using System.Text;

namespace RoboMapper
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class, AllowMultiple = true)]
    public class MapIgnore: Attribute
    {
    }
}
