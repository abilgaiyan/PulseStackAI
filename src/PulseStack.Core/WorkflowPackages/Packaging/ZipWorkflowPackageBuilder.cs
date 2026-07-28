using System.IO.Compression;
using System.Text.Json;
using PulseStack.Abstractions.Persistence.Documents;
using PulseStack.Abstractions.Persistence.Mapping;
using PulseStack.Abstractions.Persistence.Serialization;
using PulseStack.Abstractions.Persistence.Validation;
using PulseStack.Abstractions.WorkflowPackages;
using PulseStack.Abstractions.WorkflowPackages.Contracts;

namespace PulseStack.Core.WorkflowPackages.Packaging;

public sealed class ZipWorkflowPackageBuilder : IWorkflowPackageBuilder
{
    private readonly IWorkflowValidator _validator;
    private readonly IWorkflowMapper _mapper;
    private readonly IWorkflowSerializer _serializer;

    public ZipWorkflowPackageBuilder(
        IWorkflowValidator validator,
        IWorkflowMapper mapper,
        IWorkflowSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(serializer);

        _validator = validator;
        _mapper = mapper;
        _serializer = serializer;
    }

    public async ValueTask<Stream> BuildAsync(
        WorkflowPackage package,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);

        cancellationToken.ThrowIfCancellationRequested();

        // Map workflow once
        var document = _mapper.ToDocument(package.Workflow);

        // Validate mapped document
        await ValidateWorkflowAsync(
            document,
            cancellationToken);

        // Build archive
        var stream = new MemoryStream();

        await CreatePackageAsync(
            package,
            document,
            stream,
            cancellationToken);

        stream.Position = 0;

        return stream;
    }

    private async ValueTask ValidateWorkflowAsync(
        WorkflowDocument document,
        CancellationToken cancellationToken)
    {
        var result = await _validator.ValidateAsync(
            document,
            cancellationToken);

        if (result.IsValid)
        {
            return;
        }

        var errors = string.Join(
            Environment.NewLine,
            result.Errors.Select(e => $"{e.Code}: {e.Message}"));

        throw new InvalidOperationException(
            $"Workflow validation failed.{Environment.NewLine}{errors}");
    }

   private async Task CreatePackageAsync(
        WorkflowPackage package,
        WorkflowDocument document,
        Stream output,
        CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(
            output,
            ZipArchiveMode.Create,
            leaveOpen: true);

        await WriteManifestAsync(
            archive,
            package,
            cancellationToken);

        await WriteWorkflowAsync(
            archive,
            document,
            cancellationToken);
    }

    private async Task WriteWorkflowAsync(
        ZipArchive archive,
        WorkflowDocument document,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(WorkflowPackageConstants.WorkflowEntry);

        await using var stream = entry.Open();

        await _serializer.SerializeAsync(
            document,
            stream,
            cancellationToken);
    }

    private static async Task WriteManifestAsync(
        ZipArchive archive,
        WorkflowPackage package,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(WorkflowPackageConstants.ManifestEntry);

        await using var stream = entry.Open();

        var manifest = CreateManifest(package);

        await JsonSerializer.SerializeAsync(
            stream,
            manifest,
            WorkflowPackageJsonOptions.Default,
            cancellationToken);
    }

   private static WorkflowPackageManifest CreateManifest(
        WorkflowPackage package)
    {
        return new()
        {
            PackageId = package.Identity.Id,
            PackageVersion = package.Identity.Version,
            Metadata = package.Metadata,
            PackageFormatVersion = WorkflowPackageConstants.PackageFormatVersion,
            MinimumRuntimeVersion = WorkflowPackageConstants.MinimumRuntimeVersion,
            BuiltAt = DateTimeOffset.UtcNow,
            EntryWorkflow = WorkflowPackageConstants.WorkflowEntry
        };
    }
}