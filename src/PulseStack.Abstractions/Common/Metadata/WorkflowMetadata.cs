
namespace PulseStack.Abstractions.Common.Metdata;
public sealed record WorkflowMetadata(
    string Name,
    string? Description = null,
    string? Category = null,
    IReadOnlyList<string>? Tags = null,
    string? Author = null);