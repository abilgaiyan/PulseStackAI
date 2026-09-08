using System.Collections;
using FluentAssertions;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Abstractions.Persistence.AIAssets.Validation;
using PulseStack.Core.Persistence.AIAssets.Validation;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets.Validation;

public sealed class WorkflowLiteralCancellationValidationTests
{
    [Fact]
    public void StructuralValidator_ShouldHonorCancellationBetweenNestedArrayItems()
    {
        using var source = new CancellationTokenSource();
        var errors = new CancellingErrorCollection(source);
        var document = CreateWorkflow(
            LiteralLoop(
                1,
                new ArrayWorkflowLiteralDocument(
                    [
                        new StringWorkflowLiteralDocument(null!),
                        new StringWorkflowLiteralDocument(null!)
                    ])));

        Action act = () => WorkflowDocumentStructuralValidator.Validate(
            document,
            errors,
            source.Token);

        act.Should().Throw<OperationCanceledException>();
        errors.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new AIAssetDocumentValidationError(
                AIAssetDocumentValidationCodes.MissingWorkflowStringLiteralValue,
                "Workflow string literal value is required.",
                "$.steps[0].items.literal.items[0].value"));
    }

    [Fact]
    public void StructuralValidator_ShouldHonorCancellationBetweenNestedObjectProperties()
    {
        using var source = new CancellationTokenSource();
        var errors = new CancellingErrorCollection(source);
        var document = CreateWorkflow(
            LiteralLoop(
                1,
                new ObjectWorkflowLiteralDocument(
                    [
                        new WorkflowLiteralPropertyDocument(
                            "a",
                            new StringWorkflowLiteralDocument(null!)),
                        new WorkflowLiteralPropertyDocument(
                            "b",
                            new StringWorkflowLiteralDocument(null!))
                    ])));

        Action act = () => WorkflowDocumentStructuralValidator.Validate(
            document,
            errors,
            source.Token);

        act.Should().Throw<OperationCanceledException>();
        errors.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new AIAssetDocumentValidationError(
                AIAssetDocumentValidationCodes.MissingWorkflowStringLiteralValue,
                "Workflow string literal value is required.",
                "$.steps[0].items.literal.properties[0].value.value"));
    }

    private static LoopStepDocument LiteralLoop(int id, WorkflowLiteralDocument literal)
        => new(Id(id), $"loop-{id}", new LiteralValueDocument(literal), Run(id + 100));

    private static RunStepDocument Run(int id)
        => new(Id(id), new AIAssetReferenceDocument
        {
            AssetType = AIAssetDocumentType.Agent,
            AssetId = Guid.Parse($"00000000-0000-0000-0000-{id:D12}").ToString(),
            Urn = $"urn:pulsestack:agent:{id}",
            Version = "1.0"
        });

    private static WorkflowAssetDocument CreateWorkflow(params WorkflowStepDocument[] steps)
        => new(
            AIAssetSchemaVersion.V1,
            new AIAssetIdentityDocument
            {
                Id = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                Urn = "urn:pulsestack:workflow:literal-cancellation",
                Version = "1.0"
            },
            new AIAssetMetadataDocument("Workflow"),
            AIAssetLifecycleDocument.Draft,
            steps);

    private static string Id(int value)
        => Guid.Parse($"10000000-0000-0000-0000-{value:D12}").ToString("D");

    private sealed class CancellingErrorCollection : ICollection<AIAssetDocumentValidationError>
    {
        private readonly List<AIAssetDocumentValidationError> inner = [];
        private readonly CancellationTokenSource source;

        public CancellingErrorCollection(CancellationTokenSource source)
        {
            this.source = source;
        }

        public int Count => inner.Count;

        public bool IsReadOnly => false;

        public void Add(AIAssetDocumentValidationError item)
        {
            inner.Add(item);
            source.Cancel();
        }

        public void Clear() => inner.Clear();

        public bool Contains(AIAssetDocumentValidationError item) => inner.Contains(item);

        public void CopyTo(AIAssetDocumentValidationError[] array, int arrayIndex)
            => inner.CopyTo(array, arrayIndex);

        public IEnumerator<AIAssetDocumentValidationError> GetEnumerator()
            => inner.GetEnumerator();

        public bool Remove(AIAssetDocumentValidationError item)
            => inner.Remove(item);

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();
    }
}
