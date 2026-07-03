using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflow.Nodes;
using PulseStack.Agents.Runtime.Usage;


namespace PulseStack.Agents.Runtime.Composition;
internal sealed class ParallelNodeExecutor
    : CompositeNodeExecutor
{
    public ParallelNodeExecutor(
        INodeExecutorResolver resolver)
        : base(resolver)
    {
    }

    public override bool CanExecute(
        IPipelineNode node)
        => node is ParallelNode;

    public override async Task<NodeExecutionResult> ExecuteAsync(
        IPipelineNode node,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var parallelNode =
            (ParallelNode)node;

        var tasks =
            parallelNode.Nodes
                .Select(async child =>
                {
                    return await ExecuteNodeAsync(
                        child,
                        context,
                        cancellationToken);
                });

        var results =
            await Task.WhenAll(tasks);

       var output =
            string.Join(
                Environment.NewLine,
                results
                    .Select(x => x.Output)
                    .Where(x => !string.IsNullOrWhiteSpace(x)));

        context.CurrentOutput = output;

        return new NodeExecutionResult
        {
            NodeName = parallelNode.Name,
            Success = results.All(x => x.Success),
            Output = output,
            Usage =
                results.Any(x => x.Usage is not null)
                    ? new UsageAggregator()
                        .Aggregate(
                            results.Select(x => x.Usage))
                    : null
        };
    }
}
