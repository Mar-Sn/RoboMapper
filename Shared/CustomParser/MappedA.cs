using RoboMapper;

namespace Shared.CustomParser;

public class MappedA
{
    [MapIndex("customParser", "NotMapped")]
    public NotMappedA NotMappedA { get; set; }
}