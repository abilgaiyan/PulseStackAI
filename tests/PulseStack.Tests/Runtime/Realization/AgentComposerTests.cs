using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Chat;
using PulseStack.Abstractions.Knowledge;
using PulseStack.Abstractions.Models;
using PulseStack.Abstractions.Providers;
using PulseStack.Abstractions.Runtime.Realization.Binding;
using PulseStack.Abstractions.Runtime.Realization.Composition;
using PulseStack.Abstractions.Runtime.Realization.Resolution;
using PulseStack.Abstractions.Tools;
using PulseStack.Agents.DependencyInjection;
using PulseStack.Core.Assets;
using PulseStack.Core.DependencyInjection;
using Xunit;

namespace PulseStack.Tests.Runtime.Realization;

public sealed class AgentComposerTests
{
    [Fact]
    public async Task ComposeAsync_Should_Resolve_Model_And_Create_Runtime_Agent()
    {
        var modelAsset = CreateModelAsset();

        var services = new ServiceCollection();

        services.AddSingleton<IProviderResolver, StubProviderResolver>();
        services.AddScoped<IAssetResolver>(_ =>
            new StubAssetResolver(modelAsset));

        services.AddPulseStack();
        services.AddPulseStackAgents();

        await using var provider = services.BuildServiceProvider();

        var composer = provider.GetRequiredService<IAgentComposer>();

        var definition = new AgentDefinitionFactory().Create(
            new AgentDefinitionOptions
            {
                Name = "TestAgent",
                Goal = "Test model realization",
                Role = "Test worker",
                Model = new AssetReference(
                    modelAsset.Id,
                    modelAsset.Urn)
            });

        var agent = await composer.ComposeAsync(definition);

        agent.Should().NotBeNull();
        agent.Name.Should().Be("TestAgent");
    }

    [Fact]
    public async Task ComposeAsync_Should_Reject_Agent_Without_Model()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IProviderResolver, StubProviderResolver>();
        services.AddPulseStack();
        services.AddPulseStackAgents();

        await using var provider = services.BuildServiceProvider();

        var composer = provider.GetRequiredService<IAgentComposer>();

        var definition = new AgentDefinitionFactory().Create(
            new AgentDefinitionOptions
            {
                Name = "TestAgent",
                Goal = "Test validation",
                Role = "Test worker"
            });

