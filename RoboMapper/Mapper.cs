using System;

namespace RoboMapper
{
    internal class Mapper<TFrom, TTo>: IMapper<TFrom, TTo>
    {
        private readonly WrappedObject _from;
        private readonly WrappedObject _to;

        public Mapper(WrappedObject from, WrappedObject to)
        {
            _from = @from;
            _to = to;
        }

        public TTo Map(TFrom from)
        {
            var fromA = _from.CopyWithNewObject(from);
            var toB = _to.CopyWithNewObject(Activator.CreateInstance(typeof(TTo)));
            foreach (var keyValuePair in fromA.Fields)
            {
                toB.SetValue(fromA, keyValuePair.Key);
            }

            return (TTo) toB.Obj;
        }

        public TFrom Map(TTo to)
        {
            var fromTo = _to.CopyWithNewObject(to);
            var toFrom = _from.CopyWithNewObject(Activator.CreateInstance(typeof(TFrom)));
            foreach (var keyValuePair in fromTo.Fields)
            {
                toFrom.SetValue(fromTo, keyValuePair.Key);
            }

            return (TFrom)toFrom.Obj;
        }
    }
}
