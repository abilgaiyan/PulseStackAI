using PulseStack.Abstractions.Assets;
using PulseStack.Core.Assets;

namespace PulseStack.Agents.Builders;

/// <summary>
/// Fluent authoring builder for a declarative Agent Asset.
/// </summary>
/// <remarks>
/// The builder defines Agent Language only. It does not resolve providers,
/// create chat clients, register tools, or construct runtime agents.
/// </remarks>
public sealed class AgentBuilder
{
    private readonly string _name;
    private readonly AgentDefinitionFactory _factory;

    private string? _goal;
    private string? _role;
    private readonly List<string> _responsibilities = [];
    private AssetReference? _model;
    private AssetReference? _prompt;
    private readonly List<AssetReference> _knowledge = [];
    private readonly List<AssetReference> _tools = [];
    private AssetReference? _memory;
    private readonly List<AssetReference> _policies = [];

    public AgentBuilder(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        _name = name;
        _factory = new AgentDefinitionFactory();
    }

    public AgentBuilder WithGoal(string goal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goal);

        _goal = goal;
        return this;
    }

    public AgentBuilder WithRole(string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        _role = role;
        return this;
    }

    public AgentBuilder AddResponsibility(string responsibility)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responsibility);

        _responsibilities.Add(responsibility);
        return this;
    }

    public AgentBuilder AddResponsibilities(
        IEnumerable<string> responsibilities)
    {
        ArgumentNullException.ThrowIfNull(responsibilities);

        foreach (var responsibility in responsibilities)
        {
            AddResponsibility(responsibility);
        }

        return this;
    }

    public AgentBuilder UseModel(AssetReference model)
    {
        _model = model;
        return this;
    }

    public AgentBuilder UsePrompt(AssetReference prompt)
    {
        _prompt = prompt;
        return this;
    }

    public AgentBuilder UseKnowledge(AssetReference knowledge)
    {
        _knowledge.Add(knowledge);
        return this;
    }

    public AgentBuilder UseTool(AssetReference tool)
    {
        _tools.Add(tool);
        return this;
    }

    public AgentBuilder UseMemory(AssetReference memory)
    {
        _memory = memory;
        return this;
    }

    public AgentBuilder UsePolicy(AssetReference policy)
    {
        _policies.Add(policy);
        return this;
    }

    public AgentDefinition Build()
    {
        if (string.IsNullOrWhiteSpace(_goal))
        {
            throw new InvalidOperationException(
                "Agent goal has not been configured.");
        }

        if (string.IsNullOrWhiteSpace(_role))
        {
            throw new InvalidOperationException(
                "Agent role has not been configured.");
        }

        var options = new AgentDefinitionOptions
        {
            Name = _name,
            Goal = _goal,
            Role = _role,
            Responsibilities = _responsibilities.ToArray(),
            Model = _model,
            Prompt = _prompt,
            Knowledge = _knowledge.ToArray(),
            Tools = _tools.ToArray(),
            Memory = _memory,
            Policies = _policies.ToArray()
        };

        return _factory.Create(options);
    }
}
