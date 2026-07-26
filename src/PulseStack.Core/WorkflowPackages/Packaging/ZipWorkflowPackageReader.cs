using System.IO.Compression;
using System.Text.Json;
using PulseStack.Abstractions.Persistence.Mapping;
using PulseStack.Abstractions.Persistence.Serialization;
using PulseStack.Abstractions.Persistence.Documents;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.WorkflowPackages;
using PulseStack.Abstractions.WorkflowPackages.Contracts;
using PulseStack.Abstractions.WorkflowPackages.Identity;

namespace PulseStack.Core.WorkflowPackages.Packaging;

public sealed class ZipWorkflowPackageReader : IWorkflowPackageReader
{
    private readonly IWorkflowMapper _mapper;
    private readonly IWorkflowDeserializer _deserializer;
    private readonly IAgentResolver _agentResolver;

    public ZipWorkflowPackageReader(
        IWorkflowMapper mapper,
        IWorkflowDeserializer deserializer,
        IAgentResolver agentResolver)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(deserializer);
        ArgumentNullException.ThrowIfNull(agentResolver);

        _mapper = mapper;
        _deserializer = deserializer;
        _agentResolver = agentResolver;
    }

    public async ValueTask<WorkflowPackage> ReadAsync(
        Stream input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        cancellationToken.ThrowIfCancellationRequested();

        using var archive = new ZipArchive(
            input,
            ZipArchiveMode.Read,
            leaveOpen: true);

        var manifest = await ReadManifestAsync(
            archive,
            cancellationToken);

        var document = await ReadWorkflowAsync(
            archive,
            cancellationToken);

        var workflow = _mapper.FromDocument(
            document,
            _agentResolver);

        return CreatePackage(
            manifest,
            workflow);
    }

    private static async Task<WorkflowPackageManifest> ReadManifestAsync(
        ZipArchive archive,
        CancellationToken cancellationToken)
    {
        var entry = archive.GetEntry(WorkflowPackageConstants.ManifestEntry);

        if (entry is null)
        {
            throw new InvalidOperationException(
                "Package does not contain manifest.json.");
        }

        await using var stream = entry.Open();

        var manifest =
            await JsonSerializer.DeserializeAsync<WorkflowPackageManifest>(
                stream,
                WorkflowPackageJsonOptions.Default,
                cancellationToken);

        return manifest
            ?? throw new InvalidOperationException(
                "Failed to deserialize package manifest.");
    }

    private async Task<WorkflowDocument> ReadWorkflowAsync(
        ZipArchive archive,
        CancellationToken cancellationToken)
    {
        var entry = archive.GetEntry(WorkflowPackageConstants.WorkflowEntry);

        if (entry is null)
        {
            throw new InvalidOperationException(
                "Package does not contain workflow.json.");
        }

        await using var stream = entry.Open();

        var document =
            await _deserializer.DeserializeAsync(
                stream,
                cancellationToken);

        return document;
    }

    private static WorkflowPackage CreatePackage(
        WorkflowPackageManifest manifest,
        Workflow workflow)
    {
        return new WorkflowPackage
        {
            Identity = new WorkflowPackageIdentity(
                manifest.PackageId,
                manifest.PackageVersion),

            Metadata = new WorkflowPackageMetadata(),

            Workflow = workflow
        };
    }

    private static WorkflowPackageManifest CreateManifest(
        WorkflowPackage package)
    {
        return new()
        {
            PackageId = package.Identity.Id,
            PackageVersion = package.Identity.Version,
            PackageFormatVersion = WorkflowPackageConstants.PackageFormatVersion,
            MinimumRuntimeVersion = WorkflowPackageConstants.MinimumRuntimeVersion,
            CreatedAt = DateTimeOffset.UtcNow,
            EntryWorkflow = WorkflowPackageConstants.WorkflowEntry
        };
    }

}