        var action = () => composer.ComposeAsync(definition);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*requires a Model Asset reference*");
    }

    [Fact]
    public async Task ComposeAsync_Should_Execute_Only_Referenced_Tool()
    {
        var modelAsset = CreateModelAsset();
        var toolAsset = CreateToolAsset("Approved Tool");
        var referencedTool = new RecordingTool("approved-tool");
        var unreferencedTool = new RecordingTool("unreferenced-tool");
        var client = new SequenceChatClient(
            "{\"tool\":\"approved-tool\",\"input\":\"payload\"}",
            "Completed with approved tool.");

        var services = CreateToolRealizationServices(
            modelAsset,
            toolAsset,
            referencedTool,
            unreferencedTool,
            client);

        await using var provider = services.BuildServiceProvider();
        var composer = provider.GetRequiredService<IAgentComposer>();

        var definition = CreateAgentDefinition(
            modelAsset,
            toolAsset);

        var agent = await composer.ComposeAsync(definition);
        var response = await agent.RunAsync("Use the configured tool.");

        response.Text.Should().Be("Completed with approved tool.");
        referencedTool.ExecutionCount.Should().Be(1);
        referencedTool.LastInput.Should().Be("payload");
        unreferencedTool.ExecutionCount.Should().Be(0);
    }

    [Fact]
    public async Task ComposeAsync_Should_Not_Expose_Unreferenced_Global_Tool()
    {
        var modelAsset = CreateModelAsset();
        var toolAsset = CreateToolAsset("Approved Tool");
        var referencedTool = new RecordingTool("approved-tool");
        var unreferencedTool = new RecordingTool("unreferenced-tool");
        var client = new SequenceChatClient(
            "{\"tool\":\"unreferenced-tool\",\"input\":\"payload\"}",
            "Completed without executing unreferenced tool.");

        var services = CreateToolRealizationServices(
            modelAsset,
            toolAsset,
            referencedTool,
            unreferencedTool,
            client);

        await using var provider = services.BuildServiceProvider();
        var composer = provider.GetRequiredService<IAgentComposer>();

        var definition = CreateAgentDefinition(
            modelAsset,
            toolAsset);

        var agent = await composer.ComposeAsync(definition);
        var response = await agent.RunAsync("Try to use a tool.");

        response.Text.Should().Be("Completed without executing unreferenced tool.");
        referencedTool.ExecutionCount.Should().Be(0);
        unreferencedTool.ExecutionCount.Should().Be(0);
    }

    [Fact]
    public async Task ComposeAsync_Should_Bind_Only_Referenced_Knowledge()
    {
        var modelAsset = CreateModelAsset();
        var referencedAsset = CreateKnowledgeAsset("Customer Knowledge");
        var unreferencedAsset = CreateKnowledgeAsset("Internal Knowledge");
        var referencedSource = new StubKnowledgeSource("customer-source");
        var unreferencedSource = new StubKnowledgeSource("internal-source");
        var bindingResolver = new RecordingKnowledgeBindingResolver(
            new Dictionary<AssetId, IKnowledgeSource>
            {
                [referencedAsset.Id] = referencedSource,
                [unreferencedAsset.Id] = unreferencedSource
            });

        var services = new ServiceCollection();
        services.AddSingleton<IProviderResolver, StubProviderResolver>();
        services.AddSingleton<IAsset>(modelAsset);
        services.AddSingleton<IAsset>(referencedAsset);
        services.AddSingleton<IAsset>(unreferencedAsset);
        services.AddSingleton<IKnowledgeBindingResolver>(bindingResolver);
        services.AddPulseStack();
        services.AddPulseStackAgents();

        await using var provider = services.BuildServiceProvider();
        var composer = provider.GetRequiredService<IAgentComposer>();

        var definition = new AgentDefinitionFactory().Create(
            new AgentDefinitionOptions
            {
                Name = "KnowledgeAgent",
                Goal = "Use only explicitly referenced knowledge",
                Role = "Knowledge worker",
                Model = new AssetReference(modelAsset.Id, modelAsset.Urn),
                Knowledge =
                [
                    new AssetReference(
                        referencedAsset.Id,
                        referencedAsset.Urn)
                ]
            });

        var agent = await composer.ComposeAsync(definition);

        agent.Should().NotBeNull();
        bindingResolver.ResolvedAssets.Should().ContainSingle()
            .Which.Should().Be(referencedAsset.Id);
        bindingResolver.ResolvedAssets.Should().NotContain(unreferencedAsset.Id);
    }

    private static ServiceCollection CreateToolRealizationServices(
        ModelAsset modelAsset,
        ToolAsset toolAsset,
        RecordingTool referencedTool,
        RecordingTool unreferencedTool,
        IChatClient client)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IProviderResolver>(
            new StubProviderResolver(client));
        services.AddSingleton<IAsset>(modelAsset);
        services.AddSingleton<IAsset>(toolAsset);
        services.AddSingleton<ITool>(referencedTool);
        services.AddSingleton<ITool>(unreferencedTool);
        services.AddSingleton(
            new ToolBindingRegistration(
                new AssetReference(toolAsset.Id, toolAsset.Urn),
                referencedTool.Name));

        services.AddPulseStack();
        services.AddPulseStackAgents();

        return services;
    }

    private static AgentDefinition CreateAgentDefinition(
        ModelAsset modelAsset,
        ToolAsset toolAsset) =>
        new AgentDefinitionFactory().Create(
            new AgentDefinitionOptions
            {
                Name = "ToolAgent",
                Goal = "Use only explicitly referenced tools",
                Role = "Tool worker",
                Model = new AssetReference(
                    modelAsset.Id,
                    modelAsset.Urn),
                Tools =
                [
                    new AssetReference(
                        toolAsset.Id,
                        toolAsset.Urn)
                ]
            });

    private static ModelAsset CreateModelAsset()
    {
        var catalog = new StubModelCatalog();
        var factory = new ModelAssetFactory(catalog);

        return factory.Create(
            new ModelAssetOptions(
                "Stub",
                "stub-model"));
    }

    private static ToolAsset CreateToolAsset(string name) =>
        new ToolAssetFactory().Create(
            new ToolAssetOptions
            {
                Name = name,
                Description = "Tool used by Agent realization tests.",
                Category = "Tests"
            });

    private static KnowledgeAsset CreateKnowledgeAsset(string name) =>
        new KnowledgeAssetFactory().Create(
            new KnowledgeAssetOptions
            {
                Name = name,
                Description = "Knowledge used by Agent realization tests.",
                Tags = ["tests"]
            });

    private sealed class StubAssetResolver : IAssetResolver
    {
        private readonly ModelAsset _modelAsset;

        public StubAssetResolver(ModelAsset modelAsset)
        {
            _modelAsset = modelAsset;
        }

        public ValueTask<IAsset?> ResolveAsync(
            AssetReference reference,
            CancellationToken cancellationToken = default)
        {
            if (reference.Id == _modelAsset.Id)
            {
                return ValueTask.FromResult<IAsset?>(_modelAsset);
            }

            return ValueTask.FromResult<IAsset?>(null);
        }
    }

    private sealed class StubModelCatalog : IModelCatalog
    {
        public IReadOnlyCollection<ProviderModelDescriptor> GetModels() =>
            [new ProviderModelDescriptor("Stub", "stub-model")];

        public bool Contains(string provider, string model) =>
            provider == "Stub" && model == "stub-model";
    }

    private sealed class StubProviderResolver : IProviderResolver
    {
        private readonly IChatClientFactory _factory;

        public StubProviderResolver()
            : this(new StubChatClient())
        {
        }

        public StubProviderResolver(IChatClient client)
        {
            _factory = new StubChatClientFactory(client);
        }

        public IChatClientFactory Resolve(string provider)
        {
            if (provider != "Stub")
            {
                throw new InvalidOperationException(
                    $"Unexpected provider '{provider}'.");
            }

            return _factory;
        }
    }

    private sealed class StubChatClientFactory : IChatClientFactory
    {
        private readonly IChatClient _client;

        public StubChatClientFactory()
            : this(new StubChatClient())
        {
        }

        public StubChatClientFactory(IChatClient client)
        {
            _client = client;
        }

        public IChatClient Create(string model)
        {
            if (model != "stub-model")
            {
                throw new InvalidOperationException(
                    $"Unexpected model '{model}'.");
            }

            return _client;
        }
    }

    private sealed class StubChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(
            Type serviceType,
            object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed class SequenceChatClient(params string[] responses) : IChatClient
    {
        private readonly Queue<string> _responses = new(responses);

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException(
                    "No scripted chat response remains.");
            }

            return Task.FromResult(
                new ChatResponse(
                    new ChatMessage(
                        ChatRole.Assistant,
                        _responses.Dequeue())));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(
            Type serviceType,
            object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed class RecordingTool(string name) : ITool
    {
        public int ExecutionCount { get; private set; }

        public string? LastInput { get; private set; }

        public string Name { get; } = name;

        public string Description => "Recording test tool";

        public string Category => "Tests";

        public IReadOnlyCollection<string> Tags => [];

        public ToolDescriptor Descriptor => new()
        {
            Name = Name,
            Description = Description
        };

        public Task<IToolExecutionResult> ExecuteAsync(
            ToolExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ExecutionCount++;
            LastInput = context.Input?.ToString();

            return Task.FromResult<IToolExecutionResult>(
                ToolExecutionResult.Success(
                    $"{Name}:{context.Input}"));
        }
    }

    private sealed class StubKnowledgeSource(string name) : IKnowledgeSource
    {
        public string Name { get; } = name;

        public Task<KnowledgeResult> RetrieveAsync(
            KnowledgeQuery query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                new KnowledgeResult
                {
                    Items = [$"{Name}:{query.Text}"]
                });
        }
    }

    private sealed class RecordingKnowledgeBindingResolver(
        IReadOnlyDictionary<AssetId, IKnowledgeSource> sources)
        : IKnowledgeBindingResolver
    {
        public List<AssetId> ResolvedAssets { get; } = [];

        public IKnowledgeSource Resolve(KnowledgeAsset asset)
        {
            ResolvedAssets.Add(asset.Id);

            if (!sources.TryGetValue(asset.Id, out var source))
            {
                throw new InvalidOperationException(
                    $"Unexpected Knowledge Asset '{asset.Urn.Value}'.");
            }

            return source;
        }
    }
}
