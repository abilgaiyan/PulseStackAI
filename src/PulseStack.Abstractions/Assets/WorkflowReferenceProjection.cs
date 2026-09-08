using PulseStack.Abstractions.Workflows.Definitions;

namespace PulseStack.Abstractions.Assets;

internal static class WorkflowReferenceProjection
{
    internal static IReadOnlyList<AssetReference> Create(
        IEnumerable<WorkflowStepDefinition> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);

        var projected = new List<AssetReference>();
        var seen = new Dictionary<AssetDefinitionKey, AssetReference>();

        foreach (var step in steps)
        {
            ArgumentNullException.ThrowIfNull(step);

            foreach (var reference in Collect(step))
            {
                ArgumentNullException.ThrowIfNull(reference);

                var key = AssetDefinitionKey.From(reference);
                if (seen.TryGetValue(key, out var existing))
                {
                    if (!string.Equals(
                            existing.Urn.Value,
                            reference.Urn.Value,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Workflow Run steps contain conflicting URNs for the same Asset definition identity.");
                    }

                    continue;
                }

                seen.Add(key, reference);
                projected.Add(reference);
            }
        }

        return projected.ToArray();
    }

    private static IEnumerable<AssetReference> Collect(
        WorkflowStepDefinition step)
    {
        ArgumentNullException.ThrowIfNull(step);

        return step switch
        {
            RunStepDefinition run => [run.Agent],

            ParallelStepDefinition parallel =>
                parallel.Steps.SelectMany(Collect),

            ConditionalStepDefinition conditional =>
                Collect(conditional.ThenStep)
                    .Concat(
                        conditional.ElseStep is null
                            ? []
                            : Collect(conditional.ElseStep)),

            RetryStepDefinition retry =>
                Collect(retry.Step),

            LoopStepDefinition loop =>
                Collect(loop.Step),

            SwitchStepDefinition @switch =>
                @switch.Cases
                    .SelectMany(@case => Collect(@case.Step))
                    .Concat(
                        @switch.DefaultStep is null
                            ? []
                            : Collect(@switch.DefaultStep)),

            _ => []
        };
    }
}
