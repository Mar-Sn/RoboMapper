using RoboMapper;

namespace Test
{
    [Mappable("D-E")]
    public class D
    {
        [MapIndex("A")]
        public A A { get; set; }
    }
}
