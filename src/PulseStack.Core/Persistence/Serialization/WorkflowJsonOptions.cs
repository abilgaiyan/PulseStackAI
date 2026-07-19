using System.Text.Json;

namespace PulseStack.Core.Persistence.Serialization;

public static class WorkflowJsonOptions
{
    public static readonly JsonSerializerOptions Default = Create();

    private static JsonSerializerOptions Create()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true
        };
    }
}
