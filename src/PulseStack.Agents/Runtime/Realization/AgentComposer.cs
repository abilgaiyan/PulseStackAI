using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Memory;
using PulseStack.Abstractions.Runtime.Realization.Composition;
using PulseStack.Abstractions.Runtime.Realization.Resolution;
using PulseStack.Abstractions.Tools;
using PulseStack.Core.Runtime.Realization;

namespace PulseStack.Agents.Runtime.Realization;

/// <summary>
/// Composes an Agent Asset into the runtime Agent representation.
/// </summary>
internal sealed class AgentComposer : IAgentComposer
{
    private readonly IAssetResolver _assetResolver;
    private readonly ModelRealizer _modelRealizer;
    private readonly IToolExecutor _toolExecutor;
    private readonly IToolRegistry _toolRegistry;
    private readonly IConversationMemory _memory;

    public AgentComposer(
        IAssetResolver assetResolver,
        ModelRealizer modelRealizer,
        IToolExecutor toolExecutor,
        IToolRegistry toolRegistry,
        IConversationMemory memory)
    {
        ArgumentNullException.ThrowIfNull(assetResolver);
        ArgumentNullException.ThrowIfNull(modelRealizer);
        ArgumentNullException.ThrowIfNull(toolExecutor);
        ArgumentNullException.ThrowIfNull(toolRegistry);
        ArgumentNullException.ThrowIfNull(memory);

        _assetResolver = assetResolver;
        _modelRealizer = modelRealizer;
        _toolExecutor = toolExecutor;
        _toolRegistry = toolRegistry;
        _memory = memory;
    }

    public async Task<IAgentRuntime> ComposeAsync(
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

        return new Agent(
            options.Name,
            client,
            _toolExecutor,
            instructions: null,
            temperature: null,
            tools: null,
            memory: null,
            model: modelAsset.Options.Model,
            fallbackModels: null);
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

    private static void EnsureUnsupportedReferencesAreNotConfigured(
        AgentDefinitionOptions options)
    {
        if (options.Prompt is not null ||
            options.Knowledge.Count > 0 ||
            options.Tools.Count > 0 ||
            options.Memory is not null ||
            options.Policies.Count > 0)
        {
            throw new NotSupportedException(
                "Prompt, Knowledge, Tool, Memory, and Policy Asset realization is not implemented yet.");
        }
    }
}
