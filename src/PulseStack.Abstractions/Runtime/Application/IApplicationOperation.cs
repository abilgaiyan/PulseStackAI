using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Runtime.Invocation.Application;

namespace PulseStack.Abstractions.Runtime.Application;

/// <summary>
/// Coordinates one persisted Project application operation through loading,
/// realization, and invocation using the configured stage authorities.
/// </summary>
public interface IApplicationOperation
{
    Task<ApplicationOperationResult> ExecuteAsync(
        AssetDefinitionKey projectKey,
        ApplicationInvocationRequest request,
        CancellationToken cancellationToken = default);
}
