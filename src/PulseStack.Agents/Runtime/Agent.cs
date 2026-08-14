using PulseStack.Abstractions.Agents;
using PulseStack.Agents.Realization.Composition;
using PulseStack.Agents.Realization.Binding;

namespace PulseStack.Agents.Runtime;

internal sealed class Agent :
    IAgent,
    IAgentRuntime,
    IRuntimeAgentExecutor
{
    private readonly AgentComposition _composition;
    private readonly AgentRuntime _runtime;
    
    public string Name { get; }
    internal string Model =>
        _composition.Model.Options.Model;

    internal Agent(
        AgentComposition composition,
        AgentBinding binding,
        string? instructions = null)
    {
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(composition.Definition);
        ArgumentNullException.ThrowIfNull(composition.Model);
        ArgumentNullException.ThrowIfNull(composition.ChatClient);
        ArgumentNullException.ThrowIfNull(binding.ToolExecutor);

        _composition = composition;

        Name = composition.Definition.Options.Name;

        _runtime = new AgentRuntime(
            composition.ChatClient,
            binding.ToolExecutor,
            instructions,
            binding.Temperature,
            binding.Tools,
            binding.Memory,
            composition.Model.Options.Model,
            this);
    }

    public Task<AgentResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        var context = new PipelineContext
        {
            Input = input,
            CurrentOutput = input
        };

        return _runtime.RunAsync(
            context,
            cancellationToken);
    }

    Task<AgentResponse> IAgentRuntime.RunAsync(
        PipelineContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return _runtime.RunAsync(
            context,
            cancellationToken);
    }
    
    Task<AgentResponse> IRuntimeAgentExecutor.RunAsync(
        PipelineContext context,
        AgentExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(executionContext);

        return _runtime.RunCoreAsync(
            context,
            executionContext,
            cancellationToken);
    }

    public IAsyncEnumerable<string> StreamAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        return _runtime.StreamAsync(
            input,
            cancellationToken);
    }
}
