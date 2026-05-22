using PulseStack.Abstractions.Agents;

namespace PulseStack.Agents.Runtime;

internal interface IPipelineExecutionStrategy
{
    Task<(
        string FinalOutput,
        IReadOnlyList<PipelineStepResult> Steps,
        IReadOnlyList<string> Errors)> ExecuteAsync(
            string pipelineName,
            IReadOnlyList<IAgent> agents,
            PipelineContext context,
            AgentExecutionContext executionContext,
            CancellationToken cancellationToken);
}
