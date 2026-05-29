using System.Diagnostics;

namespace PulseStack.Agents.Runtime.Observability;

public static class PulseStackActivitySource
{
    public static readonly ActivitySource Source =
        new("PulseStack.Runtime");
}
