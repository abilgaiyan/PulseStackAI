using System.Collections.Concurrent;

namespace PulseStack.Core.Persistence.Storage.Infrastructure;

internal sealed class InMemoryStreamStore<TKey>
    where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, byte[]> _storage = new();

    public async ValueTask SaveAsync(
        TKey key,
        Stream input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        using var memory = new MemoryStream();

        if (input.CanSeek)
        {
            input.Position = 0;
        }
        
        await input.CopyToAsync(memory, cancellationToken);

        _storage[key] = memory.ToArray();
    }

    public ValueTask<Stream?> LoadAsync(
        TKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_storage.TryGetValue(key, out var bytes))
        {
                return ValueTask.FromResult<Stream?>(null);
        }

        return ValueTask.FromResult<Stream?>(
                new MemoryStream(bytes, writable: false));
    }

    public ValueTask DeleteAsync(
        TKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();
        _storage.TryRemove(key, out _);

        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> ExistsAsync(
        TKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(
            _storage.ContainsKey(key));
    }
}