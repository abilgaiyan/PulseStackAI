using System.Collections;
using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;
using PulseStack.Abstractions.Workflows.Definitions;
using PulseStack.Abstractions.Workflows.Values;
using PulseStack.Core.Assets;
using PulseStack.Core.Persistence.AIAssets.Mapping;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets.Mapping;

public sealed class WorkflowDefinitionDocumentMappingEdgeTests
{
    [Fact]
    public void ToDocument_ShouldMapInputValueDefinition()
    {
        var workflow = CreateWorkflow(
            new LoopStepDefinition
            {
                Name = "input-loop",
                Items = new InputValueDefinition(),
                Step = EmptyStep()
            });

        var document = new AIAssetDocumentMapper().ToDocument(workflow)
            .Should().BeOfType<WorkflowAssetDocument>().Subject;

        document.Steps[0]
            .Should().BeOfType<LoopStepDocument>().Subject.Items
            .Should().BeOfType<InputValueDocument>();
    }

    [Fact]
    public void ToDocument_ShouldRejectBlankMapKeyWithSemanticPath()
    {
        var act = () => MapLiteral(new BlankKeyReadOnlyMap());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*$.steps[0].items.literal*null, empty, or whitespace key*");
    }

    [Fact]
    public void ToDocument_ShouldRejectMapThatEmitsMoreThanAdvertisedCount()
    {
        var act = () => MapLiteral(new OverEmittingReadOnlyMap(1, 2));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*advertised Count 1 but emitted additional entries*");
    }

    [Fact]
    public void ToDocument_ShouldRejectZeroCountMapThatEmitsAnEntry()
    {
        var act = () => MapLiteral(new OverEmittingReadOnlyMap(0, 1));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*advertised Count 0 but emitted additional entries*");
    }

    [Fact]
    public void ToDocument_ShouldBoundEffectivelyUnboundedMapAtCountPlusOne()
    {
        var map = new EffectivelyUnboundedReadOnlyMap();

        var act = () => MapLiteral(map);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*advertised Count 1 but emitted additional entries*");
        map.MoveNextCount.Should().Be(2);
    }

    [Fact]
    public void ToDocument_ShouldRejectConflictingSupportedListElementContractsAtLiteralPath()
    {
        var act = () => MapLiteral(new ConflictingReadOnlyListContracts());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*$.steps[0].items.literal*conflicting generic element contracts*");
    }

    [Fact]
    public void ToDocument_ShouldRejectConflictingSupportedMapValueContractsAtLiteralPath()
    {
        var act = () => MapLiteral(new ConflictingReadOnlyMapContracts());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*$.steps[0].items.literal*conflicting generic value contracts*");
    }

    [Fact]
    public void ToDocument_ShouldAcceptEquivalentReadOnlyAndMutableListContracts()
    {
        var literal = MapLiteral(new EquivalentListContracts(1, 2))
            .Should().BeOfType<ArrayWorkflowLiteralDocument>().Subject;

        literal.Items.Should().HaveCount(2);
        literal.Items.Should().AllSatisfy(item =>
            item.Should().BeOfType<IntegerWorkflowLiteralDocument>());
    }

    private static WorkflowLiteralDocument MapLiteral(object? value)
    {
        var workflow = CreateWorkflow(
            new LoopStepDefinition
            {
                Name = "literal-loop",
                Items = new LiteralValueDefinition { Value = value },
                Step = EmptyStep()
            });

        var document = new AIAssetDocumentMapper().ToDocument(workflow)
            .Should().BeOfType<WorkflowAssetDocument>().Subject;

        return document.Steps[0]
            .Should().BeOfType<LoopStepDocument>().Subject.Items
            .Should().BeOfType<LiteralValueDocument>().Subject.Literal;
    }

    private static ParallelStepDefinition EmptyStep()
        => new()
        {
            Name = "empty",
            Steps = []
        };

    private static WorkflowAsset CreateWorkflow(params WorkflowStepDefinition[] steps)
        => new WorkflowAssetFactory().Create(
            new WorkflowAssetOptions
            {
                Name = "Workflow",
                Description = "Description",
                Steps = steps
            });

