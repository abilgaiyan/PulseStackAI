using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;
using PulseStack.Abstractions.Runtime.Invocation.Application;
using PulseStack.Abstractions.Runtime.Realization.Application;

namespace PulseStack.Abstractions.Runtime.Application;

/// <summary>
/// Identifies the authoritative stage that produced the terminal non-exceptional
/// outcome of a portable application operation while preserving that stage's result.
/// </summary>
public abstract class ApplicationOperationResult
{
    private ApplicationOperationResult()
    {
    }

    public sealed class LoadOutcome : ApplicationOperationResult
    {
        public LoadOutcome(AIAssetGraphLoadResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            if (result is AIAssetGraphLoadResult.Success)
            {
                throw new ArgumentException(
                    "A successful graph-load result cannot be a terminal load outcome.",
                    nameof(result));
            }

            Result = result;
        }

        public AIAssetGraphLoadResult Result { get; }
    }

    public sealed class RealizationOutcome : ApplicationOperationResult
    {
        public RealizationOutcome(ApplicationRealizationResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            if (result is ApplicationRealizationResult.Success)
            {
                throw new ArgumentException(
                    "A successful realization result cannot be a terminal realization outcome.",
                    nameof(result));
            }

            Result = result;
        }

        public ApplicationRealizationResult Result { get; }
    }

    public sealed class InvocationOutcome : ApplicationOperationResult
    {
        public InvocationOutcome(ApplicationInvocationResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            Result = result;
        }

        public ApplicationInvocationResult Result { get; }
    }
}
