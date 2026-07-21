using PulseStack.Abstractions.Persistence.Storage;
using PulseStack.Abstractions.Workflows;
using System.IO;

namespace PulseStack.Core.Persistence.Storage;

public sealed class FileWorkflowStore : IWorkflowStore
{
    private readonly DirectoryInfo _rootDirectory;

    public FileWorkflowStore(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        _rootDirectory = Directory.CreateDirectory(rootPath);
    }

    public async ValueTask SaveAsync(
        WorkflowId workflowId,
        Stream input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        cancellationToken.ThrowIfCancellationRequested();
        workflowId.EnsureValid();

        if (input.CanSeek)
        {
            input.Position = 0;
        }

        var file = GetFile(workflowId);

        await using var output = file.Create();

        await input.CopyToAsync(output, cancellationToken);
    }

    public async ValueTask<Stream?> LoadAsync(
        WorkflowId workflowId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        workflowId.EnsureValid();

        var file = GetFile(workflowId);

        if (!file.Exists)
        {
            return null;
        }

        await using var input = file.OpenRead();

        var memory = new MemoryStream();

        await input.CopyToAsync(memory, cancellationToken);

        memory.Position = 0;

        return memory;
    }

    public ValueTask DeleteAsync(
        WorkflowId workflowId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        workflowId.EnsureValid();

        var file = GetFile(workflowId);

        if (file.Exists)
        {
            file.Delete();
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> ExistsAsync(
        WorkflowId workflowId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        workflowId.EnsureValid();

        return ValueTask.FromResult(GetFile(workflowId).Exists);
    }

    private FileInfo GetFile(WorkflowId workflowId)
    {
        return new FileInfo(
            Path.Combine(
                _rootDirectory.FullName,
                $"{workflowId}.json"));
    }
}