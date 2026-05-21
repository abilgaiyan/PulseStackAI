using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;
using PulseStack.Agents.Runtime.Context;

namespace PulseStack.Agents.Runtime;

internal sealed class AgentExecutionContext
{
    public AgentExecutionContext(
        PipelineContext pipelineContext,
        IList<ChatMessage> messages,
        CancellationToken cancellationToken,
        IAgent? agent = null,
        IServiceProvider? services = null,
        IPipelineContextCloner? pipelineContextCloner = null)
    {
        PipelineContext = pipelineContext ?? throw new ArgumentNullException(nameof(pipelineContext));
        Messages = messages ?? throw new ArgumentNullException(nameof(messages));
        CancellationToken = cancellationToken;
        Agent = agent;
        Services = services;
        PipelineContextCloner = pipelineContextCloner ?? new PipelineContextCloner();
    }

    public IAgent? Agent { get; }

    public PipelineContext PipelineContext { get; }

    public CancellationToken CancellationToken { get; }

    public IList<ChatMessage> Messages { get; }

    public IList<ChatMessage> ToolExecutionResults { get; } = new List<ChatMessage>();

    public IServiceProvider? Services { get; }

    private IPipelineContextCloner PipelineContextCloner { get; }

    public AgentExecutionContext CreateBranch()
    {
        var clonedContext = PipelineContextCloner.Clone(PipelineContext);

        return new AgentExecutionContext(
            clonedContext,
            new List<ChatMessage>(Messages),
            CancellationToken,
            Agent,
            Services,
            PipelineContextCloner);
    }
}
