using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Runtime.Realization.Binding;
using PulseStack.Abstractions.Runtime.Realization.Resolution;
using PulseStack.Abstractions.Runtime.Realization.Validation;
using PulseStack.Abstractions.Workflows.Conditions;
using PulseStack.Abstractions.Workflows.Definitions;

namespace PulseStack.Core.Runtime.Realization.Validation;

public sealed class WorkflowGraphValidator(
    IAssetDefinitionCatalog assetCatalog,
    IAgentGraphValidator agentGraphValidator,
    IConditionBindingCatalog conditionBindingCatalog)
    : IWorkflowGraphValidator
{
    private readonly IAssetDefinitionCatalog _assetCatalog =
        assetCatalog ?? throw new ArgumentNullException(nameof(assetCatalog));

    private readonly IAgentGraphValidator _agentGraphValidator =
        agentGraphValidator ?? throw new ArgumentNullException(nameof(agentGraphValidator));

    private readonly IConditionBindingCatalog _conditionBindingCatalog =
        conditionBindingCatalog ?? throw new ArgumentNullException(nameof(conditionBindingCatalog));

    public async ValueTask<WorkflowGraphValidationResult> ValidateAsync(
        WorkflowAsset workflow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        cancellationToken.ThrowIfCancellationRequested();

        var errors = new List<WorkflowGraphValidationError>();
        var agents = new Dictionary<AssetDefinitionKey, CachedAgentReference>();
        var conditions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < workflow.Options.Steps.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ValidateStepAsync(
                    workflow.Options.Steps[index],
                    $"$.steps[{index}]",
                    agents,
                    conditions,
                    errors,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return new WorkflowGraphValidationResult(errors);
    }

    private async ValueTask ValidateStepAsync(
        WorkflowStepDefinition step,
        string path,
        Dictionary<AssetDefinitionKey, CachedAgentReference> agents,
        HashSet<string> conditions,
        List<WorkflowGraphValidationError> errors,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (step)
        {
            case RunStepDefinition run:
                await ValidateAgentAsync(
                        run.Agent,
                        $"{path}.agent",
                        agents,
                        errors,
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ParallelStepDefinition parallel:
                for (var index = 0; index < parallel.Steps.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await ValidateStepAsync(
                            parallel.Steps[index],
                            $"{path}.steps[{index}]",
                            agents,
                            conditions,
                            errors,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                break;

            case ConditionalStepDefinition conditional:
                ValidateCondition(
                    conditional.Condition,
                    $"{path}.condition",
                    conditions,
                    errors,
                    cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();
                await ValidateStepAsync(
                        conditional.ThenStep,
                        $"{path}.thenStep",
                        agents,
                        conditions,
                        errors,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (conditional.ElseStep is not null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await ValidateStepAsync(
                            conditional.ElseStep,
                            $"{path}.elseStep",
                            agents,
                            conditions,
                            errors,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                break;

            case RetryStepDefinition retry:
                cancellationToken.ThrowIfCancellationRequested();
                await ValidateStepAsync(
                        retry.Step,
                        $"{path}.step",
                        agents,
                        conditions,
                        errors,
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            case LoopStepDefinition loop:
                cancellationToken.ThrowIfCancellationRequested();
                await ValidateStepAsync(
                        loop.Step,
                        $"{path}.step",
                        agents,
                        conditions,
                        errors,
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            case SwitchStepDefinition @switch:
                for (var index = 0; index < @switch.Cases.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await ValidateStepAsync(
                            @switch.Cases[index].Step,
                            $"{path}.cases[{index}].step",
                            agents,
                            conditions,
                            errors,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                if (@switch.DefaultStep is not null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await ValidateStepAsync(
                            @switch.DefaultStep,
                            $"{path}.defaultStep",
                            agents,
                            conditions,
                            errors,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                break;

            default:
                throw new NotSupportedException(
                    $"Workflow step definition '{step.GetType().Name}' is not supported by readiness validation.");
        }
    }

    private async ValueTask ValidateAgentAsync(
        AssetReference reference,
        string path,
        Dictionary<AssetDefinitionKey, CachedAgentReference> agents,
        List<WorkflowGraphValidationError> errors,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var key = new AssetDefinitionKey(reference.Type, reference.Id, reference.Version);

        if (agents.TryGetValue(key, out var cached))
        {
            if (cached.Urn != reference.Urn)
            {
                errors.Add(new WorkflowGraphValidationError(
                    WorkflowGraphValidationCodes.AgentReferenceUrnConflict,
                    $"Agent reference '{key}' conflicts with the first authored URN.",
                    $"{path}.urn"));
            }

            return;
        }

        agents.Add(key, new CachedAgentReference(reference.Urn, path));

        cancellationToken.ThrowIfCancellationRequested();
        var asset = await _assetCatalog.FindAsync(key, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (asset is null)
        {
            errors.Add(new WorkflowGraphValidationError(
                WorkflowGraphValidationCodes.AgentDefinitionUnavailable,
                $"Agent definition '{key}' is unavailable.",
                path));
            return;
        }

        if (asset.Type != key.Type ||
            asset.Id != key.Id ||
            asset.Version != key.Version ||
            asset is not AgentDefinition agent)
        {
            errors.Add(new WorkflowGraphValidationError(
                WorkflowGraphValidationCodes.CatalogDefinitionMismatch,
                $"Catalog result for '{key}' does not match the required Agent definition.",
                path));
            return;
        }

        if (agent.Urn != reference.Urn)
        {
            errors.Add(new WorkflowGraphValidationError(
                WorkflowGraphValidationCodes.AgentReferenceUrnConflict,
                $"Agent reference URN does not match the catalog definition for '{key}'.",
                $"{path}.urn"));
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var agentResult = await _agentGraphValidator
            .ValidateAsync(agent, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (!agentResult.IsValid)
        {
            errors.Add(new WorkflowGraphValidationError(
                WorkflowGraphValidationCodes.AgentGraphInvalid,
                $"Agent definition '{key}' is not ready for realization.",
                path,
                agentResult.Errors));
        }
    }

    private void ValidateCondition(
        ConditionDefinition condition,
        string path,
        HashSet<string> conditions,
        List<WorkflowGraphValidationError> errors,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (condition is not NamedConditionDefinition named)
        {
            throw new NotSupportedException(
                $"Condition definition '{condition.GetType().Name}' is not supported by readiness validation.");
        }

        if (!conditions.Add(named.Name))
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var available = _conditionBindingCatalog.Contains(named.Name);
        cancellationToken.ThrowIfCancellationRequested();

        if (!available)
        {
            errors.Add(new WorkflowGraphValidationError(
                WorkflowGraphValidationCodes.ConditionBindingUnavailable,
                $"Runtime condition '{named.Name}' is not registered.",
                $"{path}.name"));
        }
    }

    private sealed record CachedAgentReference(
        AssetUrn Urn,
        string FirstPath);
}
