namespace PulseStack.Abstractions.Runtime.Pipeline;

public interface INodeExecutorResolver
{
    INodeExecutor Resolve(
        IPipelineNode node);
}
