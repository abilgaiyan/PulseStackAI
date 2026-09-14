using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;

namespace PulseStack.Abstractions.Runtime.Realization.Application;

/// <summary>
/// Coordinates realization of one accepted declarative AI Asset graph into
/// the existing runtime representation without executing it.
/// </summary>
public interface IApplicationRealizer
{
    Task<ApplicationRealizationResult> RealizeAsync(
        AIAssetGraph graph,
        CancellationToken cancellationToken = default);
}
