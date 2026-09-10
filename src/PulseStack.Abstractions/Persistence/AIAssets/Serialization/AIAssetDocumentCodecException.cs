namespace PulseStack.Abstractions.Persistence.AIAssets.Serialization;

public sealed class AIAssetDocumentCodecException : Exception
{
    public AIAssetDocumentCodecException(
        AIAssetDocumentCodecOperation operation,
        AIAssetDocumentCodecFailureReason failureReason,
        string message,
        string? memberName = null,
        string? token = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Operation = operation;
        FailureReason = failureReason;
        MemberName = memberName;
        Token = token;
    }

    public AIAssetDocumentCodecOperation Operation { get; }

    public AIAssetDocumentCodecFailureReason FailureReason { get; }

    public string? MemberName { get; }

    public string? Token { get; }
}
