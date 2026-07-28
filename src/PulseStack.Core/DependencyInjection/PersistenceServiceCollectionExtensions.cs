using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulseStack.Core.Persistence.Storage.Workflows;
using PulseStack.Abstractions.Persistence.Storage;

namespace PulseStack.Core.DependencyInjection;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddInMemoryWorkflowStorage(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IWorkflowStore, InMemoryWorkflowStore>();

        return services;
    }

    public static IServiceCollection AddFileWorkflowStorage(
        this IServiceCollection services,
        string rootPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        services.TryAddSingleton<IWorkflowStore>(
            _ => new FileWorkflowStore(rootPath));

        return services;
    }
}