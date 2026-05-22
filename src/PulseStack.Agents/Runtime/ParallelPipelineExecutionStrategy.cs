using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Tools;
using PulseStack.Agents.Runtime.Context;

namespace PulseStack.Agents.Runtime;

internal sealed class ParallelPipelineExecutionStrategy
    : IPipelineExecutionStrategy
{
    public async Task<(
        string FinalOutput,
        IReadOnlyList<PipelineStepResult> Steps,
        IReadOnlyList<string> Errors)> ExecuteAsync(
            string pipelineName,
            IReadOnlyList<IAgent> agents,
            PipelineContext context,
            AgentExecutionContext executionContext,
            CancellationToken cancellationToken)
    {
        var baseStepCount = context.Steps.Count;
        var baseToolResultCount = context.ToolResults.Count;

        var tasks = agents
            .Select((agent, index) => RunBranchAsync(
                agent,
                index,
                executionContext,
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

        var errors = results
            .OrderBy(r => r.Index)
            .Where(r => r.Error is not null)
            .Select(r => r.Error!.Message)
            .ToList();

        return (
            context.CurrentOutput,
            context.Steps.ToList(),
            errors);
    }

    private static async Task<BranchResult> RunBranchAsync(
        IAgent agent,
        int index,
        AgentExecutionContext executionContext,
        int baseStepCount,
        int baseToolResultCount,
        CancellationToken cancellationToken)
    {
        var branch = executionContext.CreateBranch();

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
}
