using System.Collections.Concurrent;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Persistence.Storage;

namespace PulseStack.Core.Persistence.Storage;

public sealed class InMemoryWorkflowStore : IWorkflowStore
{
    private readonly ConcurrentDictionary<WorkflowId, byte[]> _workflows = new();

    public async ValueTask SaveAsync(
        WorkflowId workflowId,
        Stream input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        workflowId.EnsureValid();

        ArgumentNullException.ThrowIfNull(input);

        using var memory = new MemoryStream();

        await input.CopyToAsync(memory, cancellationToken);

        _workflows[workflowId] = memory.ToArray();
    }

    public ValueTask<Stream?> LoadAsync(
        WorkflowId workflowId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        workflowId.EnsureValid();

        if (!_workflows.TryGetValue(workflowId, out var bytes))
        {
                return ValueTask.FromResult<Stream?>(null);
        }

        return ValueTask.FromResult<Stream?>(
                new MemoryStream(bytes, writable: false));
    }

    public ValueTask DeleteAsync(
        WorkflowId workflowId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        workflowId.EnsureValid();

        _workflows.TryRemove(workflowId, out _);

        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> ExistsAsync(
        WorkflowId workflowId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        workflowId.EnsureValid();

        return ValueTask.FromResult(
            _workflows.TryGetValue(workflowId, out _));
    }
}