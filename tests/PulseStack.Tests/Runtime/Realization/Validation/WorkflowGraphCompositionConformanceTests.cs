using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;
using PulseStack.Abstractions.Persistence.AIAssets.Mapping;
using PulseStack.Abstractions.Persistence.AIAssets.Validation;
using PulseStack.Abstractions.Runtime.Realization.Binding;
using PulseStack.Abstractions.Runtime.Realization.Composition;
using PulseStack.Abstractions.Runtime.Realization.Resolution;
using PulseStack.Abstractions.Runtime.Realization.Validation;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Conditions;
using PulseStack.Abstractions.Workflows.Definitions;
using PulseStack.Agents.DependencyInjection;
using PulseStack.Core.Assets;
using PulseStack.Core.DependencyInjection;
using PulseStack.Core.Runtime.Realization.Binding;
using PulseStack.Core.Runtime.Realization.Resolution;
using PulseStack.Core.Runtime.Realization.Validation;
using Xunit;

namespace PulseStack.Tests.Runtime.Realization.Validation;

public sealed class WorkflowGraphCompositionConformanceTests
{
    [Fact]
    public async Task ValidDocument_ShouldReconstructValidateAndComposeWithoutExecution()
    {
        var condition = new TrackingCondition("is-ready");
        var services = CreateServices(condition);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var mapper = scope.ServiceProvider.GetRequiredService<IAIAssetDocumentMapper>();
        var documentValidator = scope.ServiceProvider.GetRequiredService<IAIAssetDocumentValidator>();
        var graphValidator = scope.ServiceProvider.GetRequiredService<IWorkflowGraphValidator>();
        var composer = scope.ServiceProvider.GetRequiredService<IWorkflowComposer>();

        var authored = CreateWorkflow([
            new ConditionalStepDefinition
            {
                Id = WorkflowStepId.New(),
                Name = "if-ready",
                Condition = new NamedConditionDefinition { Name = "is-ready" },
                ThenStep = EmptyParallel("then"),
                ElseStep = EmptyParallel("else")
            }
        ]);

        var document = mapper.ToDocument(authored)
            .Should().BeOfType<WorkflowAssetDocument>().Subject;

        var structural = await documentValidator.ValidateAsync(document);
        structural.IsValid.Should().BeTrue();

        var reconstructed = mapper.FromDocument(document)
            .Should().BeOfType<WorkflowAsset>().Subject;

        var readiness = await graphValidator.ValidateAsync(reconstructed);
        readiness.IsValid.Should().BeTrue();
        condition.EvaluationCount.Should().Be(0);

        var runtime = await composer.ComposeAsync(reconstructed);

        runtime.Should().NotBeNull();
        condition.EvaluationCount.Should().Be(0);
    }

    [Fact]
    public void AddPulseStack_ShouldResolveWorkflowGraphValidatorAsScopedService()
    {
        var services = CreateServices();
        using var provider = services.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var first = firstScope.ServiceProvider.GetRequiredService<IWorkflowGraphValidator>();
        var firstAgain = firstScope.ServiceProvider.GetRequiredService<IWorkflowGraphValidator>();
        var second = secondScope.ServiceProvider.GetRequiredService<IWorkflowGraphValidator>();

        first.Should().BeOfType<WorkflowGraphValidator>();
        firstAgain.Should().BeSameAs(first);
        second.Should().NotBeSameAs(first);
    }

    [Fact]
    public void Validators_ShouldShareScopedAssetCatalogAuthority()
    {
        var services = CreateServices();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var resolver = scope.ServiceProvider.GetRequiredService<InMemoryAssetResolver>();
        var assetResolver = scope.ServiceProvider.GetRequiredService<IAssetResolver>();
        var catalog = scope.ServiceProvider.GetRequiredService<IAssetDefinitionCatalog>();
        var workflowValidator = scope.ServiceProvider.GetRequiredService<IWorkflowGraphValidator>();
        var agentValidator = scope.ServiceProvider.GetRequiredService<IAgentGraphValidator>();

        assetResolver.Should().BeSameAs(resolver);
        catalog.Should().BeSameAs(resolver);

        GetField<IAssetDefinitionCatalog>(workflowValidator, "_assetCatalog")
            .Should().BeSameAs(catalog);
        GetField<IAssetDefinitionCatalog>(agentValidator, "catalog")
            .Should().BeSameAs(catalog);
    }

    [Fact]
    public void ConditionCatalog_ShouldRemainSharedWithWorkflowReadinessComposition()
    {
        var condition = new TrackingCondition("is-ready");
        var services = CreateServices(condition);
        using var provider = services.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var concrete = firstScope.ServiceProvider.GetRequiredService<ConditionBindingCatalog>();
        var first = firstScope.ServiceProvider.GetRequiredService<IConditionBindingCatalog>();
        var second = secondScope.ServiceProvider.GetRequiredService<IConditionBindingCatalog>();

        first.Should().BeSameAs(concrete);
        second.Should().BeSameAs(concrete);
        first.Contains("IS-READY").Should().BeTrue();
        condition.EvaluationCount.Should().Be(0);
    }

