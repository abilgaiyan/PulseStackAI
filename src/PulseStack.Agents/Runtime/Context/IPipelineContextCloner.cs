using PulseStack.Abstractions.Agents;

namespace PulseStack.Agents.Runtime.Context;

internal interface IPipelineContextCloner
{
    PipelineContext Clone(
        PipelineContext context);
}
