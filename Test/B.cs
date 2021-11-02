using RoboMapper;

namespace Test
{
    [Mappable("A-B", "B-C")]
    public class B
    {
        [MapIndex("Id")]
        public int Id2 { get; set; }
    }
}
