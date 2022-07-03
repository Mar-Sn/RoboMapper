using RoboMapper;

namespace Shared.Inherentance;

[Mappable("AWithBase-BWithBase")]
public class AWithBase: BaseA
{
   [MapIndex("Field1")]
   public int FieldA { get; set; } 
}