using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Agents.Runtime.Context;

namespace PulseStack.Agents.Runtime;

internal sealed class SequentialPipelineExecutionStrategy
    : IPipelineExecutionStrategy
{
    public async Task<PipelineExecutionState> ExecuteAsync(
        string pipelineName,
        IReadOnlyList<IAgent> agents,
        PipelineContext context,
        AgentExecutionContext executionContext,
        CancellationToken cancellationToken = default)
    {
        var errors =
            new List<PipelineExecutionError>();

        foreach (var agent in agents)
        {
            var input =
                context.CurrentOutput;

            try
            {
                var response =
                    await agent.RunAsync(
                        context,
                        cancellationToken);

                var output =
                    response.Text
                    ?? context.CurrentOutput
                    ?? string.Empty;

                context.CurrentOutput =
                    output;

                context.Steps.Add(
                    new PipelineStepResult(
                        agent.Name,
                        agent.Model,
                        input,
                        output));
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add(
                    new PipelineExecutionError
                    {
                        Code = "sequential_agent_execution_failed",

                        Message = ex.Message,

                        AgentName = agent.Name,

                        Exception = ex
                    });

                context.Items[
                    PipelineContextKeys.AgentError(
                        agent.Name)] =
                            ex.Message;
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
