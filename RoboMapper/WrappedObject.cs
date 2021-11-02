using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RoboMapper
{
    internal class WrappedObject
    {
        public Dictionary<string, GetterSetter> Fields = new Dictionary<string, GetterSetter>();

        public object Obj { get; set; }

        public WrappedObject(object obj)
        {
            Obj = obj;
        }

        public object GetFieldValue(string name)
        {
            return Fields[name].Get();
        }

        public WrappedObject CopyWithNewObject(object obj)
        {
            return new WrappedObject(obj)
            {
                Fields = Fields.ToDictionary(e => e.Key, e => e.Value.Clone(obj))
            };
        }

        public void SetValue(WrappedObject to, string name)
        {
            var value = to.Fields[name].Get();
            Fields[name].Set(value);
        }
    }
}
