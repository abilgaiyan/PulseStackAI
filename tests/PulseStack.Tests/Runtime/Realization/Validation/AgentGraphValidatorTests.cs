using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Runtime.Realization.Resolution;
using PulseStack.Abstractions.Runtime.Realization.Validation;
using PulseStack.Core.Assets;
using PulseStack.Core.DependencyInjection;
using PulseStack.Core.Runtime.Realization.Resolution;
using PulseStack.Core.Runtime.Realization.Validation;
using Xunit;

namespace PulseStack.Tests.Runtime.Realization.Validation;

public sealed class AgentGraphValidatorTests
{
    [Fact]
    public async Task ValidateAsync_ShouldRequireModelButAllowMissingPromptAndMemory()
    {
        var validator = new AgentGraphValidator(new DictionaryCatalog([]));
        var definition = CreateAgent();

        var result = await validator.ValidateAsync(definition);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be(new AgentGraphValidationError(
                AgentGraphValidationCodes.MissingRequiredModel,
                "Agent realization requires a Model Asset reference.",
                "$.options.model"));
    }

    [Fact]
    public async Task ValidateAsync_ShouldAcceptExactModelWithOptionalPromptAndMemoryMissing()
    {
        var model = CreateAsset(AssetType.Model, "model");
        var definition = CreateAgent(model: Reference(model));
        var validator = new AgentGraphValidator(new DictionaryCatalog([model]));

        var result = await validator.ValidateAsync(definition);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_ShouldReportWrongTypedFieldReference()
    {
        var model = CreateAsset(AssetType.Model, "model");
        var tool = CreateAsset(AssetType.Tool, "tool");
        var definition = CreateAgent(
            model: Reference(model),
            prompt: Reference(tool));
        var validator = new AgentGraphValidator(new DictionaryCatalog([model, tool]));

        var result = await validator.ValidateAsync(definition);

        result.Errors.Should().ContainSingle()
            .Which.Should().Be(new AgentGraphValidationError(
                AgentGraphValidationCodes.InvalidReferenceType,
                "Agent reference must target Asset type 'Prompt'.",
                "$.options.prompt"));
    }

    [Fact]
    public async Task ValidateAsync_ShouldReportUnavailableDefinitionKey()
    {
        var model = CreateAsset(AssetType.Model, "model");
        var missingTool = CreateAsset(AssetType.Tool, "missing-tool");
        var definition = CreateAgent(
            model: Reference(model),
            tools: [Reference(missingTool)]);
        var validator = new AgentGraphValidator(new DictionaryCatalog([model]));

        var result = await validator.ValidateAsync(definition);

        result.Errors.Should().ContainSingle();
        result.Errors[0].Code.Should().Be(AgentGraphValidationCodes.DefinitionUnavailable);
        result.Errors[0].Path.Should().Be("$.options.tools[0]");
        result.Errors[0].Message.Should().Contain(missingTool.Id.ToString());
    }

    [Fact]
    public async Task ValidateAsync_ShouldReportUrnConflictForExistingDefinitionKey()
    {
        var model = CreateAsset(AssetType.Model, "model");
        var tool = CreateAsset(AssetType.Tool, "tool");
        var conflictingReference = new AssetReference(
            tool.Type,
            tool.Id,
            new AssetUrn("urn:pulsestack:tool:conflict"),
            tool.Version);
        var definition = CreateAgent(
            model: Reference(model),
            tools: [conflictingReference]);
        var validator = new AgentGraphValidator(new DictionaryCatalog([model, tool]));

        var result = await validator.ValidateAsync(definition);

        result.Errors.Should().ContainSingle()
            .Which.Should().Be(new AgentGraphValidationError(
                AgentGraphValidationCodes.ReferenceUrnConflict,
                "Referenced Asset definition URN does not match the exact Agent reference.",
                "$.options.tools[0]"));
    }

    [Fact]
    public async Task ValidateAsync_ShouldDefensivelyRejectCatalogDefinitionKeyMismatch()
    {
        var model = CreateAsset(AssetType.Model, "model");
        var wrongAsset = CreateAsset(AssetType.Model, "wrong-model");
        var definition = CreateAgent(model: Reference(model));
        var validator = new AgentGraphValidator(new NonConformingCatalog(wrongAsset));

        var result = await validator.ValidateAsync(definition);

        result.Errors.Should().ContainSingle()
            .Which.Should().Be(new AgentGraphValidationError(
                AgentGraphValidationCodes.CatalogDefinitionKeyMismatch,
                "Asset catalog returned a definition inconsistent with the requested definition key.",
                "$.options.model"));
    }

    [Fact]
    public async Task ValidateAsync_ShouldResolveMultipleVersionsOfSameAssetIdIndependently()
    {
        var model = CreateAsset(AssetType.Model, "model");
        var sharedId = AssetId.New();
        var toolV1 = CreateAsset(
            AssetType.Tool,
            "tool-v1",
            sharedId,
            new AssetVersion("1.0.0"));
        var toolV2 = CreateAsset(
            AssetType.Tool,
            "tool-v2",
            sharedId,
            new AssetVersion("2.0.0"));
        var definition = CreateAgent(
            model: Reference(model),
            tools: [Reference(toolV1), Reference(toolV2)]);
        var catalog = new DictionaryCatalog([model, toolV1, toolV2]);
        var validator = new AgentGraphValidator(catalog);

        var result = await validator.ValidateAsync(definition);

        result.IsValid.Should().BeTrue();
        catalog.RequestedKeys.Should().ContainInOrder(
            AssetDefinitionKey.From(Reference(model)),
            AssetDefinitionKey.From(Reference(toolV1)),
            AssetDefinitionKey.From(Reference(toolV2)));
    }

    [Fact]
    public async Task ValidateAsync_ShouldAggregateErrorsInCanonicalAgentGraphOrder()
    {
        var wrongModel = CreateAsset(AssetType.Tool, "wrong-model");
        var prompt = CreateAsset(AssetType.Prompt, "prompt");
        var knowledge0 = CreateAsset(AssetType.Knowledge, "knowledge-0");
        var knowledge1 = CreateAsset(AssetType.Knowledge, "knowledge-1");
        var tool = CreateAsset(AssetType.Tool, "tool");
        var memory = CreateAsset(AssetType.Memory, "memory");
        var policy = CreateAsset(AssetType.Policy, "policy");

        var definition = CreateAgent(
            model: Reference(wrongModel),
            prompt: Reference(prompt),
            knowledge: [Reference(knowledge0), Reference(knowledge1)],
            tools: [Reference(tool)],
            memory: Reference(memory),
            policies: [Reference(policy)]);

        var validator = new AgentGraphValidator(new DictionaryCatalog([]));

        var result = await validator.ValidateAsync(definition);

        result.Errors.Select(error => (error.Code, error.Path)).Should().Equal(
            (AgentGraphValidationCodes.InvalidReferenceType, "$.options.model"),
            (AgentGraphValidationCodes.DefinitionUnavailable, "$.options.prompt"),
            (AgentGraphValidationCodes.DefinitionUnavailable, "$.options.knowledge[0]"),
            (AgentGraphValidationCodes.DefinitionUnavailable, "$.options.knowledge[1]"),
            (AgentGraphValidationCodes.DefinitionUnavailable, "$.options.tools[0]"),
            (AgentGraphValidationCodes.DefinitionUnavailable, "$.options.memory"),
            (AgentGraphValidationCodes.DefinitionUnavailable, "$.options.policies[0]"));
    }

    [Fact]
    public async Task ValidateAsync_ShouldIgnoreCommonReferencesAndDependenciesOutsideTypedGraph()
    {
        var model = CreateAsset(AssetType.Model, "model");
        var unrelated = CreateAsset(AssetType.Tool, "unrelated");
        var definition = CreateAgent(model: Reference(model)) with
        {
            References = [Reference(unrelated)],
            Dependencies = [new AssetDependency(Reference(unrelated))]
        };
        var catalog = new DictionaryCatalog([model]);
        var validator = new AgentGraphValidator(catalog);

        var result = await validator.ValidateAsync(definition);

        result.IsValid.Should().BeTrue();
        catalog.RequestedKeys.Should().ContainSingle()
            .Which.Should().Be(AssetDefinitionKey.From(Reference(model)));
    }

    [Fact]
    public async Task ValidateAsync_ShouldHonorCancellationBeforeTraversal()
    {
        var model = CreateAsset(AssetType.Model, "model");
        var catalog = new DictionaryCatalog([model]);
        var validator = new AgentGraphValidator(catalog);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = async () =>
            await validator.ValidateAsync(
                CreateAgent(model: Reference(model)),
                cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        catalog.RequestedKeys.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_ShouldHonorCancellationImmediatelyAfterCatalogLookup()
    {
        var model = CreateAsset(AssetType.Model, "model");
        using var cancellation = new CancellationTokenSource();
        var catalog = new CancellingNonCooperativeCatalog(model, cancellation);
        var validator = new AgentGraphValidator(catalog);

        var action = async () =>
            await validator.ValidateAsync(
                CreateAgent(model: Reference(model)),
                cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        catalog.LookupCount.Should().Be(1);
    }

    [Fact]
    public void AgentGraphValidationResult_ShouldSnapshotErrors()
    {
        var source = new List<AgentGraphValidationError>
        {
            new("AGG999", "first", "$.first")
        };
        var result = new AgentGraphValidationResult(source);

        source.Add(new AgentGraphValidationError("AGG998", "second", "$.second"));

        result.Errors.Should().ContainSingle()
            .Which.Should().Be(new AgentGraphValidationError("AGG999", "first", "$.first"));
    }

    [Fact]
    public void AddPulseStack_ShouldExposeGraphValidatorAndSharedCatalogResolver()
    {
        var services = new ServiceCollection();
        services.AddPulseStack();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var validator = scope.ServiceProvider.GetRequiredService<IAgentGraphValidator>();
        var resolver = scope.ServiceProvider.GetRequiredService<IAssetResolver>();
        var catalog = scope.ServiceProvider.GetRequiredService<IAssetDefinitionCatalog>();

        validator.Should().BeOfType<AgentGraphValidator>();
        resolver.Should().BeOfType<InMemoryAssetResolver>();
        catalog.Should().BeSameAs(resolver);
    }

    private static AgentDefinition CreateAgent(
        AssetReference? model = null,
        AssetReference? prompt = null,
        IReadOnlyCollection<AssetReference>? knowledge = null,
        IReadOnlyCollection<AssetReference>? tools = null,
        AssetReference? memory = null,
        IReadOnlyCollection<AssetReference>? policies = null)
        => new AgentDefinitionFactory().Create(
            new AgentDefinitionOptions
            {
                Name = "Graph Agent",
                Goal = "Validate the declarative realization graph.",
                Role = "Graph validation fixture",
                Model = model,
                Prompt = prompt,
                Knowledge = knowledge ?? [],
                Tools = tools ?? [],
                Memory = memory,
                Policies = policies ?? []
            });

    private static AssetReference Reference(IAsset asset)
        => new(asset.Type, asset.Id, asset.Urn, asset.Version);

    private static TestAsset CreateAsset(
        AssetType type,
        string name,
        AssetId? id = null,
        AssetVersion? version = null)
    {
        var assetId = id ?? AssetId.New();
        var assetVersion = version ?? AssetVersion.Initial;

        return new TestAsset(type)
        {
            Id = assetId,
            Urn = new AssetUrn(
                $"urn:pulsestack:{type.ToString().ToLowerInvariant()}:{name}:{assetVersion.Value}"),
            Version = assetVersion,
            Metadata = new AssetMetadata { Name = name },
            Lifecycle = AssetLifecycle.Active
        };
    }

    private sealed record TestAsset : Asset
    {
        public TestAsset(AssetType type)
            : base(type)
        {
        }
    }

    private sealed class DictionaryCatalog : IAssetDefinitionCatalog
    {
        private readonly IReadOnlyDictionary<AssetDefinitionKey, IAsset> assets;

        public DictionaryCatalog(IEnumerable<IAsset> assets)
        {
            this.assets = assets.ToDictionary(AssetDefinitionKey.From);
        }

        public List<AssetDefinitionKey> RequestedKeys { get; } = [];

        public ValueTask<IAsset?> FindAsync(
            AssetDefinitionKey key,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestedKeys.Add(key);
            assets.TryGetValue(key, out var asset);
            return ValueTask.FromResult(asset);
        }
    }

    private sealed class NonConformingCatalog(IAsset asset) : IAssetDefinitionCatalog
    {
        public ValueTask<IAsset?> FindAsync(
            AssetDefinitionKey key,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IAsset?>(asset);
    }

    private sealed class CancellingNonCooperativeCatalog(
        IAsset asset,
        CancellationTokenSource cancellation) : IAssetDefinitionCatalog
    {
        public int LookupCount { get; private set; }

        public async ValueTask<IAsset?> FindAsync(
            AssetDefinitionKey key,
            CancellationToken cancellationToken = default)
        {
            LookupCount++;
            await Task.Yield();
            cancellation.Cancel();
            return asset;
        }
    }
}
