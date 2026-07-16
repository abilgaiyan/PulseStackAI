using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflows;

namespace PulseStack.Agents.Runtime.Composition;

internal sealed class RunStepExecutor
    : IStepExecutor
{
    private readonly AgentRuntime _agentRuntime;

    public RunStepExecutor(
        AgentRuntime agentRuntime)
    {
        _agentRuntime = agentRuntime;
    }

    public bool CanExecute(
        IWorkflowStep step)
        => step is RunStep;

    public async Task<StepExecutionResult> ExecuteAsync(
        IWorkflowStep step,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var runStep = (RunStep)step;
        var agent = runStep.Agent;

        var executionContext =
            new AgentExecutionContext(
                context,
                [],
                cancellationToken);

        var result =
            await _agentRuntime.ExecuteAsync(
                agent,
                context,
                executionContext,
                new PipelineExecutionPolicy(),
                cancellationToken);

        return new StepExecutionResult
        {
            StepName = agent.Name,
            Success = result.Success,
            Output = result.Output,
            Usage = result.Usage
        };
    }
}
