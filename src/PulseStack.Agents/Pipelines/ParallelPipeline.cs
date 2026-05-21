using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Tools;
using PulseStack.Agents.Runtime;
using PulseStack.Agents.Runtime.Context;
using PulseStack.Agents.Runtime.Diagnostics;
using PulseStack.Agents.Runtime.Diagnostics.Events;

namespace PulseStack.Agents.Pipelines;

/// <summary>
/// Executes agents concurrently against isolated
/// branched execution contexts.
/// </summary>
public sealed class ParallelPipeline
    : IAgentPipeline
{
    private readonly List<IAgent> _agents = [];
    private readonly IRuntimeEventDispatcher _eventDispatcher;

    public string Name { get; }

    public ParallelPipeline(string name)
        : this(
            name,
            new RuntimeEventDispatcher())
    {
    }

    internal ParallelPipeline(
        string name,
        IRuntimeEventDispatcher eventDispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    public ParallelPipeline Add(
        IAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        _agents.Add(agent);

        return this;
    }

    public Task<PipelineResult> RunAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        var context = new PipelineContext
        {
            Input = input,
            CurrentOutput = input
        };

        return RunAsync(
            context,
            cancellationToken);
    }

    public async Task<PipelineResult> RunAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_agents.Count == 0)
        {
            throw new InvalidOperationException(
                "Pipeline contains no agents.");
        }

        var baseExecutionContext = new AgentExecutionContext(
            context,
            new List<ChatMessage>(),
            cancellationToken,
            eventDispatcher: _eventDispatcher);

        context.Items[PipelineContextKeys.RuntimeExecutionId] =
            baseExecutionContext.ExecutionId;
        context.Items[PipelineContextKeys.RuntimeEventDispatcher] =
            _eventDispatcher;

        _eventDispatcher.Dispatch(
            new PipelineStartedEvent(
                baseExecutionContext.ExecutionId,
                DateTimeOffset.UtcNow,
                Name,
                _agents.Count,
                SnapshotMetadata(context.Items)));

        var baseStepCount = context.Steps.Count;
        var baseToolResultCount = context.ToolResults.Count;

        var tasks = _agents
            .Select((agent, index) => RunBranchAsync(
                agent,
                index,
                baseExecutionContext,
                baseStepCount,
                baseToolResultCount,
                cancellationToken))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        foreach (var result in results.OrderBy(r => r.Index))
        {
            if (result.Error is not null)
            {
                context.Items[PipelineContextKeys.AgentError(result.Agent.Name)] =
                    result.Error.Message;

                continue;
            }

            foreach (var step in result.Steps)
            {
                context.Steps.Add(step);
            }

            foreach (var toolResult in result.ToolResults)
            {
                context.ToolResults.Add(toolResult);
            }

            context.Items[PipelineContextKeys.AgentOutput(result.Agent.Name)] =
                result.Output;
        }

        var outputs = results
            .OrderBy(r => r.Index)
            .Where(r => r.Error is null)
            .Select(r => r.Output)
            .ToArray();

        context.CurrentOutput = string.Join(
            Environment.NewLine,
            outputs);

        _eventDispatcher.Dispatch(
            new PipelineCompletedEvent(
                baseExecutionContext.ExecutionId,
                DateTimeOffset.UtcNow,
                Name,
                _agents.Count,
                results.Count(r => r.Error is null),
                results.Count(r => r.Error is not null),
                SnapshotMetadata(context.Items)));

        return new PipelineResult(
            context.CurrentOutput,
            context.Steps.ToList());
    }

    private static async Task<BranchResult> RunBranchAsync(
        IAgent agent,
        int index,
        AgentExecutionContext baseExecutionContext,
        int baseStepCount,
        int baseToolResultCount,
        CancellationToken cancellationToken)
    {
        var branch = baseExecutionContext.CreateBranch();
        branch.PipelineContext.Items[PipelineContextKeys.RuntimeExecutionId] =
            branch.ExecutionId;
        branch.PipelineContext.Items[PipelineContextKeys.RuntimeBranchId] =
            branch.BranchId;
        branch.PipelineContext.Items[PipelineContextKeys.RuntimeEventDispatcher] =
            branch.EventDispatcher;

        var input = branch.PipelineContext.CurrentOutput;

        try
        {
            var response = await agent.RunAsync(
                branch.PipelineContext,
                cancellationToken);

            var output =
                response.Text
                ?? branch.PipelineContext.CurrentOutput
                ?? string.Empty;

            branch.PipelineContext.CurrentOutput = output;

            branch.PipelineContext.Steps.Add(
                new PipelineStepResult(
                    agent.Name,
                    agent.Model,
                    input,
                    output));

            return BranchResult.Success(
                index,
                agent,
                output,
                branch.PipelineContext.Steps
                // TODO:
                // Current implementation derives branch-local
                // execution records using collection deltas.
                //
                // Future orchestration runtimes may introduce
                // explicit branch tracking for nested and
                // recursive execution flows.
                    .Skip(baseStepCount)
                    .ToList(),
                branch.PipelineContext.ToolResults
                    .Skip(baseToolResultCount)
                    .ToList());
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return BranchResult.Failure(
                index,
                agent,
                ex);
        }
    }

    private sealed record BranchResult(
        int Index,
        IAgent Agent,
        string Output,
        IReadOnlyList<PipelineStepResult> Steps,
        IReadOnlyList<ToolExecutionRecord> ToolResults,
        Exception? Error)
    {
        public static BranchResult Success(
            int index,
            IAgent agent,
            string output,
            IReadOnlyList<PipelineStepResult> steps,
            IReadOnlyList<ToolExecutionRecord> toolResults)
            => new(
                index,
                agent,
                output,
                steps,
                toolResults,
                null);

        public static BranchResult Failure(
            int index,
            IAgent agent,
            Exception error)
            => new(
                index,
                agent,
                string.Empty,
                [],
                [],
                error);
    }

    private static IReadOnlyDictionary<string, object?> SnapshotMetadata(
        IDictionary<string, object?> metadata)
    {
        var snapshot = new Dictionary<string, object?>();

        foreach (var item in metadata)
        {
            if (item.Key == PipelineContextKeys.RuntimeEventDispatcher)
            {
                continue;
            }

            snapshot[item.Key] = item.Value;
        }

        return snapshot;
    }
}
