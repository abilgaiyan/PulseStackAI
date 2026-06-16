using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;

namespace PulseStack.Tests.Fakes;

public sealed class FakeNodeExecutor
    : INodeExecutor
{
    private readonly List<string>? _executionOrder;

    public FakeNodeExecutor(
        List<string>? executionOrder = null)
    {
        _executionOrder = executionOrder;
    }

    public bool CanExecute(
        IPipelineNode node)
        => true;

    public Task<NodeExecutionResult> ExecuteAsync(
        IPipelineNode node,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        _executionOrder?.Add(node.Name);

        context.CurrentOutput =
            node.Name;

        return Task.FromResult(
            new NodeExecutionResult
            {
                NodeName = node.Name,
                Success = true,
                Output = node.Name
            });
    }
}