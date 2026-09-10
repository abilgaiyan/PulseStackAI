namespace PulseStack.Abstractions.Persistence.AIAssets.Storage;

/// <summary>
/// Adds safe MS-009.7 operation context to an exception whose classification is
/// owned by another contract, such as an MS-009.6 codec exception.
/// </summary>
/// <remarks>
/// This wrapper does not define or replace the classification of the inner exception.
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
        Context = context;
    }

    public AIAssetStorageDiagnosticContext Context { get; }
}
