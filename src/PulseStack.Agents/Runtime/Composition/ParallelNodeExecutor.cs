using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;


namespace PulseStack.Agents.Runtime.Composition;
internal sealed class ParallelNodeExecutor
    : INodeExecutor
{
    private readonly INodeExecutorResolver _resolver;

    public ParallelNodeExecutor(
        INodeExecutorResolver resolver)
    {
        _resolver = resolver;
    }

    public bool CanExecute(
        IPipelineNode node)
        => node is ParallelNode;

    public async Task<NodeExecutionResult> ExecuteAsync(
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
                    var executor =
                        _resolver.Resolve(child);

                    return await executor.ExecuteAsync(
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
            Output = output
        };
    }
}