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
        IPipelineNode thenNode) => If("If", condition, thenNode);

    /// <summary>
    /// Adds a conditional branch to the workflow with a custom name.
    /// The name will appear in diagnostics, logs, and future workflow visualizations.
    /// </summary>
    public WorkflowBuilder If(
        string name,
        ICondition condition,
        IPipelineNode thenNode)
    {
        ValidateName(name);
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
        int maxAttempts = 3) => Retry("Retry", node, maxAttempts);
    
    /// <summary>
    /// Wraps a node with retry logic using a custom name.
    /// </summary>
    public WorkflowBuilder Retry(
        string name,
        IPipelineNode node,
        int maxAttempts = 3)
    {
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(node);

        ValidateRetryAttempts(maxAttempts);

        return AddNode(
            new RetryNode(
                name, 
                node, 
                maxAttempts));
    }

    /// <summary>
    /// Creates a ForEach loop (default name "ForEach")
    /// </summary>
    public WorkflowBuilder ForEach(
        Func<PipelineContext, IEnumerable<object>> items,
        IPipelineNode node) => ForEach("ForEach", items, node);

    /// <summary>
    /// Creates a ForEach loop with a custom name.
    /// </summary>
    public WorkflowBuilder ForEach(
        string name,
        Func<PipelineContext, IEnumerable<object>> items,
        IPipelineNode node)
    {
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(node);

        return AddNode(
            new LoopNode(
                name, 
                items, 
                node));
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
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(nodes);

        ValidateNodes(nodes);

        var parallel = 
            new ParallelNode(
                name);
                
        foreach (var node in nodes)
            parallel.Add(node);

        return AddNode(parallel);
    }

    /// <summary>
    /// Adds a switch branch to the workflow (default name "Switch").
    /// </summary>
    public WorkflowBuilder Switch(
        Func<PipelineContext, string?> selector,
        IEnumerable<SwitchCase> cases,
        IPipelineNode? defaultNode = null)
        => Switch("Switch", selector, cases, defaultNode);

    /// <summary>
    /// Adds a switch branch to the workflow with a custom name.
    /// The name will appear in diagnostics, logs, and future workflow visualizations.
    /// </summary>
    public WorkflowBuilder Switch(
        string name,
        Func<PipelineContext, string?> selector,
        IEnumerable<SwitchCase> cases,
        IPipelineNode? defaultNode = null)
    {
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(cases);

        var caseList =
            cases as IReadOnlyList<SwitchCase>
            ?? cases.ToArray();

        ValidateSwitchCases(caseList);
        
        return AddNode(
            new SwitchNode(
                name,
                selector,
                caseList,
                defaultNode));
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

    private static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
    }

    private static void ValidateRetryAttempts(
        int maxAttempts)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(
            maxAttempts,
            1);
    }

    private static void ValidateNodes(
        IReadOnlyList<IPipelineNode> nodes)
    {
         if (nodes.Count == 0)
            throw new ArgumentException(
                "At least one node is required for Parallel execution.",
                nameof(nodes));

        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is null)
                throw new ArgumentNullException(
                    nameof(nodes),
                     $"Node at index {i} cannot be null.");
        }

    }

    private static void ValidateSwitchCases(
        IReadOnlyList<SwitchCase> caseList)
    {
         if (caseList.Count == 0)
            throw new ArgumentException(
                "Switch requires at least one case.",
                nameof(caseList));

        for (var i = 0; i < caseList.Count; i++)
        {
            if (caseList[i] is null)
                throw new ArgumentNullException(
                    nameof(caseList),
                     $"SwitchCase at index {i} cannot be null.");
        }

    }
}
