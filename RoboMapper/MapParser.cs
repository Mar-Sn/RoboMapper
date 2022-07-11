
namespace RoboMapper
{
    public abstract class MapParser<TIn, TOut> : IMapper<TIn, TOut>
    {
        public abstract TOut Map(TIn from);

        public abstract TIn Map(TOut to);
    }
}
