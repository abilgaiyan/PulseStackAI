using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulseStack.Abstractions.Providers;
using PulseStack.Core.Providers;

namespace PulseStack.Core.DependencyInjection;

internal static class ProviderResolverRegistration
{
    public static IServiceCollection AddProviderResolver(
        this IServiceCollection services)
    {
        services.TryAddSingleton<IProviderResolver, ProviderResolver>();
        return services;
    }
}
