using System.Collections.Concurrent;
using PulseStack.Abstractions.Tools;

namespace PulseStack.Core.Tools;

public sealed class ToolRegistry : IToolRegistry
{
    private readonly ConcurrentDictionary<string, ITool> _tools =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<ITool> GetAll()
        => _tools.Values.ToList();

    public ITool? GetByName(string name)
        => _tools.TryGetValue(name, out var tool) ? tool : null;

    public IReadOnlyCollection<ITool> GetByTag(string tag)
        => _tools.Values
            .Where(t => t.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            .ToList();

    public void Register(ITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        _tools[tool.Name] = tool;
    }
}
