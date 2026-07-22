using System.Text.Json;
using PulseStack.Abstractions.Persistence.Documents;
using PulseStack.Abstractions.Persistence.Serialization;

namespace PulseStack.Core.Persistence.Serialization;

public sealed class JsonWorkflowDeserializer : IWorkflowDeserializer
{
    public async ValueTask<WorkflowDocument> DeserializeAsync(
        Stream input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var document = await JsonSerializer.DeserializeAsync<WorkflowDocument>(
            input,
            WorkflowJsonOptions.Default,
            cancellationToken);

        if (document is null)
        {
            throw new InvalidOperationException(
                "The input stream does not contain a valid PulseStack workflow document.");
        }

        return document;
    }
}