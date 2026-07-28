namespace PulseStack.Core.Persistence.Storage.Infrastructure;

internal sealed class FileStreamStore<TKey>
    where TKey : notnull
{
      private readonly DirectoryInfo _rootDirectory;
      private readonly Func<TKey, string> _pathResolver;

    public FileStreamStore(
        string rootPath,
        Func<TKey, string> pathResolver)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(pathResolver);

        _rootDirectory = Directory.CreateDirectory(rootPath);
        _pathResolver = pathResolver;
    }

    public async ValueTask SaveAsync(
        TKey key,
        Stream input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        cancellationToken.ThrowIfCancellationRequested();
        

        if (input.CanSeek)
        {
            input.Position = 0;
        }

        var file = ResolveFile(key);

        file.Directory?.Create();
        await using var output = new FileStream(
            file.FullName,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await input.CopyToAsync(output, cancellationToken);
    }

    public async ValueTask<Stream?> LoadAsync(
        TKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();
       
        var file = ResolveFile(key);

        if (!file.Exists)
        {
            return null;
        }

        await using var input = file.OpenRead();

        var memory = new MemoryStream();

        if (input.CanSeek)
        {
            input.Position = 0;
        }


        await input.CopyToAsync(memory, cancellationToken);

        memory.Position = 0;

        return memory;
    }

    public ValueTask DeleteAsync(
        TKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();
      
        var file = ResolveFile(key);

        if (file.Exists)
        {
            file.Delete();
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> ExistsAsync(
        TKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(ResolveFile(key).Exists);
    }

    private FileInfo ResolveFile(TKey key)
    {
        var relativePath = _pathResolver(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        
        return new FileInfo(
            Path.Combine(
                _rootDirectory.FullName,
                relativePath));
    }
}