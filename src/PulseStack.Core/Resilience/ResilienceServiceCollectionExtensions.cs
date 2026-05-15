using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace PulseStack.Core.Resilience;

public static class ResilienceServiceCollectionExtensions
{
    public static IServiceCollection AddPulseStackResilience(
        this IServiceCollection services)
    {
        services.AddHttpClient("PulseStack")
            .AddStandardResilienceHandler();

        return services;
    }
}
