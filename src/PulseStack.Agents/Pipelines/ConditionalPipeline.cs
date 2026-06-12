using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Agents.Runtime;
using PulseStack.Agents.Runtime.Diagnostics.Events;
using PulseStack.Agents.Runtime.Observability;
using PulseStack.Agents.Runtime.Diagnostics;

namespace PulseStack.Agents.Pipelines;

public sealed class ConditionalPipeline : IAgentPipeline
{
    private readonly List<IAgent> _trueAgents = [];

    private readonly List<IAgent> _falseAgents = [];

    private readonly ICondition _condition;

    private readonly PipelineRuntime _runtime;

    private readonly IRuntimeEventDispatcher _eventDispatcher;
    private readonly IPipelineExecutionStrategy _strategy;

    private PipelineExecutionPolicy _policy = new();

    public string Name { get; }

    public ConditionalPipeline(
        string name,
        ICondition condition,
        IRuntimeObserver observer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(observer);

        Name = name;
        _condition = condition;

        var dispatcher =
            new RuntimeEventDispatcher(observer);
        
        _eventDispatcher = dispatcher;    

        var agentRuntime =
            new AgentRuntime(dispatcher);

        _runtime =
            new PipelineRuntime(dispatcher);

        _strategy =
            new SequentialPipelineExecutionStrategy(
                agentRuntime);
    }

    public async Task<PipelineExecutionResult> RunDetailedAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var result =
            await _condition.EvaluateAsync(
                context,
                cancellationToken);
                
        context.Items["ConditionResult"] = result;                

        var conditionName = _condition.Name;

        _eventDispatcher.Dispatch(
            new ConditionEvaluatedEvent(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                conditionName,
                result));

        var selectedAgents =
            result
                ? _trueAgents
                : _falseAgents;

        context.Items["ConditionalBranch"] = selectedAgents;                

        if (selectedAgents.Count == 0)
        {
            throw new InvalidOperationException(
                result
                    ? "True branch contains no agents."
                    : "False branch contains no agents.");
        }                

        return await _runtime.ExecuteAsync(
            Name,
            selectedAgents,
            context,
            _strategy,
            _policy,
            cancellationToken);
    }

    public async Task<PipelineResult> RunAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        var detailed =
            await RunDetailedAsync(
                input,
                cancellationToken);

        return new PipelineResult(
            detailed.FinalOutput,
            detailed.Steps);
    }

    public async Task<PipelineResult> RunAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var detailed =
            await RunDetailedAsync(
                context,
                cancellationToken);

        return new PipelineResult(
            detailed.FinalOutput,
            detailed.Steps);
    }

    public Task<PipelineExecutionResult> RunDetailedAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        var context = new PipelineContext
        {
            Input = input,
            CurrentOutput = input
        };

        return RunDetailedAsync(
            context,
            cancellationToken);
    }

    public ConditionalPipeline AddTrueAgent(
        IAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        _trueAgents.Add(agent);

        return this;
    }

    public ConditionalPipeline AddFalseAgent(
        IAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        _falseAgents.Add(agent);

        return this;
    }

    public ConditionalPipeline WithPolicy(
        PipelineExecutionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        _policy = policy;

        return this;
    }

}