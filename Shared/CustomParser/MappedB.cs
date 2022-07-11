using RoboMapper;

namespace Shared.CustomParser;

public class MappedB
{
    [MapIndex("customParser", "NotMapped")]
    public NotMappedB NotMappedB { get; set; }
}