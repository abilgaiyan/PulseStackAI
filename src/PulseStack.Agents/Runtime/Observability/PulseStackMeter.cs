using System.Diagnostics.Metrics;

namespace PulseStack.Agents.Runtime.Observability;

internal static class PulseStackMeter
{
    public static readonly Meter Meter =
        new("PulseStack.Runtime");
}
