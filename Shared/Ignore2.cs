using RoboMapper;

namespace Shared;

[Mappable("IgnoreTest")]
public class Ignore2
{
    [MapIndex("Test1")]
    public string Test1 { get; set; }   
}