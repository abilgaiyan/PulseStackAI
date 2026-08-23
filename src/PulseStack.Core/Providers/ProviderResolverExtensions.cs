using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulseStack.Abstractions.Providers;

namespace PulseStack.Core.Providers;

internal static class ProviderResolverExtensions
{
    public static IServiceCollection AddProviderResolver(
        this IServiceCollection services)
    {
        services.TryAddSingleton<IProviderResolver, ProviderResolver>();
        return services;
    }
}
