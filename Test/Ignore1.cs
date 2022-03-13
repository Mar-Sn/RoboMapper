using RoboMapper;

namespace Test;

[Mappable("IgnoreTest")]
public class Ignore1
{
    [MapIndex("Test1")]
    public string Test1 { get; set; }   
    
    [MapIgnore]
    public string Test2 { get; set; }
}