using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Runtime.Realization.Resolution;
using PulseStack.Abstractions.Runtime.Realization.Validation;

namespace PulseStack.Core.Runtime.Realization.Validation;

public sealed class AgentGraphValidator : IAgentGraphValidator
{
    private readonly IAssetDefinitionCatalog catalog;

    public AgentGraphValidator(IAssetDefinitionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        this.catalog = catalog;
    }

    public async ValueTask<AgentGraphValidationResult> ValidateAsync(
        AgentDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        cancellationToken.ThrowIfCancellationRequested();

        var errors = new List<AgentGraphValidationError>();

        if (definition.Options.Model is null)
        {
            AddError(
                errors,
                AgentGraphValidationCodes.MissingRequiredModel,
                "Agent realization requires a Model Asset reference.",
                "$.options.model");
        }
        else
        {
            await ValidateReferenceAsync(
                definition.Options.Model,
                AssetType.Model,
                "$.options.model",
                errors,
                cancellationToken);
        }

        if (definition.Options.Prompt is not null)
        {
            await ValidateReferenceAsync(
                definition.Options.Prompt,
                AssetType.Prompt,
                "$.options.prompt",
                errors,
                cancellationToken);
        }

        for (var index = 0; index < definition.Options.Knowledge.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ValidateReferenceAsync(
                definition.Options.Knowledge.ElementAt(index),
                AssetType.Knowledge,
                $"$.options.knowledge[{index}]",
                errors,
                cancellationToken);
        }

        for (var index = 0; index < definition.Options.Tools.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ValidateReferenceAsync(
                definition.Options.Tools.ElementAt(index),
                AssetType.Tool,
                $"$.options.tools[{index}]",
                errors,
                cancellationToken);
        }

        if (definition.Options.Memory is not null)
        {
            await ValidateReferenceAsync(
                definition.Options.Memory,
                AssetType.Memory,
                "$.options.memory",
                errors,
                cancellationToken);
        }

        for (var index = 0; index < definition.Options.Policies.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ValidateReferenceAsync(
                definition.Options.Policies.ElementAt(index),
                AssetType.Policy,
                $"$.options.policies[{index}]",
                errors,
                cancellationToken);
        }

        return new AgentGraphValidationResult(errors);
    }

    private async ValueTask ValidateReferenceAsync(
        AssetReference reference,
        AssetType expectedType,
        string path,
        ICollection<AgentGraphValidationError> errors,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(reference);

        if (reference.Type != expectedType)
        {
            AddError(
                errors,
                AgentGraphValidationCodes.InvalidReferenceType,
                $"Agent reference must target Asset type '{expectedType}'.",
                path);
            return;
        }

        var key = AssetDefinitionKey.From(reference);
        var asset = await catalog.FindAsync(key, cancellationToken);

        if (asset is null)
        {
            AddError(
                errors,
                AgentGraphValidationCodes.DefinitionUnavailable,
                $"Referenced Asset definition '{key.Type}/{key.Id}/{key.Version.Value}' is unavailable.",
                path);
            return;
        }

        var actualKey = AssetDefinitionKey.From(asset);
        if (actualKey != key)
        {
            AddError(
                errors,
                AgentGraphValidationCodes.CatalogDefinitionKeyMismatch,
                "Asset catalog returned a definition inconsistent with the requested definition key.",
                path);
            return;
        }

        if (!string.Equals(asset.Urn.Value, reference.Urn.Value, StringComparison.Ordinal))
        {
            AddError(
                errors,
                AgentGraphValidationCodes.ReferenceUrnConflict,
                "Referenced Asset definition URN does not match the exact Agent reference.",
                path);
        }
    }

    private static void AddError(
        ICollection<AgentGraphValidationError> errors,
        string code,
        string message,
        string path)
    {
        errors.Add(new AgentGraphValidationError(code, message, path));
    }
}
