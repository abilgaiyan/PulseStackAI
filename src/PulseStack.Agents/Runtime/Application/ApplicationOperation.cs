using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;
using PulseStack.Abstractions.Runtime.Application;
using PulseStack.Abstractions.Runtime.Invocation.Application;
using PulseStack.Abstractions.Runtime.Realization.Application;

namespace PulseStack.Agents.Runtime.Application;

/// <summary>
/// Coordinates one persisted Project application operation through the existing
/// graph-loading, realization, and invocation authorities.
/// </summary>
internal sealed class ApplicationOperation : IApplicationOperation
{
    private readonly IAIAssetGraphLoader _graphLoader;
    private readonly IApplicationRealizer _realizer;
    private readonly IApplicationInvoker _invoker;

    public ApplicationOperation(
        IAIAssetGraphLoader graphLoader,
        IApplicationRealizer realizer,
        IApplicationInvoker invoker)
    {
        ArgumentNullException.ThrowIfNull(graphLoader);
        ArgumentNullException.ThrowIfNull(realizer);
        ArgumentNullException.ThrowIfNull(invoker);

        _graphLoader = graphLoader;
        _realizer = realizer;
        _invoker = invoker;
    }

    public async Task<ApplicationOperationResult> ExecuteAsync(
        AssetDefinitionKey projectKey,
        ApplicationInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        var loadResult = await _graphLoader
            .LoadAsync(projectKey, cancellationToken)
            .ConfigureAwait(false);

        if (loadResult is not AIAssetGraphLoadResult.Success loadSuccess)
        {
            return new ApplicationOperationResult.LoadOutcome(loadResult);
        }

        var realizationResult = await _realizer
            .RealizeAsync(loadSuccess.Graph, cancellationToken)
            .ConfigureAwait(false);

        if (realizationResult is not ApplicationRealizationResult.Success realizationSuccess)
        {
            return new ApplicationOperationResult.RealizationOutcome(realizationResult);
        }

        var invocationResult = await _invoker
            .InvokeAsync(realizationSuccess.Application, request, cancellationToken)
            .ConfigureAwait(false);

        return new ApplicationOperationResult.InvocationOutcome(invocationResult);
    }
}
