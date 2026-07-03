
namespace PulseStack.Abstractions.Runtime.Pipeline;

/// <summary>
/// Represents a node that can participate
/// in a workflow pipeline.
/// </summary>
public interface IPipelineNode
{
    string Name { get; }
}