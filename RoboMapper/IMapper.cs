using System;
using System.Collections.Generic;
using System.Text;

namespace RoboMapper
{
    public interface IMapper<TFrom, TTo>
    {
        TTo Map(TFrom from);

        TFrom Map(TTo to);
    }
}
