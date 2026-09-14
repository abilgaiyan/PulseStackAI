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
using PulseStack.Abstractions.Runtime.Realization.Binding;
using PulseStack.Abstractions.Runtime.Realization.Resolution;
using PulseStack.Abstractions.Tools;
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
        var agent = CreateAgent(
            "Integrated Agent",
            model,
            goal: "Prove integrated application realization");
        var workflow = CreateWorkflow("Integrated Workflow", agent);
        var project = CreateProject(
            "Integrated Project",
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

    [Fact]
    public async Task CompleteGraph_ShouldUseOperationResolverExclusively()
    {
        var model = CreateModelAsset();
        var prompt = new PromptAssetFactory().Create(
            new PromptAssetOptions
            {
                Name = "Integrated Prompt",
                SystemInstructions = "Use the graph-scoped prompt."
            });
        var agent = CreateAgent(
            "Resolver Continuity Agent",
            model,
            Reference(prompt),
            "Prove graph-scoped nested resolution");
        var workflow = CreateWorkflow("Integrated Workflow", agent);
        var project = CreateProject(
            "Integrated Project",
            Reference(workflow),
            Reference(workflow),
            Reference(agent),
            Reference(model),
            Reference(prompt));
        var graph = Graph(project, workflow, agent, model, prompt);
        var ambient = RecordingAmbientAssetResolver.Hostile();
        var providerResolver = new StubProviderResolver();

        var services = new ServiceCollection();
        services.AddScoped<IAssetResolver>(_ => ambient);
        services.AddSingleton<IProviderResolver>(providerResolver);
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
        Assert.Equal(0, ambient.Calls);
        Assert.Equal(1, providerResolver.Calls);
    }

    [Fact]
    public async Task MissingGraphModel_ShouldNotFallbackToAmbientResolver()
    {
        var model = CreateModelAsset();
        var agent = CreateAgent(
            "No Fallback Agent",
            model,
            goal: "Prove missing graph definitions cannot use ambient resolution");
        var workflow = CreateWorkflow("Integrated Workflow", agent);
        var project = CreateProject(
            "Integrated Project",
            Reference(workflow),
            Reference(workflow),
            Reference(agent),
            Reference(model));

        // Defensive boundary probe only: the authored Model reference is present,
        // but the corresponding Model node is deliberately omitted from the graph.
        var incompleteGraph = Graph(project, workflow, agent);
        var ambient = new RecordingAmbientAssetResolver(model);
        var providerResolver = new StubProviderResolver();

        Assert.Same(model, await ambient.ResolveAsync(Reference(model)));
        Assert.Equal(1, ambient.Calls);
        ambient.ResetCalls();

        var services = new ServiceCollection();
        services.AddScoped<IAssetResolver>(_ => ambient);
        services.AddSingleton<IProviderResolver>(providerResolver);
        services.AddPulseStack();
        services.AddPulseStackAgents();

        await using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true
            });
        await using var scope = provider.CreateAsyncScope();

        var realizer = scope.ServiceProvider.GetRequiredService<IApplicationRealizer>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => realizer.RealizeAsync(incompleteGraph));

        Assert.Equal(
            $"Model Asset '{model.Urn.Value}' could not be resolved.",
            exception.Message);
        Assert.Equal(0, ambient.Calls);
        Assert.Equal(0, providerResolver.Calls);
    }

    [Fact]
    public async Task SeparateOperationsOnSameScopedRealizer_ShouldRemainGraphIsolated()
    {
        var modelA = CreateModelAsset();
        var agentA = CreateAgent("Graph A Agent", modelA);
        var workflowA = CreateWorkflow("Graph A Workflow", agentA);
        var projectA = CreateProject(
            "Graph A Project",
            Reference(workflowA),
            Reference(workflowA),
            Reference(agentA),
            Reference(modelA));
        var graphA = Graph(projectA, workflowA, agentA, modelA);

        var modelB = CreateModelAsset();
        var agentB = CreateAgent("Graph B Agent", modelB);
        var workflowB = CreateWorkflow("Graph B Workflow", agentB);
        var projectB = CreateProject(
            "Graph B Project",
            Reference(workflowB),
            Reference(workflowB),
            Reference(agentB),
            Reference(modelB));
        var graphB = Graph(projectB, workflowB, agentB, modelB);

        var ambient = RecordingAmbientAssetResolver.Hostile();
        var services = new ServiceCollection();
        services.AddScoped<IAssetResolver>(_ => ambient);
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

        var resultA = Assert.IsType<ApplicationRealizationResult.Success>(
            await realizer.RealizeAsync(graphA));
        var resultB = Assert.IsType<ApplicationRealizationResult.Success>(
            await realizer.RealizeAsync(graphB));

        var runA = Assert.IsType<RunStep>(Assert.Single(resultA.Workflow.Steps));
        var runB = Assert.IsType<RunStep>(Assert.Single(resultB.Workflow.Steps));

        Assert.Equal("Graph A Workflow", resultA.Workflow.Name);
        Assert.Equal("Graph A Agent", runA.Agent.Name);
        Assert.NotEqual("Graph B Workflow", resultA.Workflow.Name);
        Assert.NotEqual("Graph B Agent", runA.Agent.Name);

        Assert.Equal("Graph B Workflow", resultB.Workflow.Name);
        Assert.Equal("Graph B Agent", runB.Agent.Name);
        Assert.NotEqual("Graph A Workflow", resultB.Workflow.Name);
        Assert.NotEqual("Graph A Agent", runB.Agent.Name);

        Assert.Equal(0, ambient.Calls);
    }

    [Fact]
    public async Task RepeatedAgentReference_ShouldCreateDistinctRuntimeAgents()
    {
        var model = CreateModelAsset();
        var agent = CreateAgent("Repeated Agent", model);
        var workflow = CreateWorkflow("Repeated Agent Workflow", agent, agent);
        var project = CreateProject(
            "Repeated Agent Project",
            Reference(workflow),
            Reference(workflow),
            Reference(agent),
            Reference(model));
        var graph = Graph(project, workflow, agent, model);
        var ambient = RecordingAmbientAssetResolver.Hostile();

        var services = new ServiceCollection();
        services.AddScoped<IAssetResolver>(_ => ambient);
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
        var success = Assert.IsType<ApplicationRealizationResult.Success>(
            await realizer.RealizeAsync(graph));

        var runs = success.Workflow.Steps
            .Select(Assert.IsType<RunStep>)
            .ToArray();

        Assert.Equal(2, runs.Length);
        Assert.Equal("Repeated Agent", runs[0].Agent.Name);
        Assert.Equal("Repeated Agent", runs[1].Agent.Name);
        Assert.NotSame(runs[0].Agent, runs[1].Agent);
        Assert.Equal(0, ambient.Calls);
    }

    [Fact]
    public async Task RepeatedRealizationOfSameGraph_ShouldCreateFreshWorkflowAndAgents()
    {
        var model = CreateModelAsset();
        var agent = CreateAgent("Fresh Agent", model);
        var workflow = CreateWorkflow("Fresh Workflow", agent);
        var project = CreateProject(
            "Fresh Project",
            Reference(workflow),
            Reference(workflow),
            Reference(agent),
            Reference(model));
        var graph = Graph(project, workflow, agent, model);
        var ambient = RecordingAmbientAssetResolver.Hostile();

        var services = new ServiceCollection();
        services.AddScoped<IAssetResolver>(_ => ambient);
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

        var first = Assert.IsType<ApplicationRealizationResult.Success>(
            await realizer.RealizeAsync(graph));
        var second = Assert.IsType<ApplicationRealizationResult.Success>(
            await realizer.RealizeAsync(graph));

        var firstRun = Assert.IsType<RunStep>(Assert.Single(first.Workflow.Steps));
        var secondRun = Assert.IsType<RunStep>(Assert.Single(second.Workflow.Steps));

        Assert.NotSame(first.Workflow, second.Workflow);
        Assert.NotSame(firstRun.Agent, secondRun.Agent);
        Assert.Equal("Fresh Workflow", first.Workflow.Name);
        Assert.Equal("Fresh Workflow", second.Workflow.Name);
        Assert.Equal("Fresh Agent", firstRun.Agent.Name);
        Assert.Equal("Fresh Agent", secondRun.Agent.Name);
        Assert.Equal(0, ambient.Calls);
    }

    [Fact]
    public async Task NestedProviderFailure_ShouldPropagateExactExceptionInstance()
    {
        var model = CreateModelAsset();
        var agent = CreateAgent("Provider Failure Agent", model);
        var workflow = CreateWorkflow("Provider Failure Workflow", agent);
        var project = CreateProject(
            "Provider Failure Project",
            Reference(workflow),
            Reference(workflow),
            Reference(agent),
            Reference(model));
        var graph = Graph(project, workflow, agent, model);
        var ambient = RecordingAmbientAssetResolver.Hostile();
        var expected = new InvalidOperationException(
            "MS-010.3F.4 provider failure sentinel.");
        var providerResolver = new ThrowingProviderResolver(expected);

        var services = new ServiceCollection();
        services.AddScoped<IAssetResolver>(_ => ambient);
        services.AddSingleton<IProviderResolver>(providerResolver);
        services.AddPulseStack();
        services.AddPulseStackAgents();

        await using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true
            });
        await using var scope = provider.CreateAsyncScope();

        var realizer = scope.ServiceProvider.GetRequiredService<IApplicationRealizer>();

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => realizer.RealizeAsync(graph));

        Assert.Same(expected, actual);
        Assert.Equal(1, providerResolver.Calls);
        Assert.Equal(0, ambient.Calls);
    }

    [Fact]
    public async Task NestedToolBindingFailure_ShouldPropagateExactExceptionInstance()
    {
        var model = CreateModelAsset();
        var tool = CreateToolAsset("Binding Failure Tool");
        var agent = CreateAgent(
            "Binding Failure Agent",
            model,
            tools: [Reference(tool)]);
        var workflow = CreateWorkflow("Binding Failure Workflow", agent);
        var project = CreateProject(
            "Binding Failure Project",
            Reference(workflow),
            Reference(workflow),
            Reference(agent),
            Reference(model),
            Reference(tool));
        var graph = Graph(project, workflow, agent, model, tool);
        var ambient = RecordingAmbientAssetResolver.Hostile();
        var expected = new InvalidOperationException(
            "MS-010.3F.4 tool binding failure sentinel.");
        var bindingResolver = new ThrowingToolBindingResolver(expected);
        var providerResolver = new StubProviderResolver();

        var services = new ServiceCollection();
        services.AddScoped<IAssetResolver>(_ => ambient);
        services.AddSingleton<IProviderResolver>(providerResolver);
        services.AddSingleton<IToolBindingResolver>(bindingResolver);
        services.AddPulseStack();
        services.AddPulseStackAgents();

        await using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true
            });
        await using var scope = provider.CreateAsyncScope();

        var realizer = scope.ServiceProvider.GetRequiredService<IApplicationRealizer>();

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => realizer.RealizeAsync(graph));

        Assert.Same(expected, actual);
        Assert.Same(tool, bindingResolver.LastAsset);
        Assert.Equal(1, bindingResolver.Calls);
        Assert.Equal(1, providerResolver.Calls);
        Assert.Equal(0, ambient.Calls);
    }

    [Fact]
    public async Task AlreadyCancelledOperation_ShouldStayOnCancellationChannel()
    {
        var model = CreateModelAsset();
        var tool = CreateToolAsset("Cancelled Boundary Tool");
        var agent = CreateAgent(
            "Cancelled Boundary Agent",
            model,
            tools: [Reference(tool)]);
        var workflow = CreateWorkflow("Cancelled Boundary Workflow", agent);
        var project = CreateProject(
            "Cancelled Boundary Project",
            Reference(workflow),
            Reference(workflow),
            Reference(agent),
            Reference(model),
            Reference(tool));
        var graph = Graph(project, workflow, agent, model, tool);
        var ambient = RecordingAmbientAssetResolver.Hostile();
        var providerResolver = new StubProviderResolver();
        var bindingResolver = new ThrowingToolBindingResolver(
            new InvalidOperationException(
                "Binding must not be reached for an already-cancelled operation."));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var services = new ServiceCollection();
        services.AddScoped<IAssetResolver>(_ => ambient);
        services.AddSingleton<IProviderResolver>(providerResolver);
        services.AddSingleton<IToolBindingResolver>(bindingResolver);
        services.AddPulseStack();
        services.AddPulseStackAgents();

        await using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true
            });
        await using var scope = provider.CreateAsyncScope();

        var realizer = scope.ServiceProvider.GetRequiredService<IApplicationRealizer>();

        var actual = await Assert.ThrowsAsync<OperationCanceledException>(
            () => realizer.RealizeAsync(graph, cancellation.Token));

        Assert.Equal(cancellation.Token, actual.CancellationToken);
        Assert.Equal(0, providerResolver.Calls);
        Assert.Equal(0, bindingResolver.Calls);
        Assert.Equal(0, ambient.Calls);
    }

    [Fact]
    public async Task NestedCancellation_ShouldPropagateExactExceptionAndToken()
    {
        var model = CreateModelAsset();
        var agent = CreateAgent("Nested Cancellation Agent", model);
        var workflow = CreateWorkflow("Nested Cancellation Workflow", agent);
        var project = CreateProject(
            "Nested Cancellation Project",
            Reference(workflow),
            Reference(workflow),
            Reference(agent),
            Reference(model));
        var graph = Graph(project, workflow, agent, model);
        var ambient = RecordingAmbientAssetResolver.Hostile();
        using var cancellation = new CancellationTokenSource();
        var expected = new OperationCanceledException(
            "MS-010.3F.4 nested cancellation sentinel.",
            cancellation.Token);
        var providerResolver = new ThrowingProviderResolver(expected);

        var services = new ServiceCollection();
        services.AddScoped<IAssetResolver>(_ => ambient);
        services.AddSingleton<IProviderResolver>(providerResolver);
        services.AddPulseStack();
        services.AddPulseStackAgents();

        await using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true
            });
        await using var scope = provider.CreateAsyncScope();

        var realizer = scope.ServiceProvider.GetRequiredService<IApplicationRealizer>();

        var actual = await Assert.ThrowsAsync<OperationCanceledException>(
            () => realizer.RealizeAsync(graph, cancellation.Token));

        Assert.Same(expected, actual);
        Assert.Equal(cancellation.Token, actual.CancellationToken);
        Assert.Equal(1, providerResolver.Calls);
        Assert.Equal(0, ambient.Calls);
    }

    [Fact]
    public async Task Realization_ShouldNotExecuteRuntimeCollaborators()
    {
        var model = CreateModelAsset();
        var toolAsset = CreateToolAsset("Execution Boundary Tool");
        var agent = CreateAgent(
            "Execution Boundary Agent",
            model,
            tools: [Reference(toolAsset)]);
        var workflow = CreateWorkflow("Execution Boundary Workflow", agent);
        var project = CreateProject(
            "Execution Boundary Project",
            Reference(workflow),
            Reference(workflow),
            Reference(agent),
            Reference(model),
            Reference(toolAsset));
        var graph = Graph(project, workflow, agent, model, toolAsset);

        var ambient = RecordingAmbientAssetResolver.Hostile();
        var agentRuntime = new RecordingAgentRuntime();
        var chatClient = new RecordingChatClient();
        var providerResolver = new RecordingProviderResolver(chatClient);
        var boundTool = new RecordingTool("execution-boundary-tool");
        var bindingResolver = new RecordingToolBindingResolver(boundTool);
        var toolExecutor = new RecordingToolExecutor();

        var services = new ServiceCollection();
        services.AddScoped<IAssetResolver>(_ => ambient);
        services.AddSingleton<IAgentRuntime>(agentRuntime);
        services.AddSingleton<IProviderResolver>(providerResolver);
        services.AddSingleton<IToolBindingResolver>(bindingResolver);
        services.AddScoped<IToolExecutor>(_ => toolExecutor);
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
        var run = Assert.IsType<RunStep>(Assert.Single(success.Workflow.Steps));

        Assert.Equal("Execution Boundary Workflow", success.Workflow.Name);
        Assert.Equal("Execution Boundary Agent", run.Agent.Name);
        Assert.Equal(1, providerResolver.Calls);
        Assert.Equal(1, bindingResolver.Calls);
        Assert.Same(toolAsset, bindingResolver.LastAsset);

        Assert.Equal(0, agentRuntime.RunCalls);
        Assert.Equal(0, agentRuntime.StreamCalls);
        Assert.Equal(0, chatClient.Calls);
        Assert.Equal(0, toolExecutor.Calls);
        Assert.Equal(0, boundTool.ExecutionCalls);
        Assert.Equal(0, ambient.Calls);
    }

    private static AgentDefinition CreateAgent(
        string name,
        ModelAsset model,
        AssetReference? prompt = null,
        string goal = "Prove integrated application realization",
        IReadOnlyCollection<AssetReference>? tools = null) =>
        new AgentDefinitionFactory().Create(
            new AgentDefinitionOptions
            {
                Name = name,
                Goal = goal,
                Role = "Worker",
                Model = Reference(model),
                Prompt = prompt,
                Tools = tools ?? []
            });

    private static WorkflowAsset CreateWorkflow(
        string name,
        params AgentDefinition[] agents) =>
        new WorkflowAssetFactory().Create(
            new WorkflowAssetOptions
            {
                Name = name,
                Description = "MS-010.3F integrated realization proof",
                Steps = agents
                    .Select(static agent => (WorkflowStepDefinition)new RunStepDefinition
                    {
                        Agent = Reference(agent)
                    })
                    .ToArray()
            });

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
        string name,
        AssetReference entryWorkflow,
        params AssetReference[] ownedAssets)
    {
        var options = new ProjectAssetOptions
        {
            Name = name,
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

        var id = AssetId.New();
        return (ProjectAsset)constructor.Invoke(
            [
                id,
                new AssetUrn($"urn:pulsestack:project:{id}"),
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

    private static ToolAsset CreateToolAsset(string name) =>
        new ToolAssetFactory().Create(
            new ToolAssetOptions
            {
                Name = name,
                Description = "MS-010.3F binding conformance tool",
                Category = "Conformance"
            });

    private static AssetReference Reference(IAsset asset) =>
        new(asset.Type, asset.Id, asset.Urn, asset.Version);

    private sealed class RecordingAmbientAssetResolver : IAssetResolver
    {
        private readonly IReadOnlyDictionary<AssetDefinitionKey, IAsset> _assets;
        private readonly bool _throwOnResolve;

        public RecordingAmbientAssetResolver(params IAsset[] assets)
            : this(false, assets)
        {
        }

        private RecordingAmbientAssetResolver(
            bool throwOnResolve,
            params IAsset[] assets)
        {
            _throwOnResolve = throwOnResolve;
            _assets = assets.ToDictionary(AssetDefinitionKey.From);
        }

        public int Calls { get; private set; }

        public static RecordingAmbientAssetResolver Hostile() => new(true);

        public void ResetCalls() => Calls = 0;

        public ValueTask<IAsset?> ResolveAsync(
            AssetReference reference,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;

            if (_throwOnResolve)
            {
                throw new InvalidOperationException(
                    "Ambient DI IAssetResolver must not be consulted during application realization.");
            }

            _assets.TryGetValue(AssetDefinitionKey.From(reference), out var asset);
            return ValueTask.FromResult(asset);
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
        private readonly IChatClientFactory _factory = new StubChatClientFactory();

        public int Calls { get; private set; }

        public IChatClientFactory Resolve(string provider)
        {
            Calls++;

            if (provider != "Stub")
            {
                throw new InvalidOperationException(
                    $"Unexpected provider '{provider}'.");
            }

            return _factory;
        }
    }

    private sealed class ThrowingProviderResolver : IProviderResolver
    {
        private readonly Exception _exception;

        public ThrowingProviderResolver(Exception exception)
        {
            _exception = exception;
        }

        public int Calls { get; private set; }

        public IChatClientFactory Resolve(string provider)
        {
            Calls++;
            throw _exception;
        }
    }

    private sealed class RecordingProviderResolver : IProviderResolver
    {
        private readonly IChatClientFactory _factory;

        public RecordingProviderResolver(IChatClient client)
        {
            _factory = new RecordingChatClientFactory(client);
        }

        public int Calls { get; private set; }

        public IChatClientFactory Resolve(string provider)
        {
            Calls++;

            if (provider != "Stub")
            {
                throw new InvalidOperationException(
                    $"Unexpected provider '{provider}'.");
            }

            return _factory;
        }
    }

    private sealed class ThrowingToolBindingResolver : IToolBindingResolver
    {
        private readonly Exception _exception;

        public ThrowingToolBindingResolver(Exception exception)
        {
            _exception = exception;
        }

        public int Calls { get; private set; }

        public ToolAsset? LastAsset { get; private set; }

        public ITool Resolve(ToolAsset asset)
        {
            Calls++;
            LastAsset = asset;
            throw _exception;
        }
    }

    private sealed class RecordingToolBindingResolver : IToolBindingResolver
    {
        private readonly ITool _tool;

        public RecordingToolBindingResolver(ITool tool)
        {
            _tool = tool;
        }

        public int Calls { get; private set; }

        public ToolAsset? LastAsset { get; private set; }

        public ITool Resolve(ToolAsset asset)
        {
            Calls++;
            LastAsset = asset;
            return _tool;
        }
    }

    private sealed class RecordingAgentRuntime : IAgentRuntime
    {
        public int RunCalls { get; private set; }

        public int StreamCalls { get; private set; }

        public Task<AgentResponse> RunAsync(
            PipelineContext context,
            CancellationToken cancellationToken = default)
        {
            RunCalls++;
            return Task.FromException<AgentResponse>(
                new InvalidOperationException(
                    "MS-010.3F.5 realization must not invoke IAgentRuntime.RunAsync."));
        }

        public IAsyncEnumerable<string> StreamAsync(
            string input,
            CancellationToken cancellationToken = default)
        {
            StreamCalls++;
            throw new InvalidOperationException(
                "MS-010.3F.5 realization must not invoke IAgentRuntime.StreamAsync.");
        }
    }

    private sealed class RecordingToolExecutor : IToolExecutor
    {
        public int Calls { get; private set; }

        public Task<IToolExecutionResult> ExecuteAsync(
            ITool tool,
            ToolExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromException<IToolExecutionResult>(
                new InvalidOperationException(
                    "MS-010.3F.5 realization must not execute tools."));
        }
    }

    private sealed class RecordingTool : ITool
    {
        public RecordingTool(string name)
        {
            Name = name;
            Descriptor = new ToolDescriptor
            {
                Name = name,
                Description = "MS-010.3F.5 execution-boundary tool"
            };
        }

        public string Name { get; }

        public string Description => "MS-010.3F.5 execution-boundary tool";

        public string Category => "Conformance";

        public IReadOnlyCollection<string> Tags => [];

        public ToolDescriptor Descriptor { get; }

        public int ExecutionCalls { get; private set; }

        public Task<IToolExecutionResult> ExecuteAsync(
            ToolExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            ExecutionCalls++;
            return Task.FromException<IToolExecutionResult>(
                new InvalidOperationException(
                    "MS-010.3F.5 realization must not invoke bound tools."));
        }
    }

    private sealed class RecordingChatClientFactory : IChatClientFactory
    {
        private readonly IChatClient _client;

        public RecordingChatClientFactory(IChatClient client)
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

    private sealed class RecordingChatClient : IChatClient
    {
        public int Calls { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromException<ChatResponse>(
                new InvalidOperationException(
                    "MS-010.3F.5 realization must not call the chat client."));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new InvalidOperationException(
                "MS-010.3F.5 realization must not stream from the chat client.");
        }

        public object? GetService(
            Type serviceType,
            object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed class StubChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "MS-010.3F realization must not execute the realized Agent.");

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
