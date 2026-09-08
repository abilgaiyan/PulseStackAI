using System.Collections;

namespace PulseStack.Abstractions.Persistence.AIAssets.Documents;

internal sealed class StructuralReadOnlyList<T> : IReadOnlyList<T>, IEquatable<StructuralReadOnlyList<T>>
{
    private readonly T[] items;

    public StructuralReadOnlyList(IEnumerable<T>? items = null)
    {
        this.items = items?.ToArray() ?? Array.Empty<T>();
    }

    public int Count => items.Length;

    public T this[int index] => items[index];

    public bool Equals(StructuralReadOnlyList<T>? other)
    {
        return other is not null && items.SequenceEqual(other.items);
    }

    public override bool Equals(object? obj)
    {
        return obj is StructuralReadOnlyList<T> other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();

        foreach (var item in items)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }

    public IEnumerator<T> GetEnumerator()
    {
        return ((IEnumerable<T>)items).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return items.GetEnumerator();
    }
}
