using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Agents.Runtime.Usage;


namespace PulseStack.Agents.Runtime.Composition;
internal sealed class ParallelStepExecutor
    : CompositeStepExecutor
{
    public ParallelStepExecutor(
        IStepExecutorResolver resolver)
        : base(resolver)
    {
    }

    public override bool CanExecute(
        IWorkflowStep step)
        => step is ParallelStep;

    public override async Task<StepExecutionResult> ExecuteAsync(
        IWorkflowStep step,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var ParallelStep =
            (ParallelStep)step;

        var tasks =
            ParallelStep.Steps
                .Select(async child =>
                {
                    return await ExecuteStepAsync(
                        child,
                        context,
                        cancellationToken);
                });

        var results =
            await Task.WhenAll(tasks);

       var output =
            string.Join(
                Environment.NewLine,
                results
                    .Select(x => x.Output)
                    .Where(x => !string.IsNullOrWhiteSpace(x)));

        context.CurrentOutput = output;

        return new StepExecutionResult
        {
            StepName = ParallelStep.Name,
            Success = results.All(x => x.Success),
            Output = output,
            Usage =
                results.Any(x => x.Usage is not null)
                    ? new UsageAggregator()
                        .Aggregate(
                            results.Select(x => x.Usage))
                    : null
        };
    }
}