    private sealed class BlankKeyReadOnlyMap : IReadOnlyDictionary<string, object?>
    {
        public int Count => 1;
        public IEnumerable<string> Keys => [" "];
        public IEnumerable<object?> Values => [1];
        public object? this[string key] => 1;
        public bool ContainsKey(string key) => key == " ";
        public bool TryGetValue(string key, out object? value)
        {
            value = 1;
            return key == " ";
        }
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            yield return new KeyValuePair<string, object?>(" ", 1);
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class OverEmittingReadOnlyMap(int advertisedCount, int emittedCount)
        : IReadOnlyDictionary<string, object?>
    {
        public int Count => advertisedCount;
        public IEnumerable<string> Keys => Enumerable.Range(0, emittedCount).Select(i => $"k{i}");
        public IEnumerable<object?> Values => Enumerable.Range(0, emittedCount).Cast<object?>();
        public object? this[string key] => 0;
        public bool ContainsKey(string key) => true;
        public bool TryGetValue(string key, out object? value)
        {
            value = 0;
            return true;
        }
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            for (var index = 0; index < emittedCount; index++)
            {
                yield return new KeyValuePair<string, object?>($"k{index}", index);
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class EffectivelyUnboundedReadOnlyMap : IReadOnlyDictionary<string, object?>
    {
        public int MoveNextCount { get; private set; }
        public int Count => 1;
        public IEnumerable<string> Keys => throw new NotSupportedException();
        public IEnumerable<object?> Values => throw new NotSupportedException();
        public object? this[string key] => 0;
        public bool ContainsKey(string key) => true;
        public bool TryGetValue(string key, out object? value)
        {
            value = 0;
            return true;
        }
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
            => new UnboundedEnumerator(this);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private sealed class UnboundedEnumerator(EffectivelyUnboundedReadOnlyMap owner)
            : IEnumerator<KeyValuePair<string, object?>>
        {
            private int index = -1;
            public KeyValuePair<string, object?> Current
                => new($"k{index}", index);
            object IEnumerator.Current => Current;
            public bool MoveNext()
            {
                owner.MoveNextCount++;
                index++;
                return true;
            }
            public void Reset() => throw new NotSupportedException();
            public void Dispose() { }
        }
    }

    private sealed class ConflictingReadOnlyListContracts
        : IReadOnlyList<int>, IReadOnlyList<string>
    {
        int IReadOnlyCollection<int>.Count => 1;
        int IReadOnlyCollection<string>.Count => 1;
        int IReadOnlyList<int>.this[int index] => 1;
        string IReadOnlyList<string>.this[int index] => "one";
        IEnumerator<int> IEnumerable<int>.GetEnumerator()
            => new[] { 1 }.AsEnumerable().GetEnumerator();
        IEnumerator<string> IEnumerable<string>.GetEnumerator()
            => new[] { "one" }.AsEnumerable().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator()
            => ((IEnumerable<int>)this).GetEnumerator();
    }

    private sealed class ConflictingReadOnlyMapContracts
        : IReadOnlyDictionary<string, int>, IReadOnlyDictionary<string, string>
    {
        int IReadOnlyCollection<KeyValuePair<string, int>>.Count => 1;
        int IReadOnlyCollection<KeyValuePair<string, string>>.Count => 1;
        IEnumerable<string> IReadOnlyDictionary<string, int>.Keys => ["k"];
        IEnumerable<int> IReadOnlyDictionary<string, int>.Values => [1];
        IEnumerable<string> IReadOnlyDictionary<string, string>.Keys => ["k"];
        IEnumerable<string> IReadOnlyDictionary<string, string>.Values => ["one"];
        int IReadOnlyDictionary<string, int>.this[string key] => 1;
        string IReadOnlyDictionary<string, string>.this[string key] => "one";
        bool IReadOnlyDictionary<string, int>.ContainsKey(string key) => true;
        bool IReadOnlyDictionary<string, string>.ContainsKey(string key) => true;
        bool IReadOnlyDictionary<string, int>.TryGetValue(string key, out int value)
        {
            value = 1;
            return true;
        }
        bool IReadOnlyDictionary<string, string>.TryGetValue(string key, out string value)
        {
            value = "one";
            return true;
        }
        IEnumerator<KeyValuePair<string, int>> IEnumerable<KeyValuePair<string, int>>.GetEnumerator()
            => new[] { new KeyValuePair<string, int>("k", 1) }.AsEnumerable().GetEnumerator();
        IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator()
            => new[] { new KeyValuePair<string, string>("k", "one") }.AsEnumerable().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator()
            => ((IEnumerable<KeyValuePair<string, int>>)this).GetEnumerator();
    }

    private sealed class EquivalentListContracts(params object?[] items)
        : IReadOnlyList<object?>, IList<object?>
    {
        public int Count => items.Length;
        public bool IsReadOnly => true;
        public object? this[int index]
        {
            get => items[index];
            set => throw new NotSupportedException();
        }
        public int IndexOf(object? item) => Array.IndexOf(items, item);
        public bool Contains(object? item) => items.Contains(item);
        public void CopyTo(object?[] array, int arrayIndex) => items.CopyTo(array, arrayIndex);
        public IEnumerator<object?> GetEnumerator()
            => ((IEnumerable<object?>)items).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Add(object? item) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public void Insert(int index, object? item) => throw new NotSupportedException();
        public bool Remove(object? item) => throw new NotSupportedException();
        public void RemoveAt(int index) => throw new NotSupportedException();
    }
}
