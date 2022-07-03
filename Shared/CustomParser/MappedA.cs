using RoboMapper;

namespace Shared.CustomParser;

[Mappable("MappedA-MappedB")]
public class MappedA
{
    [MapIndex("customParser", "NotMapped")]
    public NotMappedA NotMappedA { get; set; }
}