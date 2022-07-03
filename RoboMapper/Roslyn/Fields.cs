using System;
using System.Collections.Generic;

namespace RoboMapper.Roslyn
{
    public class Fields
    {
        private readonly Dictionary<string, Field> _data = new Dictionary<string, Field>();
        private readonly Dictionary<(Type, Type), string> _typePointer = new Dictionary<(Type, Type), string>();
        public void TryAdd(Field field)
        {
            _data.TryAdd(field.Name, field);
            _typePointer.TryAdd((field.A, field.B), field.Name);
            _typePointer.TryAdd((field.B, field.A), field.Name);
        }

        public Field? TryGet(Type a, Type b)
        {
            if (_typePointer.TryGetValue((a, b), out var key))
            {
                return _data[key];
            }

            return null;
        }

        public IEnumerable<Field> Values => _data.Values;
        public Dictionary<string, Field> Data => _data;
    }
}