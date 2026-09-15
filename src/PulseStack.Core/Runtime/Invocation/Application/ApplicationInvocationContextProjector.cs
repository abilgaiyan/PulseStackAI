using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Invocation.Application;

namespace PulseStack.Core.Runtime.Invocation.Application;

internal static class ApplicationInvocationContextProjector
{
    public static PipelineContext Project(ApplicationInvocationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = new PipelineContext
        {
            Input = request.Input,
            CurrentOutput = request.Input
        };

        foreach (var item in request.Items)
        {
            context.Items.Add(item.Key, item.Value);
        }

        return context;
    }
}
