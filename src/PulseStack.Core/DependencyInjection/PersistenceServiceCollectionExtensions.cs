using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using PulseStack.Abstractions.Persistence.AIAssets.Mapping;
using PulseStack.Abstractions.Persistence.AIAssets.Serialization;
using PulseStack.Abstractions.Persistence.AIAssets.Storage;
using PulseStack.Abstractions.Persistence.AIAssets.Validation;
using PulseStack.Abstractions.Persistence.Storage;
using PulseStack.Core.Persistence.AIAssets.Catalog;
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
        AddAIAssetStorageComposition(services, options, capabilityProfile: null);
        return services;
    }

    /// <summary>
    /// Records explicit durability evidence for a previously composed custom AI Asset storage authority.
    /// Built-in storage providers contribute this evidence automatically.
    /// </summary>
    public static IServiceCollection AddAIAssetStorageCapability(
        this IServiceCollection services,
        AIAssetStorageCapabilityProfile capabilityProfile)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(capabilityProfile);
        EnsureValidStorageCapability(capabilityProfile);

        var selections = services
            .Where(descriptor => descriptor.ServiceType == typeof(AIAssetLoaderAuthoritySelection))
            .ToArray();
        if (selections.Length != 1 || selections[0].Lifetime != ServiceLifetime.Singleton)
        {
            throw new AIAssetStorageException(
                AIAssetStorageFailureCategory.CompositionConfiguration,
                "Exactly one singleton AI Asset loader authority must be selected before storage durability evidence is recorded.");
        }

        if (services.Any(descriptor => descriptor.ServiceType == typeof(AIAssetStorageCapabilityProfile)))
        {
            throw new AIAssetStorageException(
                AIAssetStorageFailureCategory.CompositionConfiguration,
                "AI Asset storage durability capability has already been configured.");
        }

        services.AddSingleton(capabilityProfile);
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
        AddAIAssetStorageComposition(
            services,
            options,
            new AIAssetStorageCapabilityProfile(AIAssetAuthorityDurability.Transient));
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
        AddAIAssetStorageComposition(
            services,
            options,
            new AIAssetStorageCapabilityProfile(AIAssetAuthorityDurability.Durable));
        return services;
    }

    /// <summary>
    /// Configures one custom persistent catalog authority. The capability profile is explicit
    /// composition evidence and is not inferred from the provider implementation.
    /// </summary>
    public static IServiceCollection AddAIAssetCatalog(
        this IServiceCollection services,
        IAIAssetCatalogProvider catalogProvider,
        AIAssetCatalogCapabilityProfile capabilityProfile)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(catalogProvider);
        ArgumentNullException.ThrowIfNull(capabilityProfile);

        EnsureValidCatalogCapability(capabilityProfile);
        EnsureCatalogCompositionAvailable(services, capabilityProfile);

        services.AddSingleton(catalogProvider);
        AddAIAssetCatalogComposition(services, capabilityProfile);
        return services;
    }

    public static IServiceCollection AddInMemoryAIAssetCatalog(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var capabilityProfile = new AIAssetCatalogCapabilityProfile(AIAssetAuthorityDurability.Transient);
        EnsureCatalogCompositionAvailable(services, capabilityProfile);

        services.AddSingleton<IAIAssetCatalogProvider>(new InMemoryAIAssetCatalogProvider());
        AddAIAssetCatalogComposition(services, capabilityProfile);
        return services;
    }

    public static IServiceCollection AddFileAIAssetCatalog(
        this IServiceCollection services,
        string rootPath)
    {
        ArgumentNullException.ThrowIfNull(services);

        var capabilityProfile = new AIAssetCatalogCapabilityProfile(AIAssetAuthorityDurability.Durable);
        EnsureCatalogCompositionAvailable(services, capabilityProfile);

        services.AddSingleton<IAIAssetCatalogProvider>(new FileAIAssetCatalogProvider(rootPath));
        AddAIAssetCatalogComposition(services, capabilityProfile);
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
        AIAssetStorageOptions options,
        AIAssetStorageCapabilityProfile? capabilityProfile)
    {
        services.AddAIAssetDocumentCodec();
        services.TryAddSingleton<IAIAssetDocumentValidator, AIAssetDocumentValidator>();
        services.TryAddSingleton<IAIAssetDocumentMapper, AIAssetDocumentMapper>();
        services.AddSingleton(options);

        if (capabilityProfile is not null)
        {
            services.AddSingleton(capabilityProfile);
        }

        services.AddSingleton<IAIAssetWriter>(provider =>
            new AIAssetWriter(
                GetExactlyOneStorageService<ISerializedAIAssetStore>(provider),
                GetExactlyOneStorageService<IAIAssetDocumentCodec>(provider),
                GetExactlyOneStorageService<IAIAssetDocumentValidator>(provider),
                GetExactlyOneStorageService<AIAssetStorageOptions>(provider)));

        services.AddSingleton<AIAssetLoaderAuthoritySelection>(provider =>
            new AIAssetLoaderAuthoritySelection(
                new AIAssetLoader(
                    GetExactlyOneStorageService<ISerializedAIAssetStore>(provider),
                    GetExactlyOneStorageService<IAIAssetDocumentCodec>(provider),
                    GetExactlyOneStorageService<IAIAssetDocumentValidator>(provider),
                    GetExactlyOneStorageService<IAIAssetDocumentMapper>(provider),
                    GetExactlyOneStorageService<AIAssetStorageOptions>(provider))));

        services.AddSingleton<IAIAssetLoader>(provider =>
            provider.GetRequiredService<AIAssetLoaderAuthoritySelection>().Loader);
    }

    private static void AddAIAssetCatalogComposition(
        IServiceCollection services,
        AIAssetCatalogCapabilityProfile capabilityProfile)
    {
        services.AddSingleton(capabilityProfile);
        services.AddSingleton<IAIAssetPublisher>(provider =>
            new AIAssetPublisher(
                GetExactlyOneCatalogService<IAIAssetCatalogProvider>(provider),
                GetSelectedLoaderAuthority(provider)));
        services.AddSingleton<IPersistentAIAssetResolver>(provider =>
            new PersistentAIAssetResolver(
                GetExactlyOneCatalogService<IAIAssetCatalogProvider>(provider),
                GetSelectedLoaderAuthority(provider)));
    }

    private static void EnsureStorageCompositionAvailable(IServiceCollection services)
    {
        if (services.Any(descriptor =>
                descriptor.ServiceType == typeof(ISerializedAIAssetStore)
                || descriptor.ServiceType == typeof(AIAssetStorageOptions)
                || descriptor.ServiceType == typeof(AIAssetStorageCapabilityProfile)
                || descriptor.ServiceType == typeof(AIAssetLoaderAuthoritySelection)
                || descriptor.ServiceType == typeof(IAIAssetWriter)
                || descriptor.ServiceType == typeof(IAIAssetLoader)))
        {
            throw new AIAssetStorageException(
                AIAssetStorageFailureCategory.CompositionConfiguration,
                "AI Asset storage composition has already been configured or partially configured.");
        }
    }

    private static void EnsureCatalogCompositionAvailable(
        IServiceCollection services,
        AIAssetCatalogCapabilityProfile catalogCapability)
    {
        if (services.Any(descriptor =>
                descriptor.ServiceType == typeof(IAIAssetCatalogProvider)
                || descriptor.ServiceType == typeof(AIAssetCatalogCapabilityProfile)
                || descriptor.ServiceType == typeof(IAIAssetPublisher)
                || descriptor.ServiceType == typeof(IPersistentAIAssetResolver)))
        {
            throw CatalogCompositionFailure(
                "AI Asset catalog composition has already been configured or partially configured.");
        }

        var loaderSelections = services
            .Where(descriptor => descriptor.ServiceType == typeof(AIAssetLoaderAuthoritySelection))
            .ToArray();
        if (loaderSelections.Length != 1 || loaderSelections[0].Lifetime != ServiceLifetime.Singleton)
        {
            throw CatalogCompositionFailure(
                "Exactly one singleton AI Asset loader authority must be selected before the persistent catalog.");
        }

        var storageCapabilities = services
            .Where(descriptor => descriptor.ServiceType == typeof(AIAssetStorageCapabilityProfile))
            .ToArray();
        if (storageCapabilities.Length != 1
            || storageCapabilities[0].ImplementationInstance is not AIAssetStorageCapabilityProfile storageCapability)
        {
            throw CatalogCompositionFailure(
                "Exactly one explicit AI Asset storage durability capability must be configured before the persistent catalog.");
        }

        EnsureValidStorageCapabilityForCatalog(storageCapability);
        EnsureDurabilityCompatible(storageCapability, catalogCapability);
    }

    private static void EnsureDurabilityCompatible(
        AIAssetStorageCapabilityProfile storageCapability,
        AIAssetCatalogCapabilityProfile catalogCapability)
    {
        if (catalogCapability.Durability == AIAssetAuthorityDurability.Durable
            && storageCapability.Durability == AIAssetAuthorityDurability.Transient)
        {
            throw CatalogCompositionFailure(
                "A durable AI Asset catalog cannot be composed over transient serialized AI Asset storage.");
        }
    }

    private static void EnsureValidStorageCapability(AIAssetStorageCapabilityProfile capabilityProfile)
    {
        if (!Enum.IsDefined(capabilityProfile.Durability))
        {
            throw new AIAssetStorageException(
                AIAssetStorageFailureCategory.CompositionConfiguration,
                $"Unsupported AI Asset storage durability value '{capabilityProfile.Durability}'.");
        }
    }

    private static void EnsureValidStorageCapabilityForCatalog(AIAssetStorageCapabilityProfile capabilityProfile)
    {
        if (!Enum.IsDefined(capabilityProfile.Durability))
        {
            throw CatalogCompositionFailure(
                $"Unsupported AI Asset storage durability value '{capabilityProfile.Durability}'.");
        }
    }

    private static void EnsureValidCatalogCapability(AIAssetCatalogCapabilityProfile capabilityProfile)
    {
        if (!Enum.IsDefined(capabilityProfile.Durability))
        {
            throw CatalogCompositionFailure(
                $"Unsupported AI Asset catalog durability value '{capabilityProfile.Durability}'.");
        }
    }

    private static AIAssetCatalogException CatalogCompositionFailure(string message) =>
        new(
            AIAssetCatalogFailureCategory.CompositionConfiguration,
            message,
            new AIAssetCatalogDiagnosticContext("Compose"));

    private static TService GetExactlyOneStorageService<TService>(IServiceProvider provider)
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

    private static TService GetExactlyOneCatalogService<TService>(IServiceProvider provider)
        where TService : class
    {
        var services = provider.GetServices<TService>().Take(2).ToArray();
        if (services.Length != 1)
        {
            throw CatalogCompositionFailure(
                $"Exactly one {typeof(TService).Name} authority must be configured.");
        }

        return services[0];
    }

    private static IAIAssetLoader GetSelectedLoaderAuthority(IServiceProvider provider) =>
        provider.GetRequiredService<AIAssetLoaderAuthoritySelection>().Loader;
}
