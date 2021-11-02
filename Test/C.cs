using System;
using System.Collections.Generic;
using System.Text;
using RoboMapper;

namespace Test
{
    [Mappable("B-C")]
    public class C
    {
        [MapIndex("Id")]
        public int Id3 { get; set; }
    }
}
