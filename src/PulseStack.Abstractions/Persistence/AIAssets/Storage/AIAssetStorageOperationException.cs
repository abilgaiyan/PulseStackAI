namespace PulseStack.Abstractions.Persistence.AIAssets.Storage;

/// <summary>
/// Adds safe MS-009.7 operation context to an exception whose classification is
/// owned by another contract, such as an MS-009.6 codec exception.
/// </summary>
/// <remarks>
/// This wrapper does not define or replace the classification of the inner exception.
/// Its context always identifies one valid MS-009.7 operation and exact validated asset key.
/// </remarks>
public sealed class AIAssetStorageOperationException : Exception
{
    public AIAssetStorageOperationException(
        string message,
        AIAssetStorageDiagnosticContext context,
        Exception innerException)
        : base(message, innerException)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(innerException);

        if (context.Operation is null || !Enum.IsDefined(context.Operation.Value))
        {
            throw new ArgumentException(
                "The diagnostic context must identify a defined MS-009.7 storage operation.",
                nameof(context));
        }

        if (context.Key is null)
        {
            throw new ArgumentException(
                "The diagnostic context must identify the exact validated asset definition key.",
                nameof(context));
        }

        AIAssetStorageContract.EnsureValidKey(context.Key.Value);
        Context = context;
    }

    public AIAssetStorageDiagnosticContext Context { get; }
}
