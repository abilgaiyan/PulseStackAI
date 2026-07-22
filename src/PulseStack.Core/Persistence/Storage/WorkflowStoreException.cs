namespace PulseStack.Core.Persistence.Storage;

public sealed class WorkflowStoreException : Exception
{
    public WorkflowStoreException(string message)
        : base(message)
    {
    }
}