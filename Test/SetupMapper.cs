#nullable enable
using Microsoft.Extensions.Logging;
using Shared;
using Shared.CustomParser;
using Shared.Inherentance;
using Shared.Inheritance;

namespace Test;

public class SetupMapper
{
    private static SetupMapper? _instance = null;

    public static SetupMapper Instance { get
    {
        return _instance ??= new SetupMapper();
    }}
    
    private SetupMapper()
    {
        var logger = new LoggerFactory();
        RoboMapper.RoboMapper.Init(logger.CreateLogger(nameof(TestSimpleConversion)));
        RoboMapper.RoboMapper.Bind<A, B>();
        RoboMapper.RoboMapper.Bind<B, C>();
        RoboMapper.RoboMapper.Bind<D, E>();
        RoboMapper.RoboMapper.Bind<AWithBase, BWithBase>();
        RoboMapper.RoboMapper.Bind<Ignore1, Ignore2>();
        RoboMapper.RoboMapper.Bind<MappedA, MappedB>(e =>
        {
            e.MapWith<NotMappedParser>(nameof(MappedA.NotMappedA), nameof(MappedB.NotMappedB));
        });
        RoboMapper.RoboMapper.LoadAssembly();    
    }

    public void Init()
    {
        //
    }
}