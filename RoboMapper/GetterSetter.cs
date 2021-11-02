using System;
using System.Reflection;

namespace RoboMapper
{
    internal class GetterSetter
    {
        public delegate void SetterAction(object backing, object[] obj);

        public object BackingInstance { get; }

        public GetterSetter(object backingInstance)
        {
            BackingInstance = backingInstance;
        }

        public void Set(object to)
        {
            Setter.Invoke(BackingInstance, new[] { to });
        }

        public void Set(object[] to)
        {
            Setter.Invoke(BackingInstance, to);
        }

        public object Get()
        {
            return Getter.Invoke(BackingInstance, new object[]{});
        }

        public SetterAction Setter { get; set; }

        public MethodInfo Getter { get; set; }

        public GetterSetter Clone(object backingInstance)
        {
            return new GetterSetter(backingInstance)
            {
                Setter = Setter,
                Getter = Getter
            };
        }
    }
}
