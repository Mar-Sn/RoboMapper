using RoboMapper;

namespace Shared.Inherentance;

[Mappable("AWithBase-BWithBase")]
public class BWithBase: BaseB
{
    [MapIndex("Field1")]
    public int FieldB { get; set; } 
}