using RoboMapper;

namespace Shared.Inheritance;

public class BWithBase: BaseB
{
    [MapIndex("Field1")]
    public int FieldB { get; set; } 
}