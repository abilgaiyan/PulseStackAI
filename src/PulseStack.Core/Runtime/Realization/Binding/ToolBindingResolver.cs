using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Runtime.Realization.Binding;
using PulseStack.Abstractions.Tools;

namespace PulseStack.Core.Runtime.Realization.Binding;

public sealed class ToolBindingResolver : IToolBindingResolver
{
    private readonly IReadOnlyDictionary<AssetId, ToolBindingRegistration> _bindings;
    private readonly IToolRegistry _tools;

    public ToolBindingResolver(
        IEnumerable<ToolBindingRegistration> bindings,
        IToolRegistry tools)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(tools);

        _tools = tools;
        _bindings = bindings.ToDictionary(
            binding => binding.Asset.Id);
    }

    public ITool Resolve(ToolAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (!_bindings.TryGetValue(asset.Id, out var binding) ||
            !string.Equals(binding.Asset.Urn.Value, asset.Urn.Value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Tool Asset '{asset.Urn.Value}' is not bound to a runtime Tool implementation.");
        }

        var tool = _tools.GetByName(binding.ToolName);

        return tool ?? throw new InvalidOperationException(
            $"Runtime Tool '{binding.ToolName}' registered for Asset '{asset.Urn.Value}' could not be resolved.");
    }
}
