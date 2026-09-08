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

public sealed class WorkflowGraphValidatorAcceptanceTests
{
    [Fact]
    public async Task ValidateAsync_ShouldIgnoreNonemptyWorkflowDependencies()
    {
        var dependencyId = AssetId.New();
        var dependency = new AssetDependency(
            new AssetReference(
                AssetType.Tool,
                dependencyId,
                new AssetUrn($"urn:pulsestack:tool:{dependencyId}"),
                AssetVersion.Initial));
        var catalog = new StubCatalog(_ => null);
        var validator = CreateValidator(catalog: catalog);
        var workflow = CreateWorkflow([]) with
        {
            Dependencies = [dependency]
        };

        var result = await validator.ValidateAsync(workflow);

        result.IsValid.Should().BeTrue();
        catalog.FindCount.Should().Be(0);
    }

    [Fact]
    public async Task ValidateAsync_ShouldTraverseConditionalThenElseAndSwitchInFrozenOrder()
    {
        var thenAgent = CreateAgent();
        var caseOneAgent = CreateAgent();
        var caseTwoAgent = CreateAgent();
        var defaultAgent = CreateAgent();
        var validator = CreateValidator(
            conditionCatalog: new StubConditionCatalog(_ => false));

        var workflow = CreateWorkflow([
            new ConditionalStepDefinition
            {
                Id = WorkflowStepId.New(),
                Name = "conditional",
                Condition = new NamedConditionDefinition { Name = "missing-condition" },
                ThenStep = Run(thenAgent),
                ElseStep = new SwitchStepDefinition
                {
                    Id = WorkflowStepId.New(),
                    Name = "switch",
                    Selector = new LiteralValueDefinition { Value = "runtime-selector" },
                    Cases =
                    [
                        new SwitchCaseDefinition
                        {
                            Value = "one",
                            Step = Run(caseOneAgent)
                        },
                        new SwitchCaseDefinition
                        {
                            Value = "two",
                            Step = Run(caseTwoAgent)
                        }
                    ],
                    DefaultStep = Run(defaultAgent)
                }
            }
        ]);

        var result = await validator.ValidateAsync(workflow);

        result.Errors.Select(error => (error.Code, error.Path)).Should().Equal(
            (WorkflowGraphValidationCodes.ConditionBindingUnavailable, "$.steps[0].condition.name"),
            (WorkflowGraphValidationCodes.AgentDefinitionUnavailable, "$.steps[0].thenStep.agent"),
            (WorkflowGraphValidationCodes.AgentDefinitionUnavailable, "$.steps[0].elseStep.cases[0].step.agent"),
            (WorkflowGraphValidationCodes.AgentDefinitionUnavailable, "$.steps[0].elseStep.cases[1].step.agent"),
            (WorkflowGraphValidationCodes.AgentDefinitionUnavailable, "$.steps[0].elseStep.defaultStep.agent"));
    }

    [Fact]
    public async Task ValidateAsync_ShouldPropagateAgentGraphValidatorException()
    {
        var agent = CreateAgent();
        var validator = CreateValidator(
            catalog: new StubCatalog(_ => agent),
            agentValidator: new StubAgentValidator(
                _ => throw new InvalidOperationException("agent-validator-fault")));

        var action = async () => await validator.ValidateAsync(CreateWorkflow([Run(agent)]));

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("agent-validator-fault");
    }

    [Fact]
    public async Task ValidateAsync_ShouldPropagateConditionCatalogException()
    {
        var validator = CreateValidator(
            conditionCatalog: new StubConditionCatalog(
                _ => throw new InvalidOperationException("condition-catalog-fault")));

        var action = async () => await validator.ValidateAsync(CreateWorkflow([
            Conditional("requires-approval")
        ]));

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("condition-catalog-fault");
    }

    [Fact]
    public async Task ValidateAsync_ShouldHonorCancellationAfterNonCooperativeConditionLookup()
    {
        using var source = new CancellationTokenSource();
        var conditionCatalog = new StubConditionCatalog(_ =>
        {
            source.Cancel();
            return true;
        });
        var validator = CreateValidator(conditionCatalog: conditionCatalog);

        var action = async () => await validator.ValidateAsync(
            CreateWorkflow([Conditional("requires-approval")]),
            source.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        conditionCatalog.ContainsCount.Should().Be(1);
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
        public ValueTask<AgentGraphValidationResult> ValidateAsync(
            AgentDefinition definition,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(validate(definition));
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
}
