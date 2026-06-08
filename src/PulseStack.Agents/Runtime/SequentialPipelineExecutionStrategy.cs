using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Runtime.Usage;
using PulseStack.Agents.Runtime.Context;
using PulseStack.Agents.Runtime.Diagnostics;
using PulseStack.Agents.Runtime.Usage;

namespace PulseStack.Agents.Runtime;

internal sealed class SequentialPipelineExecutionStrategy
    : IPipelineExecutionStrategy
{
    private readonly AgentRuntime _agentRuntime;

    internal SequentialPipelineExecutionStrategy(
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
        var errors =
            new List<PipelineExecutionError>();

        var usages =
            new List<AIUsage?>();

        foreach (var agent in agents)
        {
            var input =
                context.CurrentOutput;

            var result =
                await _agentRuntime.ExecuteAsync(
                    agent,
                    context,
                    executionContext,
                    policy,
                    cancellationToken);

            context.Steps.Add(
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
                usages.Add(result.Usage);

                continue;
            }

            var exception =
                result.Exception
                ?? new InvalidOperationException(
                    "Agent execution failed.");

            errors.Add(
                new PipelineExecutionError
                {
                    Code =
                        "sequential_agent_execution_failed",

                    Message =
                        exception.Message,

                    AgentName =
                        agent.Name,

                    Exception =
                        exception
                });

            context.Items[
                PipelineContextKeys.AgentError(
                    agent.Name)] =
                        exception.Message;

            if (!policy.ContinueOnAgentFailure)
            {
                throw exception;
            }
        }

        return new PipelineExecutionState
        {
            FinalOutput =
                context.CurrentOutput
                ?? string.Empty,

            Steps =
                context.Steps.ToList(),

            Errors =
                errors,

            TotalUsage =
                new UsageAggregator()
                    .Aggregate(usages)
        };
    }
}
