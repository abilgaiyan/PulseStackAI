namespace PulseStack.Abstractions.Tools;
public interface IToolArtifact
{
    string Name { get; }

    string ContentType { get; }

    long? Size { get; }
}
