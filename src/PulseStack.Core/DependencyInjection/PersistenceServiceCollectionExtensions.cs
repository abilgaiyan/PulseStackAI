using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulseStack.Abstractions.Persistence.Storage;
using PulseStack.Core.Persistence.Storage.Workflows;
using PulseStack.Core.Persistence.Storage.WorkflowPackages;

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

     public static IServiceCollection AddInMemoryWorkflowPackageStorage(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IWorkflowPackageStore, InMemoryWorkflowPackageStore>();

        return services;
    }

    public static IServiceCollection AddFileWorkflowPackageStorage(
        this IServiceCollection services,
        string rootPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        services.TryAddSingleton<IWorkflowPackageStore>(
            _ => new FileWorkflowPackageStore(rootPath));

        return services;
    }
}