using RoboMapper;

namespace Test
{
    [Mappable("A-B")]
    public class A
    {
        [MapIndex("Id")]
        public int Id1 { get; set; }
    }
}
