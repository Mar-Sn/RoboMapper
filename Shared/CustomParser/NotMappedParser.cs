using RoboMapper;

namespace Shared.CustomParser;

public class NotMappedParser: MapParser<NotMappedA, NotMappedB>
{
    public override NotMappedB Map(NotMappedA from) => new ()
    {
        CanMapThis = from.CanMapThis?.ToString()
    };
    
    public override NotMappedA Map(NotMappedB to)
    {
        if (bool.TryParse(to.CanMapThis, out var parsed))
        {
            return new NotMappedA
            {
                CanMapThis = parsed
            }; 
        }
        return new NotMappedA
        {
            CanMapThis = null
        };
    }
}