    [Fact]
    public async Task MissingAgent_ShouldPreventComposerHandoff()
    {
        var services = CreateServices();
        var trackingComposer = new TrackingWorkflowComposer();
        services.AddScoped<IWorkflowComposer>(_ => trackingComposer);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var mapper = scope.ServiceProvider.GetRequiredService<IAIAssetDocumentMapper>();
        var documentValidator = scope.ServiceProvider.GetRequiredService<IAIAssetDocumentValidator>();
        var graphValidator = scope.ServiceProvider.GetRequiredService<IWorkflowGraphValidator>();
        var composer = scope.ServiceProvider.GetRequiredService<IWorkflowComposer>();

        var missingAgent = new AssetReference(
            AssetType.Agent,
            AssetId.New(),
            new AssetUrn("urn:pulsestack:agent:missing"),
            AssetVersion.Initial);
        var authored = CreateWorkflow([
            new RunStepDefinition
            {
                Id = WorkflowStepId.New(),
                Agent = missingAgent
            }
        ]);
        var document = mapper.ToDocument(authored)
            .Should().BeOfType<WorkflowAssetDocument>().Subject;

        (await documentValidator.ValidateAsync(document)).IsValid.Should().BeTrue();
        var reconstructed = mapper.FromDocument(document)
            .Should().BeOfType<WorkflowAsset>().Subject;
        var readiness = await graphValidator.ValidateAsync(reconstructed);

        readiness.IsValid.Should().BeFalse();
        readiness.Errors.Should().ContainSingle()
            .Which.Code.Should().Be(WorkflowGraphValidationCodes.AgentDefinitionUnavailable);

        if (readiness.IsValid)
        {
            await composer.ComposeAsync(reconstructed);
        }

        trackingComposer.ComposeCount.Should().Be(0);
    }

    [Fact]
    public async Task MissingCondition_ShouldPreventComposerHandoffAndRemainValidationIsolated()
    {
        var services = CreateServices();
        var trackingComposer = new TrackingWorkflowComposer();
        services.AddScoped<IWorkflowComposer>(_ => trackingComposer);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var mapper = scope.ServiceProvider.GetRequiredService<IAIAssetDocumentMapper>();
        var documentValidator = scope.ServiceProvider.GetRequiredService<IAIAssetDocumentValidator>();
        var graphValidator = scope.ServiceProvider.GetRequiredService<IWorkflowGraphValidator>();
        var composer = scope.ServiceProvider.GetRequiredService<IWorkflowComposer>();

        var authored = CreateWorkflow([
            new ConditionalStepDefinition
            {
                Id = WorkflowStepId.New(),
                Name = "if-missing",
                Condition = new NamedConditionDefinition { Name = "missing-condition" },
                ThenStep = EmptyParallel("then")
            }
        ]);
        var document = mapper.ToDocument(authored)
            .Should().BeOfType<WorkflowAssetDocument>().Subject;

        (await documentValidator.ValidateAsync(document)).IsValid.Should().BeTrue();
        var reconstructed = mapper.FromDocument(document)
            .Should().BeOfType<WorkflowAsset>().Subject;
        var readiness = await graphValidator.ValidateAsync(reconstructed);

        readiness.IsValid.Should().BeFalse();
        readiness.Errors.Should().ContainSingle()
            .Which.Code.Should().Be(WorkflowGraphValidationCodes.ConditionBindingUnavailable);

        if (readiness.IsValid)
        {
            await composer.ComposeAsync(reconstructed);
        }

        trackingComposer.ComposeCount.Should().Be(0);
    }

    private static ServiceCollection CreateServices(TrackingCondition? condition = null)
    {
        var services = new ServiceCollection();

        if (condition is not null)
        {
            services.AddSingleton(new ConditionBindingRegistration(condition.Name, condition));
        }

        services.AddPulseStack();
        services.AddPulseStackAgents();
        return services;
    }

    private static WorkflowAsset CreateWorkflow(
        IReadOnlyCollection<WorkflowStepDefinition> steps)
        => new WorkflowAssetFactory().Create(new WorkflowAssetOptions
        {
            Name = "workflow-conformance",
            Description = "MS-009.4C.4 composition conformance",
            Steps = steps
        });

    private static ParallelStepDefinition EmptyParallel(string name)
        => new()
        {
            Id = WorkflowStepId.New(),
            Name = name,
            Steps = []
        };

    private static T GetField<T>(object instance, string name)
        where T : class
        => (T)(instance.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(instance)
            ?? throw new InvalidOperationException(
                $"Field '{name}' was not found on '{instance.GetType().Name}'."));

    private sealed class TrackingCondition(string name) : ICondition
    {
        public string Name { get; } = name;

        public int EvaluationCount { get; private set; }

        public ValueTask<bool> EvaluateAsync(
            PulseStack.Abstractions.Agents.PipelineContext context,
            CancellationToken cancellationToken = default)
        {
            EvaluationCount++;
            return ValueTask.FromResult(true);
        }
    }

    private sealed class TrackingWorkflowComposer : IWorkflowComposer
    {
        public int ComposeCount { get; private set; }

        public Task<Workflow> ComposeAsync(
            WorkflowAsset workflow,
            CancellationToken cancellationToken = default)
        {
            ComposeCount++;
            throw new InvalidOperationException("Invalid Workflow must not reach composition.");
        }
    }
}
