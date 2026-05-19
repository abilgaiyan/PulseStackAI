
namespace PulseStack.Abstractions.Tools;
public interface ITool
{
    string Name { get; }

    string Description { get; }

    string Category { get; }
    bool IsEnabled => true;
    IReadOnlyCollection<string> Tags { get; }
    ToolDescriptor Descriptor { get; }

    Task<IToolExecutionResult> ExecuteAsync(
        ToolExecutionContext context,
        CancellationToken cancellationToken = default);
}
