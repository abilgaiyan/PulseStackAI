using PulseStack.Abstractions.Assets;

namespace PulseStack.Abstractions.Runtime.Realization.Application;

internal static class ApplicationRealizationContract
{
    public static void EnsureValidDefinitionKey(
        AssetDefinitionKey key,
        string parameterName)
    {
        if (!IsSupportedAssetType(key.Type))
        {
            throw new ArgumentException(
                "The definition key must identify a supported schema-v1 AI Asset type.",
                parameterName);
        }

        if (key.Id.IsEmpty)
        {
            throw new ArgumentException(
                "The definition key must contain a non-empty AssetId.",
                parameterName);
        }

        if (key.Version is null || string.IsNullOrWhiteSpace(key.Version.Value))
        {
            throw new ArgumentException(
                "The definition key must contain a non-empty AssetVersion.",
                parameterName);
        }
    }

    public static void EnsureValidReference(
        AssetReference? reference,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(reference, parameterName);
        EnsureValidDefinitionKey(AssetDefinitionKey.From(reference), parameterName);

        if (reference.Urn is null || string.IsNullOrWhiteSpace(reference.Urn.Value))
        {
            throw new ArgumentException(
                "The asset reference must contain a non-empty AssetUrn.",
                parameterName);
        }
    }

    private static bool IsSupportedAssetType(AssetType type) =>
        type is AssetType.Project
            or AssetType.Library
            or AssetType.Package
            or AssetType.Workflow
            or AssetType.Agent
            or AssetType.Prompt
            or AssetType.Tool
            or AssetType.Knowledge
            or AssetType.Memory
            or AssetType.Policy
            or AssetType.Model;
}
