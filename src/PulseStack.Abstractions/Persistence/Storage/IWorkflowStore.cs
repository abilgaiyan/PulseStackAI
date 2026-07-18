
namespace PulseStack.Abstractions.Persistence.Storage;

public interface IWorkflowStore
{
    ValueTask SaveAsync(
        string name,
        Stream input,
        CancellationToken cancellationToken = default);

    ValueTask<Stream> LoadAsync(
        string name,
        CancellationToken cancellationToken = default);
}
