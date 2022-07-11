using System.Diagnostics;

namespace RoboMapper
{
    public interface IMapper<TFrom, TTo>
    {
        public TTo Map(TFrom from);

        public TFrom Map(TTo to);
    }
    
}
