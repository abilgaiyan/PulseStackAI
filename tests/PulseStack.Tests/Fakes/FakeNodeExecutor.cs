using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Runtime.Usage;

namespace PulseStack.Tests.Fakes;

public sealed class FakeNodeExecutor
    : INodeExecutor
{
    private readonly List<string>? _executionOrder;
    private readonly bool _success;
    private readonly string? _output;
    private readonly AIUsage? _usage;

    public FakeNodeExecutor(
        List<string>? executionOrder = null,
        bool success = true,
        string? output = null,
        AIUsage? usage = null)
    {
        _executionOrder = executionOrder;
        _success = success;
        _output = output;
        _usage = usage;
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

        var output =
            _output ?? node.Name;

        context.CurrentOutput = output;

        return Task.FromResult(
            new NodeExecutionResult
            {
                NodeName = node.Name,
                Success = _success,
                Output = output,
                Usage = _usage
            });
    }
}
