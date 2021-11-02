using RoboMapper;

namespace Test
{
    [Mappable("D-E")]
    public class E
    {
        [MapIndex("A")]
        public B B1 { get; set; }
    }
}
