using RoboMapper;

namespace Shared.CustomParser;

[Mappable("MappedA-MappedB")]
public class MappedB
{
    [MapIndex("customParser", "NotMapped")]
    public NotMappedB NotMappedB { get; set; }
}