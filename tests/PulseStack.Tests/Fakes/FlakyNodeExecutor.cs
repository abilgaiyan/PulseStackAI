using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Agents;

namespace PulseStack.Tests.Fakes;

internal sealed class FlakyNodeExecutor : INodeExecutor
{
    private int _attempts;

    public int Attempts => _attempts;

    public bool CanExecute(
        IPipelineNode node)
        => node is FakeAgent;

    public Task<NodeExecutionResult> ExecuteAsync(
        IPipelineNode node,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        _attempts++;

        return Task.FromResult(
            new NodeExecutionResult
            {
                NodeName = node.Name,
                Success = _attempts >= 2,
                Output = _attempts >= 2
                    ? "Success"
                    : null
            });
    }
}
