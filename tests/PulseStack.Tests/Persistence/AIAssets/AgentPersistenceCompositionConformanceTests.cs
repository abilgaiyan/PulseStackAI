using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Chat;
using PulseStack.Abstractions.Knowledge;
using PulseStack.Abstractions.Memory;
using PulseStack.Abstractions.Models;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Mapping;
using PulseStack.Abstractions.Persistence.AIAssets.Validation;
using PulseStack.Abstractions.Policies;
using PulseStack.Abstractions.Providers;
using PulseStack.Abstractions.Runtime.Realization.Binding;
using PulseStack.Abstractions.Runtime.Realization.Composition;
using PulseStack.Abstractions.Runtime.Realization.Resolution;
using PulseStack.Abstractions.Runtime.Realization.Validation;
using PulseStack.Abstractions.Tools;
using PulseStack.Agents.DependencyInjection;
using PulseStack.Core.Assets;
using PulseStack.Core.DependencyInjection;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AgentPersistenceCompositionConformanceTests
{
    [Fact]
    public async Task CompleteAgent_ShouldComposeAcrossPersistenceAndRealizationBoundaries()
    {
        var fixture = CreateCompleteFixture();
        var services = CreateServices(fixture);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var mapper = scope.ServiceProvider.GetRequiredService<IAIAssetDocumentMapper>();
        var documentValidator = scope.ServiceProvider.GetRequiredService<IAIAssetDocumentValidator>();
        var graphValidator = scope.ServiceProvider.GetRequiredService<IAgentGraphValidator>();
        var composer = scope.ServiceProvider.GetRequiredService<IAgentComposer>();
        var resolver = scope.ServiceProvider.GetRequiredService<IAssetResolver>();
        var catalog = scope.ServiceProvider.GetRequiredService<IAssetDefinitionCatalog>();

        catalog.Should().BeSameAs(resolver);

        var document = mapper.ToDocument(fixture.Agent)
            .Should().BeOfType<AgentAssetDocument>().Subject;

        AssertPortableDocumentContainsDefinitionOnly(document, fixture);

        var documentValidation = await documentValidator.ValidateAsync(document);
        documentValidation.IsValid.Should().BeTrue();
        documentValidation.Errors.Should().BeEmpty();

        var reconstructed = mapper.FromDocument(document)
            .Should().BeOfType<AgentDefinition>().Subject;

        AssertCanonicalAgentEquality(fixture.Agent, reconstructed);

        var graphValidation = await graphValidator.ValidateAsync(reconstructed);
        graphValidation.IsValid.Should().BeTrue();
        graphValidation.Errors.Should().BeEmpty();

        var runtimeAgent = await composer.ComposeAsync(reconstructed);

        runtimeAgent.Should().NotBeNull();
        runtimeAgent.Name.Should().Be(fixture.Agent.Options.Name);
        fixture.Tool.ExecutionCount.Should().Be(0);
        fixture.Knowledge.RetrievalCount.Should().Be(0);
        fixture.Memory.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task InvalidPersistenceDocument_ShouldStopAtDocumentValidation()
    {
        var fixture = CreateCompleteFixture();
        var services = CreateServices(fixture);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var mapper = scope.ServiceProvider.GetRequiredService<IAIAssetDocumentMapper>();
        var validator = scope.ServiceProvider.GetRequiredService<IAIAssetDocumentValidator>();

        var canonical = mapper.ToDocument(fixture.Agent)
            .Should().BeOfType<AgentAssetDocument>().Subject;
        var invalid = CopyDocument(canonical, goal: "   ");

        var validation = await validator.ValidateAsync(invalid);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(error =>
            error.Code == AIAssetDocumentValidationCodes.MissingAgentGoal);

        // The conformance pipeline stops here: reconstruction, graph validation,
        // and composition are deliberately not invoked for an invalid document.
    }

    [Fact]
    public async Task ValidPersistenceDocumentWithIncompleteGraph_ShouldStopAtGraphValidation()
    {
        var fixture = CreateCompleteFixture();
        var services = CreateServices(fixture, registerToolAsset: false);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var mapper = scope.ServiceProvider.GetRequiredService<IAIAssetDocumentMapper>();
        var documentValidator = scope.ServiceProvider.GetRequiredService<IAIAssetDocumentValidator>();
        var graphValidator = scope.ServiceProvider.GetRequiredService<IAgentGraphValidator>();

        var document = mapper.ToDocument(fixture.Agent)
            .Should().BeOfType<AgentAssetDocument>().Subject;
        var documentValidation = await documentValidator.ValidateAsync(document);
        documentValidation.IsValid.Should().BeTrue();

        var reconstructed = mapper.FromDocument(document)
            .Should().BeOfType<AgentDefinition>().Subject;
        var graphValidation = await graphValidator.ValidateAsync(reconstructed);

        graphValidation.IsValid.Should().BeFalse();
        graphValidation.Errors.Should().ContainSingle(error =>
            error.Code == AgentGraphValidationCodes.DefinitionUnavailable
            && error.Path == "$.options.tools[0]");
        fixture.Tool.ExecutionCount.Should().Be(0);

        // The conformance pipeline stops here: AgentComposer is deliberately
        // not invoked for a structurally valid but realization-incomplete graph.
    }

    private static ServiceCollection CreateServices(
        CompleteFixture fixture,
        bool registerToolAsset = true)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IProviderResolver, StubProviderResolver>();
        services.AddSingleton<IAsset>(fixture.Model);
        services.AddSingleton<IAsset>(fixture.Prompt);
        services.AddSingleton<IAsset>(fixture.KnowledgeAsset);
        if (registerToolAsset)
        {
            services.AddSingleton<IAsset>(fixture.ToolAsset);
        }
        services.AddSingleton<IAsset>(fixture.MemoryAsset);
        services.AddSingleton<IAsset>(fixture.PolicyAsset);

        services.AddSingleton<ITool>(fixture.Tool);
        services.AddSingleton<IKnowledgeSource>(fixture.Knowledge);
        services.AddSingleton<IConversationMemoryFactory>(fixture.MemoryFactory);
        services.AddSingleton<IRuntimePolicy>(fixture.Policy);

        services.AddSingleton(new ToolBindingRegistration(
            Reference(fixture.ToolAsset), fixture.Tool.Name));
        services.AddSingleton(new KnowledgeBindingRegistration(
            Reference(fixture.KnowledgeAsset), fixture.Knowledge.Name));
        services.AddSingleton(new MemoryBindingRegistration(
            Reference(fixture.MemoryAsset), fixture.MemoryFactory.Name));
        services.AddSingleton(new PolicyBindingRegistration(
            Reference(fixture.PolicyAsset), fixture.Policy.Name));

        services.AddPulseStack();
        services.AddPulseStackAgents();

        return services;
    }

    private static CompleteFixture CreateCompleteFixture()
    {
        var model = new ModelAssetFactory(new StubModelCatalog()).Create(
            new ModelAssetOptions("Stub", "stub-model"));
        var prompt = new PromptAssetFactory().Create(
            new PromptAssetOptions
            {
                Name = "Operations Prompt",
                SystemInstructions = "Assist with the persisted operations workflow."
            });
        var knowledgeAsset = new KnowledgeAssetFactory().Create(
            new KnowledgeAssetOptions
            {
                Name = "Operations Knowledge",
                Description = "Persisted knowledge definition.",
                Tags = ["operations", "conformance"]
            });
        var toolAsset = new ToolAssetFactory().Create(
            new ToolAssetOptions
            {
                Name = "Operations Tool",
                Description = "Persisted tool definition.",
                Category = "Conformance",
                Tags = ["operations", "conformance"]
            });
        var memoryAsset = new MemoryAssetFactory().Create(
            new MemoryAssetOptions
            {
                Name = "Operations Memory",
                Description = "Persisted memory definition.",
                Tags = ["operations"]
            });
        var policyAsset = new PolicyAssetFactory().Create(
            new PolicyAssetOptions
            {
                Name = "Operations Policy",
                Description = "Persisted policy definition.",
                Tags = ["operations"]
            });

        var agent = new AgentDefinitionFactory().Create(
            new AgentDefinitionOptions
            {
                Name = "Portable Operations Agent",
                Goal = "Prove the complete Agent persistence lifecycle.",
                Role = "Persistence conformance worker",
                Responsibilities = ["Inspect", "Coordinate", "Report"],
                Model = Reference(model),
                Prompt = Reference(prompt),
                Knowledge = [Reference(knowledgeAsset)],
                Tools = [Reference(toolAsset)],
                Memory = Reference(memoryAsset),
                Policies = [Reference(policyAsset)]
            });

        var tool = new RecordingTool("operations-tool");
        var knowledge = new RecordingKnowledgeSource("operations-knowledge");
        var memory = new RecordingMemory();
        var memoryFactory = new StubMemoryFactory("operations-memory", memory);
        var policy = new StubRuntimePolicy("operations-policy");

        return new CompleteFixture(
            agent,
            model,
            prompt,
            knowledgeAsset,
            toolAsset,
            memoryAsset,
            policyAsset,
            tool,
            knowledge,
            memory,
            memoryFactory,
            policy);
    }

    private static void AssertCanonicalAgentEquality(
        AgentDefinition expected,
        AgentDefinition actual)
    {
        actual.Id.Should().Be(expected.Id);
        actual.Urn.Should().Be(expected.Urn);
        actual.Version.Should().Be(expected.Version);
        actual.Metadata.Should().Be(expected.Metadata);
        actual.Lifecycle.Should().Be(expected.Lifecycle);
        actual.Options.Name.Should().Be(expected.Options.Name);
        actual.Options.Goal.Should().Be(expected.Options.Goal);
        actual.Options.Role.Should().Be(expected.Options.Role);
        actual.Options.Responsibilities.Should().Equal(expected.Options.Responsibilities);
        actual.Options.Model.Should().Be(expected.Options.Model);
        actual.Options.Prompt.Should().Be(expected.Options.Prompt);
        actual.Options.Knowledge.Should().Equal(expected.Options.Knowledge);
        actual.Options.Tools.Should().Equal(expected.Options.Tools);
        actual.Options.Memory.Should().Be(expected.Options.Memory);
        actual.Options.Policies.Should().Equal(expected.Options.Policies);
        actual.References.Should().Equal(expected.References);
        actual.Dependencies.Should().Equal(expected.Dependencies);
    }

    private static void AssertPortableDocumentContainsDefinitionOnly(
        AgentAssetDocument document,
        CompleteFixture fixture)
    {
        document.Model.Should().NotBeNull();
        document.Prompt.Should().NotBeNull();
        document.Knowledge.Should().ContainSingle();
        document.Tools.Should().ContainSingle();
        document.Memory.Should().NotBeNull();
        document.Policies.Should().ContainSingle();

        var persistedPropertyNames = typeof(AgentAssetDocument)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        persistedPropertyNames.Should().NotContain([
            "ChatClient",
            "ToolExecutor",
            "ToolRegistry",
            "KnowledgeSources",
            "ConversationMemory",
            "RuntimePolicies",
            "ProviderResolver",
            "BindingResolver"
        ]);

        fixture.Tool.ExecutionCount.Should().Be(0);
        fixture.Knowledge.RetrievalCount.Should().Be(0);
    }

    private static AgentAssetDocument CopyDocument(
        AgentAssetDocument source,
        string? goal = null)
        => new(
            source.SchemaVersion,
            source.Identity,
            source.Metadata,
            source.Lifecycle,
            goal ?? source.Goal,
            source.Role,
            source.Responsibilities,
            source.Model,
            source.Prompt,
            source.Knowledge,
            source.Tools,
            source.Memory,
            source.Policies,
            source.References,
            source.Dependencies);

    private static AssetReference Reference(IAsset asset)
        => new(asset.Type, asset.Id, asset.Urn, asset.Version);

    private sealed record CompleteFixture(
        AgentDefinition Agent,
        ModelAsset Model,
        PromptAsset Prompt,
        KnowledgeAsset KnowledgeAsset,
        ToolAsset ToolAsset,
        MemoryAsset MemoryAsset,
        PolicyAsset PolicyAsset,
        RecordingTool Tool,
        RecordingKnowledgeSource Knowledge,
        RecordingMemory Memory,
        StubMemoryFactory MemoryFactory,
        StubRuntimePolicy Policy);

    private sealed class StubModelCatalog : IModelCatalog
    {
        public IReadOnlyCollection<ProviderModelDescriptor> GetModels()
            => [new ProviderModelDescriptor("Stub", "stub-model")];

        public bool Contains(string provider, string model)
            => string.Equals(provider, "Stub", StringComparison.Ordinal)
               && string.Equals(model, "stub-model", StringComparison.Ordinal);
    }

    private sealed class StubProviderResolver : IProviderResolver
    {
        private readonly IChatClientFactory factory = new StubChatClientFactory();

        public IChatClientFactory Resolve(string provider)
        {
            if (!string.Equals(provider, "Stub", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unexpected provider '{provider}'.");
            }

            return factory;
        }
    }

    private sealed class StubChatClientFactory : IChatClientFactory
    {
        private readonly IChatClient client = new NonExecutingChatClient();

        public IChatClient Create(string model)
        {
            if (!string.Equals(model, "stub-model", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unexpected model '{model}'.");
            }

            return client;
        }
    }

    private sealed class NonExecutingChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Runtime execution is outside MS-009.3D.");

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed class RecordingTool(string name) : ITool
    {
        public int ExecutionCount { get; private set; }
        public string Name { get; } = name;
        public string Description => "Conformance tool";
        public string Category => "Conformance";
        public IReadOnlyCollection<string> Tags => [];
        public ToolDescriptor Descriptor => new() { Name = Name, Description = Description };

        public Task<IToolExecutionResult> ExecuteAsync(
            ToolExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            return Task.FromResult<IToolExecutionResult>(
                ToolExecutionResult.Success("unexpected execution"));
        }
    }

    private sealed class RecordingKnowledgeSource(string name) : IKnowledgeSource
    {
        public int RetrievalCount { get; private set; }
        public string Name { get; } = name;

        public Task<KnowledgeResult> RetrieveAsync(
            KnowledgeQuery query,
            CancellationToken cancellationToken = default)
        {
            RetrievalCount++;
            return Task.FromResult(new KnowledgeResult());
        }
    }

    private sealed class RecordingMemory : IConversationMemory
    {
        private readonly List<ChatMessage> messages = [];
        public IReadOnlyList<ChatMessage> Messages => messages;
        public void Add(ChatMessage message) => messages.Add(message);
        public void Clear() => messages.Clear();
    }

    private sealed class StubMemoryFactory(
        string name,
        IConversationMemory memory) : IConversationMemoryFactory
    {
        public string Name { get; } = name;
        public IConversationMemory Create() => memory;
    }

    private sealed class StubRuntimePolicy(string name) : IRuntimePolicy
    {
        public string Name { get; } = name;
    }
}
