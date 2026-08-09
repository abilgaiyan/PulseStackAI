using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulseStack.Abstractions.Models;
using PulseStack.Core.Models;

namespace PulseStack.Core.DependencyInjection;

public static class ModelCatalogServiceCollectionExtensions
{
    public static IServiceCollection AddPulseStackModelCatalog(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IModelCatalog>(sp =>
        {
            var sources =
                sp.GetServices<IModelCatalogSource>();

            return new ModelCatalog(sources);
        });

        return services;
    }
}