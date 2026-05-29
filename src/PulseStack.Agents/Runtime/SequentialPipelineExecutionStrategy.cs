using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Agents.Runtime.Context;
using PulseStack.Agents.Runtime.Diagnostics;

namespace PulseStack.Agents.Runtime;

internal sealed class SequentialPipelineExecutionStrategy
    : IPipelineExecutionStrategy
{
    private readonly AgentRuntime _agentRuntime;

    public SequentialPipelineExecutionStrategy()
        : this(new AgentRuntime(new RuntimeEventDispatcher()))
    {
    }

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
                errors
        };
    }
}
