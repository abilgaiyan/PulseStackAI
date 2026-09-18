using PulseStack.Abstractions.Assets;

namespace PulseStack.Core.Assets;

public sealed class WorkflowAssetFactory
{
    public WorkflowAsset Create(WorkflowAssetOptions options) =>
        Create(AssetId.New(), options);

    public WorkflowAsset Create(AssetId id, WorkflowAssetOptions options)
    {
        id.EnsureValid();
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Name);

        return new WorkflowAsset(
            id,
            new AssetUrn($"urn:pulsestack:workflow:{id}"),
            options);
    }

    public WorkflowAsset Create(IdentityCompleteWorkflowAssetOptions options) =>
        Create(AssetId.New(), options);

    public WorkflowAsset Create(
        AssetId id,
        IdentityCompleteWorkflowAssetOptions options)
    {
        id.EnsureValid();
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Name);
        ArgumentNullException.ThrowIfNull(options.Steps);

        var steps = options.Steps.ToArray();
        if (steps.Any(static step => step is null))
        {
            throw new ArgumentException(
                "Identity-complete workflow steps cannot contain null entries.",
                nameof(options));
        }

        return Create(
            id,
            new WorkflowAssetOptions
            {
                Name = options.Name,
                Description = options.Description,
                Steps = steps
                    .Select(static step => step.Definition)
                    .ToArray()
            });
    }
}
