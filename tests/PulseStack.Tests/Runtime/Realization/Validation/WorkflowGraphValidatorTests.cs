using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Runtime.Realization.Binding;
using PulseStack.Abstractions.Runtime.Realization.Resolution;
using PulseStack.Abstractions.Runtime.Realization.Validation;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Conditions;
using PulseStack.Abstractions.Workflows.Definitions;
using PulseStack.Abstractions.Workflows.Values;
using PulseStack.Core.Assets;
using PulseStack.Core.Runtime.Realization.Validation;
using Xunit;

namespace PulseStack.Tests.Runtime.Realization.Validation;

public sealed class WorkflowGraphValidatorTests
{
    [Fact]
    public async Task ValidateAsync_ShouldAcceptEmptyWorkflow()
    {
        var validator = CreateValidator();

        var result = await validator.ValidateAsync(CreateWorkflow([]));

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_ShouldReturnWfg001_WhenAgentIsMissing()
    {
        var agent = CreateAgent();
        var validator = CreateValidator();

        var result = await validator.ValidateAsync(CreateWorkflow([Run(agent)]));

        result.Errors.Should().ContainSingle();
        result.Errors[0].Code.Should().Be(WorkflowGraphValidationCodes.AgentDefinitionUnavailable);
        result.Errors[0].Path.Should().Be("$.steps[0].agent");
    }

    [Fact]
    public async Task ValidateAsync_ShouldReturnOnlyWfg003_WhenCatalogKeyDoesNotMatch()
    {
        var requested = CreateAgent();
        var returned = CreateAgent();
        var catalog = new StubCatalog(_ => returned);
        var validator = CreateValidator(catalog: catalog);

        var result = await validator.ValidateAsync(CreateWorkflow([Run(requested)]));

        result.Errors.Should().ContainSingle();
        result.Errors[0].Code.Should().Be(WorkflowGraphValidationCodes.CatalogDefinitionMismatch);
    }

    [Fact]
    public async Task ValidateAsync_ShouldReturnWfg003_WhenCatalogValueIsNotAgentDefinition()
    {
        var requested = CreateAgent();
        var nonAgent = new FakeAgentAsset
        {
            Id = requested.Id,
            Urn = requested.Urn,
            Version = requested.Version,
            Metadata = requested.Metadata
        };
        var validator = CreateValidator(catalog: new StubCatalog(_ => nonAgent));

        var result = await validator.ValidateAsync(CreateWorkflow([Run(requested)]));

        result.Errors.Should().ContainSingle();
        result.Errors[0].Code.Should().Be(WorkflowGraphValidationCodes.CatalogDefinitionMismatch);
    }

    [Fact]
    public async Task ValidateAsync_ShouldReturnOnlyWfg002_WhenCatalogUrnDoesNotMatch()
    {
        var requested = CreateAgent();
        var returned = requested with
        {
            Urn = new AssetUrn($"{requested.Urn}:other")
        };
        var validator = CreateValidator(catalog: new StubCatalog(_ => returned));

        var result = await validator.ValidateAsync(CreateWorkflow([Run(requested)]));

        result.Errors.Should().ContainSingle();
        result.Errors[0].Code.Should().Be(WorkflowGraphValidationCodes.AgentReferenceUrnConflict);
        result.Errors[0].Path.Should().Be("$.steps[0].agent.urn");
    }

    [Fact]
    public async Task ValidateAsync_ShouldPreserveNestedAgentDiagnostics()
    {
        var agent = CreateAgent();
        var nested = new[]
        {
            new AgentGraphValidationError("AGG001", "model missing", "$.model"),
            new AgentGraphValidationError("AGG003", "definition missing", "$.tools[0]")
        };
        var agentValidator = new StubAgentValidator(_ => new AgentGraphValidationResult(nested));
        var validator = CreateValidator(
            catalog: new StubCatalog(_ => agent),
            agentValidator: agentValidator);

        var result = await validator.ValidateAsync(CreateWorkflow([Run(agent)]));

        result.Errors.Should().ContainSingle();
        result.Errors[0].Code.Should().Be(WorkflowGraphValidationCodes.AgentGraphInvalid);
        result.Errors[0].AgentErrors.Should().Equal(nested);
    }

    [Fact]
    public async Task ValidateAsync_ShouldAcceptRegisteredConditionWithoutResolvingOrEvaluating()
    {
        var conditionCatalog = new StubConditionCatalog(_ => true);
        var validator = CreateValidator(conditionCatalog: conditionCatalog);

        var result = await validator.ValidateAsync(CreateWorkflow([
            Conditional("RequiresApproval")
        ]));

        result.IsValid.Should().BeTrue();
        conditionCatalog.ContainsCount.Should().Be(1);
    }

    [Fact]
    public async Task ValidateAsync_ShouldReturnWfg005AtConditionNamePath()
    {
        var validator = CreateValidator(
            conditionCatalog: new StubConditionCatalog(_ => false));

        var result = await validator.ValidateAsync(CreateWorkflow([
            Conditional("RequiresApproval")
        ]));

        result.Errors.Should().ContainSingle();
        result.Errors[0].Code.Should().Be(WorkflowGraphValidationCodes.ConditionBindingUnavailable);
        result.Errors[0].Path.Should().Be("$.steps[0].condition.name");
    }

    [Fact]
    public async Task ValidateAsync_ShouldDeduplicateAgentLookupAndValidation()
    {
        var agent = CreateAgent();
        var catalog = new StubCatalog(_ => agent);
        var agentValidator = new StubAgentValidator(_ => AgentGraphValidationResult.Success());
        var validator = CreateValidator(catalog, agentValidator);

        var result = await validator.ValidateAsync(CreateWorkflow([
            Run(agent),
            Run(agent)
        ]));

        result.IsValid.Should().BeTrue();
        catalog.FindCount.Should().Be(1);
        agentValidator.ValidateCount.Should().Be(1);
    }

    [Fact]
    public async Task ValidateAsync_ShouldDeduplicateConditionsCaseInsensitively()
    {
        var conditionCatalog = new StubConditionCatalog(_ => false);
        var validator = CreateValidator(conditionCatalog: conditionCatalog);

        var result = await validator.ValidateAsync(CreateWorkflow([
            Conditional("RequiresApproval"),
            Conditional("requiresapproval")
        ]));

        conditionCatalog.ContainsCount.Should().Be(1);
        result.Errors.Should().ContainSingle();
        result.Errors[0].Path.Should().Be("$.steps[0].condition.name");
    }

    [Fact]
    public async Task ValidateAsync_ShouldValidateDifferentAgentVersionsIndependently()
    {
        var first = CreateAgent();
        var second = first with
        {
            Version = new AssetVersion("2.0.0")
        };
        var catalog = new StubCatalog(key => key.Version == first.Version ? first : second);
        var agentValidator = new StubAgentValidator(_ => AgentGraphValidationResult.Success());
        var validator = CreateValidator(catalog, agentValidator);

        var result = await validator.ValidateAsync(CreateWorkflow([
            Run(first),
            Run(second)
        ]));

        result.IsValid.Should().BeTrue();
        catalog.FindCount.Should().Be(2);
        agentValidator.ValidateCount.Should().Be(2);
    }

    [Fact]
    public async Task ValidateAsync_ShouldReportLaterConflictingUrnWithoutSecondLookup()
    {
        var agent = CreateAgent();
        var conflicting = new AssetReference(
            AssetType.Agent,
            agent.Id,
            new AssetUrn($"{agent.Urn}:conflict"),
            agent.Version);
        var catalog = new StubCatalog(_ => agent);
        var validator = CreateValidator(catalog: catalog);

        // Author a valid Workflow first because WorkflowReferenceProjection correctly
        // rejects conflicting URNs at construction time. Then corrupt the detached
        // step snapshot to prove the readiness validator's defensive invariant.
        var workflow = CreateWorkflow([
            Run(agent),
            Run(agent)
        ]);
        var steps = (WorkflowStepDefinition[])workflow.Options.Steps;
        steps[1] = new RunStepDefinition
        {
            Id = WorkflowStepId.New(),
            Agent = conflicting
        };

        var result = await validator.ValidateAsync(workflow);

        catalog.FindCount.Should().Be(1);
        result.Errors.Should().ContainSingle();
        result.Errors[0].Code.Should().Be(WorkflowGraphValidationCodes.AgentReferenceUrnConflict);
        result.Errors[0].Path.Should().Be("$.steps[1].agent.urn");
    }

    [Fact]
    public async Task ValidateAsync_ShouldPreserveRecursiveAuthoredPathOrder()
    {
        var missing = CreateAgent();
        var validator = CreateValidator(
            conditionCatalog: new StubConditionCatalog(_ => false));

        var workflow = CreateWorkflow([
            new ParallelStepDefinition
            {
                Id = WorkflowStepId.New(),
                Name = "parallel",
                Steps =
                [
                    Run(missing),
                    new RetryStepDefinition
                    {
                        Id = WorkflowStepId.New(),
                        Step = new LoopStepDefinition
                        {
                            Id = WorkflowStepId.New(),
                            Items = new LiteralValueDefinition { Value = new[] { 1 } },
                            Step = Run(CreateAgent())
                        }
                    }
                ]
            },
            Conditional("missing-condition")
        ]);

        var result = await validator.ValidateAsync(workflow);

        result.Errors.Select(error => (error.Code, error.Path)).Should().Equal(
            (WorkflowGraphValidationCodes.AgentDefinitionUnavailable, "$.steps[0].steps[0].agent"),
            (WorkflowGraphValidationCodes.AgentDefinitionUnavailable, "$.steps[0].steps[1].step.step.agent"),
            (WorkflowGraphValidationCodes.ConditionBindingUnavailable, "$.steps[1].condition.name"));
    }

    [Fact]
    public async Task ValidateAsync_ShouldIgnoreWorkflowDependenciesAndRuntimeValues()
    {
        var validator = CreateValidator();
        var workflow = CreateWorkflow([
            new LoopStepDefinition
            {
                Id = WorkflowStepId.New(),
                Items = new ContextItemValueDefinition { Key = "runtime-only" },
                Step = new ParallelStepDefinition
                {
                    Id = WorkflowStepId.New(),
                    Name = "empty",
                    Steps = []
                }
            }
        ]);

        var result = await validator.ValidateAsync(workflow);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_ShouldPropagateCatalogException()
    {
        var agent = CreateAgent();
        var validator = CreateValidator(
            catalog: new StubCatalog(_ => throw new InvalidOperationException("catalog-fault")));

        var action = async () => await validator.ValidateAsync(CreateWorkflow([Run(agent)]));

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("catalog-fault");
    }

    [Fact]
    public async Task ValidateAsync_ShouldHonorCancellationBeforeTraversal()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var validator = CreateValidator();

        var action = async () => await validator.ValidateAsync(CreateWorkflow([]), source.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ValidateAsync_ShouldHonorCancellationAfterNonCooperativeCatalogLookup()
    {
        var agent = CreateAgent();
        using var source = new CancellationTokenSource();
        var catalog = new StubCatalog(_ =>
        {
            source.Cancel();
            return agent;
        });
        var validator = CreateValidator(catalog: catalog);

        var action = async () => await validator.ValidateAsync(CreateWorkflow([Run(agent)]), source.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ValidateAsync_ShouldHonorCancellationAfterNonCooperativeAgentValidation()
    {
        var agent = CreateAgent();
        using var source = new CancellationTokenSource();
        var agentValidator = new StubAgentValidator(_ =>
        {
            source.Cancel();
            return AgentGraphValidationResult.Success();
        });
        var validator = CreateValidator(
            new StubCatalog(_ => agent),
            agentValidator);

        var action = async () => await validator.ValidateAsync(CreateWorkflow([Run(agent)]), source.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    private static WorkflowGraphValidator CreateValidator(
        StubCatalog? catalog = null,
        StubAgentValidator? agentValidator = null,
        StubConditionCatalog? conditionCatalog = null)
        => new(
            catalog ?? new StubCatalog(_ => null),
            agentValidator ?? new StubAgentValidator(_ => AgentGraphValidationResult.Success()),
            conditionCatalog ?? new StubConditionCatalog(_ => true));

    private static WorkflowAsset CreateWorkflow(
        IReadOnlyCollection<WorkflowStepDefinition> steps)
        => new WorkflowAssetFactory().Create(new WorkflowAssetOptions
        {
            Name = "workflow",
            Steps = steps
        });

    private static AgentDefinition CreateAgent()
        => new AgentDefinitionFactory().Create(new AgentDefinitionOptions
        {
            Name = "agent",
            Goal = "goal",
            Role = "role"
        });

    private static RunStepDefinition Run(AgentDefinition agent)
        => new()
        {
            Id = WorkflowStepId.New(),
            Agent = new AssetReference(
                AssetType.Agent,
                agent.Id,
                agent.Urn,
                agent.Version)
        };

    private static ConditionalStepDefinition Conditional(string name)
        => new()
        {
            Id = WorkflowStepId.New(),
            Name = "if",
            Condition = new NamedConditionDefinition { Name = name },
            ThenStep = new ParallelStepDefinition
            {
                Id = WorkflowStepId.New(),
                Name = "then",
                Steps = []
            }
        };

    private sealed class StubCatalog(Func<AssetDefinitionKey, IAsset?> find) : IAssetDefinitionCatalog
    {
        public int FindCount { get; private set; }

        public ValueTask<IAsset?> FindAsync(
            AssetDefinitionKey key,
            CancellationToken cancellationToken = default)
        {
            FindCount++;
            return ValueTask.FromResult(find(key));
        }
    }

    private sealed class StubAgentValidator(Func<AgentDefinition, AgentGraphValidationResult> validate)
        : IAgentGraphValidator
    {
        public int ValidateCount { get; private set; }

        public ValueTask<AgentGraphValidationResult> ValidateAsync(
            AgentDefinition definition,
            CancellationToken cancellationToken = default)
        {
            ValidateCount++;
            return ValueTask.FromResult(validate(definition));
        }
    }

    private sealed class StubConditionCatalog(Func<string, bool> contains)
        : IConditionBindingCatalog
    {
        public int ContainsCount { get; private set; }

        public bool Contains(string name)
        {
            ContainsCount++;
            return contains(name);
        }
    }

    private sealed record FakeAgentAsset : Asset
    {
        public FakeAgentAsset() : base(AssetType.Agent)
        {
        }
    }
}
