using System.Text.Json;
using PulseStack.Abstractions.Persistence.Serialization;
using PulseStack.Abstractions.Persistence.Documents;

namespace PulseStack.Core.Persistence.Serialization;

public sealed class JsonWorkflowSerializer : IWorkflowSerializer
{
    public async ValueTask SerializeAsync(
        WorkflowDocument document,
        Stream output,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(output);

        await JsonSerializer.SerializeAsync(
            output,
            document,
            WorkflowJsonOptions.Default,
            cancellationToken);
    }

}
