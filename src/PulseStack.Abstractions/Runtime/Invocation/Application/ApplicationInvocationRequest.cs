using System.Collections.ObjectModel;

namespace PulseStack.Abstractions.Runtime.Invocation.Application;

public sealed class ApplicationInvocationRequest
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyItems =
        new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(StringComparer.Ordinal));

    public string Input { get; }

    public IReadOnlyDictionary<string, object?> Items { get; }

    public ApplicationInvocationRequest(
        string input,
        IReadOnlyDictionary<string, object?>? items = null)
    {
        ArgumentNullException.ThrowIfNull(input);

        Input = input;
        Items = items is null
            ? EmptyItems
            : SnapshotItems(items);
    }

    private static IReadOnlyDictionary<string, object?> SnapshotItems(
        IReadOnlyDictionary<string, object?> source)
    {
        var snapshot = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var item in source)
        {
            if (string.IsNullOrWhiteSpace(item.Key))
            {
                throw new ArgumentException(
                    "Invocation item keys must be non-empty and non-whitespace.",
                    nameof(source));
            }

            if (!snapshot.TryAdd(item.Key, item.Value))
            {
                throw new ArgumentException(
                    "Invocation item keys must be unique using ordinal comparison.",
                    nameof(source));
            }
        }

        return new ReadOnlyDictionary<string, object?>(snapshot);
    }
}
