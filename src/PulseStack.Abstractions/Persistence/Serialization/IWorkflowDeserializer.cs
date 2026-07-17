using PulseStack.Abstractions.Persistence.Documents;

namespace PulseStack.Abstractions.Persistence.Serialization;

public interface IWorkflowDeserializer
{
    ValueTask<WorkflowDocument> DeserializeAsync(
        Stream input,
        CancellationToken cancellationToken = default);
}