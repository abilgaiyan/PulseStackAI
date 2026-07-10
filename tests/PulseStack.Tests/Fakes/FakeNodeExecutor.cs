using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Abstractions.Runtime.Usage;

namespace PulseStack.Tests.Fakes;

public sealed class FakeNodeExecutor
    : IStepExecutor
{
    private readonly List<string>? _executionOrder;
    private readonly bool _success;
    private readonly string? _output;
    private readonly AIUsage? _usage;

    public FakeNodeExecutor(
        List<string>? executionOrder = null,
        bool success = true,
        string? output = null,
        AIUsage? usage = null)
    {
        _executionOrder = executionOrder;
        _success = success;
        _output = output;
        _usage = usage;
    }

    public bool CanExecute(
        IWorkflowStep step)
        => true;

    public Task<StepExecutionResult> ExecuteAsync(
        IWorkflowStep step,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        _executionOrder?.Add(step.Name);

        var output =
            _output ?? step.Name;

        context.CurrentOutput = output;

        return Task.FromResult(
            new StepExecutionResult
            {
                StepName = step.Name,
                Success = _success,
                Output = output,
                Usage = _usage
            });
    }
}
