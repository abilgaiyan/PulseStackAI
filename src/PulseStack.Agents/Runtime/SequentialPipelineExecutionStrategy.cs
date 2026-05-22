using PulseStack.Abstractions.Agents;
using PulseStack.Agents.Runtime.Context;

namespace PulseStack.Agents.Runtime;

internal sealed class SequentialPipelineExecutionStrategy
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
        foreach (var agent in agents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var input = context.CurrentOutput;

            var response = await agent.RunAsync(
                input,
                cancellationToken);

            var output = response.Text ?? string.Empty;

            var step = new PipelineStepResult(
                agent.Name,
                agent.Model,
                input,
                output);

            context.Steps.Add(step);

            context.Items[PipelineContextKeys.AgentOutput(agent.Name)]
                = output;

            context.CurrentOutput = output;
        }

        return (
            context.CurrentOutput,
            context.Steps.ToList(),
            []);
    }
}
