using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Runtime.Realization;
using PulseStack.Abstractions.Runtime.Realization.Binding;
using PulseStack.Abstractions.Runtime.Realization.Composition;
using PulseStack.Abstractions.Runtime.Realization.Resolution;
using PulseStack.Abstractions.Tools;
using PulseStack.Agents.Realization.Binding;
using PulseStack.Agents.Realization.Composition;
using PulseStack.Core.Runtime.Realization;
using PulseStack.Core.Tools;

namespace PulseStack.Agents.Runtime.Realization;

internal sealed class AgentComposer : IAgentComposer
{
    private readonly IAssetResolver _assetResolver;
    private readonly ModelRealizer _modelRealizer;
    private readonly PromptRealizer _promptRealizer;
    private readonly IToolBindingResolver _toolBindingResolver;
    private readonly IToolExecutor _toolExecutor;

    public AgentComposer(
        IAssetResolver assetResolver,
        ModelRealizer modelRealizer,
        PromptRealizer promptRealizer,
        IToolBindingResolver toolBindingResolver,
        IToolExecutor toolExecutor)
    {
        ArgumentNullException.ThrowIfNull(assetResolver);
        ArgumentNullException.ThrowIfNull(modelRealizer);
        ArgumentNullException.ThrowIfNull(promptRealizer);
        ArgumentNullException.ThrowIfNull(toolBindingResolver);
        ArgumentNullException.ThrowIfNull(toolExecutor);

        _assetResolver = assetResolver;
        _modelRealizer = modelRealizer;
        _promptRealizer = promptRealizer;
        _toolBindingResolver = toolBindingResolver;
        _toolExecutor = toolExecutor;
    }

    public async Task<IAgent> ComposeAsync(
        AgentDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var options = definition.Options;

        if (options.Model is null)
        {
            throw new InvalidOperationException(
                $"Agent '{options.Name}' requires a Model Asset reference.");
        }

        EnsureUnsupportedReferencesAreNotConfigured(options);

        var modelAsset = await ResolveModelAsync(
            options.Model,
            cancellationToken);

        var client = _modelRealizer.Realize(modelAsset);

        RuntimePrompt? prompt = null;

        if (options.Prompt is not null)
        {
            var promptAsset = await ResolvePromptAsync(
                options.Prompt,
                cancellationToken);

            prompt = _promptRealizer.Realize(promptAsset);
        }

        var tools = await BindToolsAsync(
            options.Tools,
            cancellationToken);

        var composition = new AgentComposition
        {
            Definition = definition,
            Model = modelAsset,
            ChatClient = client,
            Prompt = prompt
        };

        var binding = new AgentBinding
        {
            ToolExecutor = _toolExecutor,
            Tools = tools
        };

        return new Agent(
            composition,
            binding);
    }

    private async Task<ModelAsset> ResolveModelAsync(
        AssetReference reference,
        CancellationToken cancellationToken)
    {
        var asset = await _assetResolver.ResolveAsync(
            reference,
            cancellationToken);

        if (asset is null)
        {
            throw new InvalidOperationException(
                $"Model Asset '{reference.Urn.Value}' could not be resolved.");
        }

        if (asset is not ModelAsset modelAsset)
        {
            throw new InvalidOperationException(
                $"Asset '{reference.Urn.Value}' is '{asset.Type}', but a Model Asset is required.");
        }

        return modelAsset;
    }

    private async Task<PromptAsset> ResolvePromptAsync(
        AssetReference reference,
        CancellationToken cancellationToken)
    {
        var asset = await _assetResolver.ResolveAsync(
            reference,
            cancellationToken);

        if (asset is null)
        {
            throw new InvalidOperationException(
                $"Prompt Asset '{reference.Urn.Value}' could not be resolved.");
        }

        if (asset is not PromptAsset promptAsset)
        {
            throw new InvalidOperationException(
                $"Asset '{reference.Urn.Value}' is '{asset.Type}', but a Prompt Asset is required.");
        }

        return promptAsset;
    }

    private async Task<IToolRegistry?> BindToolsAsync(
        IReadOnlyCollection<AssetReference> references,
        CancellationToken cancellationToken)
    {
        if (references.Count == 0)
        {
            return null;
        }

        var registry = new ToolRegistry();

        foreach (var reference in references)
        {
            var asset = await _assetResolver.ResolveAsync(
                reference,
                cancellationToken);

            if (asset is null)
            {
                throw new InvalidOperationException(
                    $"Tool Asset '{reference.Urn.Value}' could not be resolved.");
            }

            if (asset is not ToolAsset toolAsset)
            {
                throw new InvalidOperationException(
                    $"Asset '{reference.Urn.Value}' is '{asset.Type}', but a Tool Asset is required.");
            }

            registry.Register(
                _toolBindingResolver.Resolve(toolAsset));
        }

        return registry;
    }

    private static void EnsureUnsupportedReferencesAreNotConfigured(
        AgentDefinitionOptions options)
    {
        if (options.Knowledge.Count > 0 ||
            options.Memory is not null ||
            options.Policies.Count > 0)
        {
            throw new NotSupportedException(
                "Knowledge, Memory, and Policy Asset realization is not implemented yet.");
        }
    }
}
