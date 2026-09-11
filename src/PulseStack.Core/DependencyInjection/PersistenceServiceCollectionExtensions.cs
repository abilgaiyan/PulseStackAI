using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulseStack.Abstractions.Persistence.AIAssets.Mapping;
using PulseStack.Abstractions.Persistence.AIAssets.Serialization;
using PulseStack.Abstractions.Persistence.AIAssets.Storage;
using PulseStack.Abstractions.Persistence.AIAssets.Validation;
using PulseStack.Abstractions.Persistence.Storage;
using PulseStack.Core.Persistence.AIAssets.Mapping;
using PulseStack.Core.Persistence.AIAssets.Storage;
using PulseStack.Core.Persistence.AIAssets.Validation;
using PulseStack.Core.Persistence.Storage.Workflows;
using PulseStack.Core.Persistence.Storage.WorkflowPackages;

namespace PulseStack.Core.DependencyInjection;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddAIAssetDocumentCodec(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IAIAssetDocumentCodec, AIAssetDocumentCodec>();

        return services;
    }

    public static IServiceCollection AddAIAssetStorage(
        this IServiceCollection services,
        ISerializedAIAssetStore store,
        AIAssetStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(store);
        AIAssetStorageContract.EnsureValidOptions(options);
        EnsureStorageCompositionAvailable(services);

        services.AddSingleton(store);
        AddAIAssetStorageComposition(services, options);
        return services;
    }

    public static IServiceCollection AddInMemoryAIAssetStorage(
        this IServiceCollection services,
        AIAssetStorageOptions options,
        InMemorySerializedAIAssetStoreNamespace? storeNamespace = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        AIAssetStorageContract.EnsureValidOptions(options);
        EnsureStorageCompositionAvailable(services);

        services.AddSingleton<ISerializedAIAssetStore>(
            new InMemorySerializedAIAssetStore(
                storeNamespace ?? new InMemorySerializedAIAssetStoreNamespace()));
        AddAIAssetStorageComposition(services, options);
        return services;
    }

    public static IServiceCollection AddFileAIAssetStorage(
        this IServiceCollection services,
        string rootPath,
        AIAssetStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        AIAssetStorageContract.EnsureValidOptions(options);
        EnsureStorageCompositionAvailable(services);

        services.AddSingleton<ISerializedAIAssetStore>(
            new FileSerializedAIAssetStore(rootPath));
        AddAIAssetStorageComposition(services, options);
        return services;
    }

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

    private static void AddAIAssetStorageComposition(
        IServiceCollection services,
        AIAssetStorageOptions options)
    {
        services.AddAIAssetDocumentCodec();
        services.TryAddSingleton<IAIAssetDocumentValidator, AIAssetDocumentValidator>();
        services.TryAddSingleton<IAIAssetDocumentMapper, AIAssetDocumentMapper>();
        services.AddSingleton(options);

        services.AddSingleton<IAIAssetWriter>(provider =>
            new AIAssetWriter(
                GetExactlyOne<ISerializedAIAssetStore>(provider),
                GetExactlyOne<IAIAssetDocumentCodec>(provider),
                GetExactlyOne<IAIAssetDocumentValidator>(provider),
                GetExactlyOne<AIAssetStorageOptions>(provider)));

        services.AddSingleton<IAIAssetLoader>(provider =>
            new AIAssetLoader(
                GetExactlyOne<ISerializedAIAssetStore>(provider),
                GetExactlyOne<IAIAssetDocumentCodec>(provider),
                GetExactlyOne<IAIAssetDocumentValidator>(provider),
                GetExactlyOne<IAIAssetDocumentMapper>(provider),
                GetExactlyOne<AIAssetStorageOptions>(provider)));
    }

    private static void EnsureStorageCompositionAvailable(IServiceCollection services)
    {
        if (services.Any(descriptor =>
                descriptor.ServiceType == typeof(ISerializedAIAssetStore)
                || descriptor.ServiceType == typeof(AIAssetStorageOptions)
                || descriptor.ServiceType == typeof(IAIAssetWriter)
                || descriptor.ServiceType == typeof(IAIAssetLoader)))
        {
            throw new AIAssetStorageException(
                AIAssetStorageFailureCategory.CompositionConfiguration,
                "AI Asset storage composition has already been configured or partially configured.");
        }
    }

    private static TService GetExactlyOne<TService>(IServiceProvider provider)
        where TService : class
    {
        var services = provider.GetServices<TService>().Take(2).ToArray();
        if (services.Length != 1)
        {
            throw new AIAssetStorageException(
                AIAssetStorageFailureCategory.CompositionConfiguration,
                $"Exactly one {typeof(TService).Name} authority must be configured.");
        }

        return services[0];
    }
}
