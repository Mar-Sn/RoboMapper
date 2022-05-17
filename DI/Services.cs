using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DI;

public static class Services
{
    public static void AddRoboMappers(this IServiceCollection services, ILogger logger)
    {
        RoboMapper.RoboMapper.Init(logger);
        var allMappers = RoboMapper.RoboMapper.GetMappers();
        foreach (var mapper in allMappers)
        {
            services.AddSingleton(mapper.Item1, mapper.Item2);
        }
    }
}