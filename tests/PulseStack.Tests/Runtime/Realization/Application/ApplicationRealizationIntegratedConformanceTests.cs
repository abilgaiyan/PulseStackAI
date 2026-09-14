using System.Reflection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Chat;
using PulseStack.Abstractions.Models;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;
using PulseStack.Abstractions.Providers;
using PulseStack.Abstractions.Runtime.Realization.Application;
using PulseStack.Abstractions.Workflows.Definitions;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Agents.DependencyInjection;
using PulseStack.Core.Assets;
using PulseStack.Core.DependencyInjection;
using Xunit;

namespace PulseStack.Tests.Runtime.Realization.Application;

public sealed class ApplicationRealizationIntegratedConformanceTests
{
    [Fact]
    public async Task ProjectGraph_ShouldRealizeEntryWorkflowThroughDiResolvedApplicationRealizer()
    {
        var model = CreateModelAsset();
        var agent = new AgentDefinitionFactory().Create(
            new AgentDefinitionOptions
            {
                Name = "Integrated Agent",
                Goal = "Prove integrated application realization",
                Role = "Worker",
                Model = Reference(model)
            });
        var workflow = new WorkflowAssetFactory().Create(
            new WorkflowAssetOptions
            {
                Name = "Integrated Workflow",
                Description = "MS-010.3F.1 positive integrated realization proof",
                Steps =
                [
                    new RunStepDefinition
                    {
                        Agent = Reference(agent)
                    }
                ]
            });
        var project = CreateProject(
            Reference(workflow),
            Reference(workflow),
            Reference(agent),
            Reference(model));
        var graph = Graph(project, workflow, agent, model);

        var services = new ServiceCollection();
        services.AddSingleton<IProviderResolver, StubProviderResolver>();
        services.AddPulseStack();
        services.AddPulseStackAgents();

        await using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true
            });
        await using var scope = provider.CreateAsyncScope();

        var realizer = scope.ServiceProvider.GetRequiredService<IApplicationRealizer>();
        var result = await realizer.RealizeAsync(graph);

        var success = Assert.IsType<ApplicationRealizationResult.Success>(result);
        Assert.Equal("Integrated Workflow", success.Workflow.Name);

        var run = Assert.IsType<RunStep>(Assert.Single(success.Workflow.Steps));
        Assert.Equal("Integrated Agent", run.Agent.Name);
    }

    private static AIAssetGraph Graph(IAsset root, params IAsset[] additional)
    {
        var assets = new[] { root }.Concat(additional);

        return new AIAssetGraph(
            AssetDefinitionKey.From(root),
            assets.Select(static asset =>
                new AIAssetGraphNode(AssetDefinitionKey.From(asset), asset)),
            Array.Empty<AIAssetGraphRelationship>());
    }

    private static ProjectAsset CreateProject(
        AssetReference entryWorkflow,
        params AssetReference[] ownedAssets)
    {
        var options = new ProjectAssetOptions
        {
            Name = "Integrated Project",
            EntryWorkflow = entryWorkflow,
            OwnedAssets = ownedAssets
        };

        var constructor = typeof(ProjectAsset)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(static constructor =>
            {
                var parameters = constructor.GetParameters();
                return parameters.Length == 4
                    && parameters[0].ParameterType == typeof(AssetId)
                    && parameters[1].ParameterType == typeof(AssetUrn)
                    && parameters[2].ParameterType == typeof(ProjectAssetOptions);
            });

        return (ProjectAsset)constructor.Invoke(
            [
                AssetId.New(),
                new AssetUrn("urn:pulsestack:project:integrated-project"),
                options,
                null
            ]);
    }

    private static ModelAsset CreateModelAsset()
    {
        var catalog = new StubModelCatalog();
        var factory = new ModelAssetFactory(catalog);

        return factory.Create(
            new ModelAssetOptions(
                "Stub",
                "stub-model"));
    }

    private static AssetReference Reference(IAsset asset) =>
        new(asset.Type, asset.Id, asset.Urn, asset.Version);

    private sealed class StubModelCatalog : IModelCatalog
    {
        public IReadOnlyCollection<ProviderModelDescriptor> GetModels() =>
            [new ProviderModelDescriptor("Stub", "stub-model")];

        public bool Contains(string provider, string model) =>
            provider == "Stub" && model == "stub-model";
    }

    private sealed class StubProviderResolver : IProviderResolver
    {
        private readonly IChatClientFactory _factory = new StubChatClientFactory();

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
        private readonly IChatClient _client = new StubChatClient();

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
            throw new InvalidOperationException(
                "MS-010.3F.1 realization must not execute the realized Agent.");

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
}
