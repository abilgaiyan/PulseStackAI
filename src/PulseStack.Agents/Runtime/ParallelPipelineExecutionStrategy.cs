using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Tools;
using PulseStack.Agents.Runtime.Context;
using PulseStack.Agents.Runtime.Diagnostics;

namespace PulseStack.Agents.Runtime;

internal sealed class ParallelPipelineExecutionStrategy
    : IPipelineExecutionStrategy
{
    private readonly AgentRuntime _agentRuntime;

    public ParallelPipelineExecutionStrategy()
        : this(new AgentRuntime(new RuntimeEventDispatcher()))
    {
    }

    internal ParallelPipelineExecutionStrategy(
        AgentRuntime agentRuntime)
    {
        _agentRuntime = agentRuntime ?? throw new ArgumentNullException(nameof(agentRuntime));
    }

    public async Task<PipelineExecutionState> ExecuteAsync(
        string pipelineName,
        IReadOnlyList<IAgent> agents,
        PipelineContext context,
        AgentExecutionContext executionContext,
        PipelineExecutionPolicy policy,
        CancellationToken cancellationToken = default)
    {
        var baseStepCount =
            context.Steps.Count;

        var baseToolResultCount =
            context.ToolResults.Count;

        var tasks = agents
            .Select((agent, index) => RunBranchAsync(
                agent,
                index,
                executionContext,
                baseStepCount,
                baseToolResultCount,
                _agentRuntime,
                policy,
                cancellationToken))
            .ToArray();

        var results =
            await Task.WhenAll(tasks);

        var errors =
            new List<PipelineExecutionError>();

        foreach (var result in results.OrderBy(r => r.Index))
        {
            foreach (var step in result.Steps)
            {
                context.Steps.Add(step);
            }

            foreach (var toolResult in result.ToolResults)
            {
                context.ToolResults.Add(toolResult);
            }

            if (result.Error is not null)
            {
                context.Items[
                    PipelineContextKeys.AgentError(
                        result.Agent.Name)] =
                            result.Error.Message;

                errors.Add(
                    new PipelineExecutionError
                    {
                        Code = "parallel_agent_execution_failed",

                        Message = result.Error.Message,

                        AgentName = result.Agent.Name,

                        Exception = result.Error
                    });

                continue;
            }

            context.Items[
                PipelineContextKeys.AgentOutput(
                    result.Agent.Name)] =
                        result.Output;
        }

        var outputs = results
            .OrderBy(r => r.Index)
            .Where(r => r.Error is null)
            .Select(r => r.Output)
            .ToArray();

        context.CurrentOutput =
            string.Join(
                Environment.NewLine,
                outputs);

        return new PipelineExecutionState
        {
            FinalOutput =
                context.CurrentOutput
                ?? string.Empty,

            Steps =
                context.Steps.ToList(),

            Errors =
                errors
        };
    }

    private static async Task<BranchResult> RunBranchAsync(
        IAgent agent,
        int index,
        AgentExecutionContext executionContext,
        int baseStepCount,
        int baseToolResultCount,
        AgentRuntime agentRuntime,
        PipelineExecutionPolicy policy,
        CancellationToken cancellationToken)
    {
        var branch = executionContext.CreateBranch();

        branch.PipelineContext.Items[
            PipelineContextKeys.RuntimeExecutionId] =
                branch.ExecutionId;

        branch.PipelineContext.Items[
            PipelineContextKeys.RuntimeBranchId] =
                branch.BranchId;

        branch.PipelineContext.Items[
            PipelineContextKeys.RuntimeEventDispatcher] =
                branch.EventDispatcher;

        var input =
            branch.PipelineContext.CurrentOutput;

        var result =
            await agentRuntime.ExecuteAsync(
                agent,
                branch.PipelineContext,
                branch,
                policy,
                cancellationToken);

        branch.PipelineContext.Steps.Add(
            new PipelineStepResult(
                agent.Name,
                agent.Model,
                input,
                result.Success ? result.Output : null,
                result.Success,
                result.StartedAt,
                result.CompletedAt,
                result.RetryCount));

        if (result.Success)
        {
            return BranchResult.Success(
                index,
                agent,
                result.Output,
                branch.PipelineContext.Steps
                    .Skip(baseStepCount)
                    .ToList(),
                branch.PipelineContext.ToolResults
                    .Skip(baseToolResultCount)
                    .ToList());
        }

        var exception =
            result.Exception
            ?? new InvalidOperationException(
                "Agent execution failed.");

        return BranchResult.Failure(
            index,
            agent,
            branch.PipelineContext.Steps
                .Skip(baseStepCount)
                .ToList(),
            branch.PipelineContext.ToolResults
                .Skip(baseToolResultCount)
                .ToList(),
            exception);
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
            IReadOnlyList<PipelineStepResult> steps,
            IReadOnlyList<ToolExecutionRecord> toolResults,
            Exception error)
            => new(
                index,
                agent,
                string.Empty,
                steps,
                toolResults,
                error);
    }
}

               
