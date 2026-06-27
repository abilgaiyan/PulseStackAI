using PulseStack.Abstractions.Agents;

namespace PulseStack.Abstractions.Runtime.Pipeline;

public sealed class WorkflowBuilder
{
    private readonly WorkflowPipeline _workflow;

    internal WorkflowBuilder(
        string name)
    {
         ArgumentException.ThrowIfNullOrWhiteSpace(name);

        _workflow =
            new WorkflowPipeline(
                name);
    }

    public WorkflowBuilder Run(
        IAgent agent)
    {
       return AddNode(agent);
    }

    public WorkflowBuilder Workflow(
        WorkflowPipeline workflow)
    {
       return AddNode(workflow);
    }

    /// <summary>
    /// Adds a conditional branch to the workflow (uses default name "If").
    /// </summary>
    public WorkflowBuilder If(
        ICondition condition,
        IPipelineNode thenNode)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(thenNode);

       return AddNode(
            new ConditionalNode(
                "If",
                condition,
                thenNode));
    }

    /// <summary>
    /// Adds a conditional branch to the workflow with a custom name.
    /// The name will appear in diagnostics, logs, and future workflow visualizations.
    /// </summary>
    public WorkflowBuilder If(
        string name,
        ICondition condition,
        IPipelineNode thenNode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(thenNode);

        return AddNode(
            new ConditionalNode(
                name,
                condition,
                thenNode));
    }

    /// <summary>
    /// Wraps a node with retry logic (default name "Retry")
    /// </summary>
    public WorkflowBuilder Retry(
        IPipelineNode node,
        int maxAttempts = 3)
    {
        ArgumentNullException.ThrowIfNull(node);

        ValidateRetryAttempts(maxAttempts);

        return AddNode(new RetryNode("Retry", node, maxAttempts));
    }

    /// <summary>
    /// Wraps a node with retry logic using a custom name.
    /// </summary>
    public WorkflowBuilder Retry(
        string name,
        IPipelineNode node,
        int maxAttempts = 3)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(node);

        ValidateRetryAttempts(maxAttempts);

        return AddNode(new RetryNode(name, node, maxAttempts));
    }

    /// <summary>
    /// Creates a ForEach loop (default name "ForEach")
    /// </summary>
    public WorkflowBuilder ForEach(
        Func<PipelineContext, IEnumerable<object>> items,
        IPipelineNode node)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(node);

        return AddNode(new LoopNode("ForEach", items, node));
    }

    /// <summary>
    /// Creates a ForEach loop with a custom name.
    /// </summary>
    public WorkflowBuilder ForEach(
        string name,
        Func<PipelineContext, IEnumerable<object>> items,
        IPipelineNode node)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(node);

        return AddNode(new LoopNode(name, items, node));
    }

    /// <summary>
    /// Creates a Parallel execution block (default name "Parallel").
    /// </summary>
    public WorkflowBuilder Parallel(params IPipelineNode[] nodes)
        => Parallel("Parallel", nodes);

    /// <summary>
    /// Creates a Parallel execution block with a custom name.
    /// The name will appear in diagnostics, logs, and future workflow visualizations.
    /// </summary>
    public WorkflowBuilder Parallel(string name, params IPipelineNode[] nodes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(nodes);

        if (nodes.Length == 0)
            throw new ArgumentException(
                "At least one node is required for Parallel execution.",
                nameof(nodes));

        for (var i = 0; i < nodes.Length; i++)
        {
            if (nodes[i] is null)
                throw new ArgumentNullException(
                    nameof(nodes),
                    $"Node at index {i} is null.");
        }

        var parallel = new ParallelNode(name);
        foreach (var node in nodes)
            parallel.Add(node);

        return AddNode(parallel);
    }

    public WorkflowPipeline Build()
    {
        return _workflow;
    }

    private WorkflowBuilder AddNode(
        IPipelineNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        _workflow.Add(node);

        return this;
    }

    private static void ValidateRetryAttempts(
        int maxAttempts)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(
            maxAttempts,
            1);
    }
}
