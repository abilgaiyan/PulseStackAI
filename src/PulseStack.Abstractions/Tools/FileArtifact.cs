namespace PulseStack.Abstractions.Tools;

public sealed class FileArtifact
    : IToolArtifact
{
    public string Name { get; init; }
        = string.Empty;

    public string ContentType { get; init; }
        = "application/octet-stream";

    public long? Size { get; init; }

    public string FilePath { get; init; }
        = string.Empty;
}
