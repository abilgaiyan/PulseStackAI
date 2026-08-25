using System.Collections;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Runtime.Realization.Binding;
using PulseStack.Abstractions.Runtime.Realization.Composition;
using PulseStack.Abstractions.Runtime.Realization.Evaluation;
using PulseStack.Abstractions.Runtime.Realization.Resolution;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Definitions;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Core.Runtime.Realization.Evaluation;

namespace PulseStack.Core.Runtime.Realization.Composition;

public sealed class WorkflowComposer : IWorkflowComposer
{
    private readonly IAssetResolver _assetResolver;
    private readonly IAgentComposer _agentComposer;
    private readonly IConditionBindingResolver _conditionBindingResolver;
    private readonly IWorkflowValueEvaluator _workflowValueEvaluator;

    public WorkflowComposer(
        IAssetResolver assetResolver,
        IAgentComposer agentComposer,
        IConditionBindingResolver conditionBindingResolver)
        : this(
            assetResolver,
            agentComposer,
            conditionBindingResolver,
            new WorkflowValueEvaluator())
    {
    }

    public WorkflowComposer(
        IAssetResolver assetResolver,
        IAgentComposer agentComposer,
        IConditionBindingResolver conditionBindingResolver,
        IWorkflowValueEvaluator workflowValueEvaluator)
    {
        ArgumentNullException.ThrowIfNull(assetResolver);
        ArgumentNullException.ThrowIfNull(agentComposer);
        ArgumentNullException.ThrowIfNull(conditionBindingResolver);
        ArgumentNullException.ThrowIfNull(workflowValueEvaluator);

        _assetResolver = assetResolver;
        _agentComposer = agentComposer;
        _conditionBindingResolver = conditionBindingResolver;
        _workflowValueEvaluator = workflowValueEvaluator;
    }

    public async Task<Workflow> ComposeAsync(
        WorkflowAsset workflow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        var runtime = new Workflow(
            WorkflowIdentity.Create(),
            WorkflowStepId.New(),
            new WorkflowDefinition(
                workflow.Options.Name,
                workflow.Options.Description));

        foreach (var step in workflow.Options.Steps)
        {
            runtime.Add(
                await ComposeStepAsync(
                    step,
                    cancellationToken));
        }

        return runtime;
    }

    private async Task<IWorkflowStep> ComposeStepAsync(
        WorkflowStepDefinition step,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);

        return step switch
        {
            RunStepDefinition run =>
                await ComposeRunStepAsync(run, cancellationToken),

            ParallelStepDefinition parallel =>
                await ComposeParallelStepAsync(parallel, cancellationToken),

            ConditionalStepDefinition conditional =>
                await ComposeConditionalStepAsync(conditional, cancellationToken),

            RetryStepDefinition retry =>
                await ComposeRetryStepAsync(retry, cancellationToken),

            LoopStepDefinition loop =>
                await ComposeLoopStepAsync(loop, cancellationToken),

            _ => throw new NotSupportedException(
                $"Workflow step definition '{step.GetType().Name}' is not supported by realization yet.")
        };
    }

    private async Task<RunStep> ComposeRunStepAsync(
        RunStepDefinition step,
        CancellationToken cancellationToken)
    {
        var asset = await _assetResolver.ResolveAsync(
            step.Agent,
            cancellationToken);

        if (asset is null)
        {
            throw new InvalidOperationException(
                $"Agent Asset '{step.Agent.Urn.Value}' could not be resolved.");
        }

        if (asset is not AgentDefinition definition)
        {
            throw new InvalidOperationException(
                $"Asset '{step.Agent.Urn.Value}' is '{asset.Type}', but an Agent Asset is required.");
        }

        var agent = await _agentComposer.ComposeAsync(
            definition,
            cancellationToken);

        return new RunStep(step.Id, agent);
    }

    private async Task<ParallelStep> ComposeParallelStepAsync(
        ParallelStepDefinition step,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(step.Name);

        var runtime = new ParallelStep(
            step.Id,
            step.Name);

        foreach (var child in step.Steps)
        {
            runtime.Add(
                await ComposeStepAsync(
                    child,
                    cancellationToken));
        }

        return runtime;
    }

    private async Task<ConditionalStep> ComposeConditionalStepAsync(
        ConditionalStepDefinition step,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(step.Name);
        ArgumentNullException.ThrowIfNull(step.Condition);
        ArgumentNullException.ThrowIfNull(step.ThenStep);

        var condition = _conditionBindingResolver.Resolve(step.Condition);
        var thenStep = await ComposeStepAsync(step.ThenStep, cancellationToken);
        var elseStep = step.ElseStep is null
            ? null
            : await ComposeStepAsync(step.ElseStep, cancellationToken);

        return new ConditionalStep(
            step.Id,
            step.Name,
            condition,
            thenStep,
            elseStep);
    }

    private async Task<RetryStep> ComposeRetryStepAsync(
        RetryStepDefinition step,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(step.Name);
        ArgumentNullException.ThrowIfNull(step.Step);
        ArgumentOutOfRangeException.ThrowIfLessThan(step.MaxAttempts, 1);

        var child = await ComposeStepAsync(
            step.Step,
            cancellationToken);

        return new RetryStep(
            step.Id,
            step.Name,
            child,
            step.MaxAttempts);
    }

    private async Task<LoopStep> ComposeLoopStepAsync(
        LoopStepDefinition step,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(step.Name);
        ArgumentNullException.ThrowIfNull(step.Items);
        ArgumentNullException.ThrowIfNull(step.Step);

        var child = await ComposeStepAsync(
            step.Step,
            cancellationToken);

        IEnumerable<object> ResolveItems(PipelineContext context)
        {
            var value = _workflowValueEvaluator.Evaluate(
                step.Items,
                context);

            if (value is string || value is not IEnumerable enumerable)
            {
                throw new InvalidOperationException(
                    $"ForEach step '{step.Name}' requires an enumerable workflow value.");
            }

            return enumerable.Cast<object>();
        }

        return new LoopStep(
            step.Id,
            step.Name,
            ResolveItems,
            child);
    }
}
