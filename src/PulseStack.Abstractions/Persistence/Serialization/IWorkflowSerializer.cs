using PulseStack.Abstractions.Persistence.Documents;

namespace PulseStack.Abstractions.Persistence.Serialization;

public interface IWorkflowSerializer
{
    ValueTask SerializeAsync(
        WorkflowDocument document,
        Stream output,
        CancellationToken cancellationToken = default);
}