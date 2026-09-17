using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Persistence.AIAssets.Storage;
using PulseStack.Abstractions.Runtime.Application;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Agents.DependencyInjection;
using PulseStack.Core.DependencyInjection;
using Xunit;

namespace PulseStack.Tests.Runtime;

public sealed class WorkflowExecutorCompositionDependencyInjectionTests
{
    private static readonly AIAssetStorageOptions StorageOptions = new()
    {
        MaximumRepresentationSizeBytes = 1024 * 1024
    };

    [Fact]
    public void DefaultWorkflowComposition_ShouldResolveWorkflowRuntime()
    {
        var services = new ServiceCollection();
        services.AddPulseStackAgents();
        services.AddPulseStackWorkflows();

        using var provider = BuildProvider(services);

        Assert.NotNull(provider.GetRequiredService<IWorkflowRuntime>());
    }

    [Fact]
    public void DefaultApplicationComposition_ShouldResolveApplicationOperation()
    {
        var services = new ServiceCollection();
        services.AddInMemoryAIAssetStorage(StorageOptions);
        services.AddInMemoryAIAssetCatalog();
        services.AddAIAssetGraphLoading();
        services.AddPulseStack();
        services.AddPulseStackAgents();
        services.AddPulseStackWorkflows();

        using var provider = BuildProvider(services);
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IApplicationOperation>());
    }

    [Fact]
    public void Resolver_ShouldSeeProviderOwnedCustomExecutorRegisteredAfterWorkflowComposition()
    {
        var services = new ServiceCollection();
        services.AddPulseStackAgents();
        services.AddPulseStackWorkflows();

        var custom = new TestStepExecutor();
        services.AddSingleton<IStepExecutor>(custom);

        using var provider = BuildProvider(services);
        var resolver = provider.GetRequiredService<IStepExecutorResolver>();

        Assert.Same(custom, resolver.Resolve(new TestStep()));
    }

    [Fact]
    public void Resolver_ShouldPreserveRegistrationOrderForCompetingExecutors()
    {
        var services = new ServiceCollection();
        var first = new TestStepExecutor();
        var second = new TestStepExecutor();

        services.AddSingleton<IStepExecutor>(first);
        services.AddSingleton<IStepExecutor>(second);
        services.AddPulseStackAgents();
        services.AddPulseStackWorkflows();

        using var provider = BuildProvider(services);
        var resolver = provider.GetRequiredService<IStepExecutorResolver>();

        Assert.Same(first, resolver.Resolve(new TestStep()));
    }

    private static ServiceProvider BuildProvider(IServiceCollection services) =>
        services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true
            });

    private sealed class TestStep : IWorkflowStep
    {
        public WorkflowStepId Id { get; } = WorkflowStepId.New();

        public string Name => "R2 test step";

        public IReadOnlyList<IWorkflowStep> Children { get; } = [];
    }

    private sealed class TestStepExecutor : IStepExecutor
    {
        public bool CanExecute(IWorkflowStep step) => step is TestStep;

        public Task<StepExecutionResult> ExecuteAsync(
            IWorkflowStep step,
            PipelineContext context,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Execution is outside the R2 composition proof.");
    }
}
