using System.Text;
using PulseStack.Abstractions.Knowledge;

namespace PulseStack.Agents.Runtime;

internal static class KnowledgeContribution
{
    internal static int SnapshotLimit(KnowledgeExecutionOptions? options)
    {
        var limit = (options ?? new KnowledgeExecutionOptions()).MaxContributionBytes;
        if (limit <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Knowledge contribution limit must be positive.");
        return limit;
    }

    internal static async Task<string?> RetrieveAsync(
        IReadOnlyCollection<IKnowledgeSource> sources,
        string input,
        int limit,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var query = new KnowledgeQuery { Text = input };
        var builder = new StringBuilder();
        var bytes = 0;

        void Append(string text)
        {
            var count = Encoding.UTF8.GetByteCount(text);
            if (count > limit - bytes)
                throw new InvalidOperationException("Retrieved Knowledge exceeds the configured contribution byte limit.");
            bytes += count;
            builder.Append(text);
        }

        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = source.Name;
            var result = await source.RetrieveAsync(query, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (result?.Items is null)
                throw new InvalidOperationException($"Knowledge source '{name}' returned a null result or Items.");

            var items = result.Items.ToArray();
            if (items.Any(string.IsNullOrWhiteSpace))
                throw new InvalidOperationException($"Knowledge source '{name}' returned an empty or null item.");
            if (items.Length == 0)
                continue;

            if (builder.Length == 0)
                Append("Retrieved reference material\n");
            Append("\nSource: ");
            Append(name);
            Append("\n");
            for (var i = 0; i < items.Length; i++)
            {
                if (i > 0) Append("\n\n");
                Append(items[i]);
            }
            Append("\n");
        }

        return builder.Length == 0 ? null : builder.ToString();
    }
}
