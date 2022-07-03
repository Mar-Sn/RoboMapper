using RoboMapper;

namespace Shared.CustomParser;

[MapParser("NotMapped")]
public class NotMappedParser: IMapper<NotMappedA, NotMappedB>
{
    public NotMappedB Map(NotMappedA from) => new ()
    {
        CanMapThis = from.CanMapThis?.ToString()
    };
    
    public NotMappedA Map(NotMappedB to)
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