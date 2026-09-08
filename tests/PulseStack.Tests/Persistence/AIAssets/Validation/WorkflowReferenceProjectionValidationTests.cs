using System.Collections;
using FluentAssertions;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Abstractions.Persistence.AIAssets.Validation;
using PulseStack.Core.Persistence.AIAssets.Validation;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets.Validation;

public sealed class WorkflowReferenceProjectionValidationTests
{
    private readonly IAIAssetDocumentValidator validator = new AIAssetDocumentValidator();

    [Fact]
    public async Task ValidateAsync_ShouldAcceptEmptyProjectionWhenWorkflowHasNoRunSteps()
    {
        var document = CreateWorkflow(
            [new ParallelStepDocument(Id(1), "parallel")],
            []);

        var result = await validator.ValidateAsync(document);

        ProjectionErrors(result).Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_ShouldDeduplicateRepeatedExactRunReferenceByDefinitionKey()
    {
        var agent = AgentReference(1, "1.0", "urn:pulsestack:agent:one");
        var document = CreateWorkflow(
            [Run(1, agent), Run(2, agent)],
            [agent]);

        var result = await validator.ValidateAsync(document);

        ProjectionErrors(result).Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_ShouldReportLaterConflictingUrnAndRetainFirstProjection()
    {
        var first = AgentReference(1, "1.0", "urn:pulsestack:agent:one");
        var conflicting = AgentReference(1, "1.0", "urn:pulsestack:agent:other");
        var document = CreateWorkflow(
            [Run(1, first), Run(2, conflicting)],
            [first]);

        var result = await validator.ValidateAsync(document);

        ProjectionErrors(result).Should().Equal(
            Error(
                AIAssetDocumentValidationCodes.ConflictingRunReferenceUrn,
                "$.steps[1].agent.urn"));
    }

    [Fact]
    public async Task ValidateAsync_ShouldTreatDifferentVersionsAsDistinctProjectedReferences()
    {
        var v1 = AgentReference(1, "1.0", "urn:pulsestack:agent:one:1.0");
        var v2 = AgentReference(1, "2.0", "urn:pulsestack:agent:one:2.0");
        var document = CreateWorkflow(
            [Run(1, v1), Run(2, v2)],
            [v1, v2]);

        var result = await validator.ValidateAsync(document);

        ProjectionErrors(result).Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_ShouldRequireExactEnvelopeSequence()
    {
        var first = AgentReference(1, "1.0", "urn:pulsestack:agent:one");
        var second = AgentReference(2, "1.0", "urn:pulsestack:agent:two");
        var document = CreateWorkflow(
            [Run(1, first), Run(2, second)],
            [second, first]);

        var result = await validator.ValidateAsync(document);

        ProjectionErrors(result).Should().Equal(
            Error(
                AIAssetDocumentValidationCodes.WorkflowReferenceProjectionMismatch,
                "$.references"));
    }

    [Fact]
    public async Task ValidateAsync_ShouldReportMissingEnvelopeReference()
    {
        var first = AgentReference(1, "1.0", "urn:pulsestack:agent:one");
        var second = AgentReference(2, "1.0", "urn:pulsestack:agent:two");
        var document = CreateWorkflow(
            [Run(1, first), Run(2, second)],
            [first]);

        var result = await validator.ValidateAsync(document);

        ProjectionErrors(result).Should().Equal(
            Error(AIAssetDocumentValidationCodes.WorkflowReferenceProjectionMismatch, "$.references"));
    }

    [Fact]
    public async Task ValidateAsync_ShouldReportExtraEnvelopeReference()
    {
        var projected = AgentReference(1, "1.0", "urn:pulsestack:agent:one");
        var extra = AgentReference(2, "1.0", "urn:pulsestack:agent:two");
        var document = CreateWorkflow(
            [Run(1, projected)],
            [projected, extra]);

        var result = await validator.ValidateAsync(document);

        ProjectionErrors(result).Should().Equal(
            Error(AIAssetDocumentValidationCodes.WorkflowReferenceProjectionMismatch, "$.references"));
    }

    [Fact]
    public async Task ValidateAsync_ShouldReportDuplicateEnvelopeReference()
    {
        var agent = AgentReference(1, "1.0", "urn:pulsestack:agent:one");
        var document = CreateWorkflow(
            [Run(1, agent)],
            [agent, agent]);

        var result = await validator.ValidateAsync(document);

        result.Errors.Should().Contain(error =>
            error.Code == AIAssetDocumentValidationCodes.DuplicateReference
            && error.Path == "$.references[1]");
        ProjectionErrors(result).Should().Equal(
            Error(AIAssetDocumentValidationCodes.WorkflowReferenceProjectionMismatch, "$.references"));
    }

    [Fact]
    public async Task ValidateAsync_ShouldReportWrongEnvelopeUrn()
    {
        var projected = AgentReference(1, "1.0", "urn:pulsestack:agent:one");
        var wrongUrn = projected with { Urn = "urn:pulsestack:agent:wrong" };
        var document = CreateWorkflow(
            [Run(1, projected)],
            [wrongUrn]);

        var result = await validator.ValidateAsync(document);

        ProjectionErrors(result).Should().Equal(
            Error(AIAssetDocumentValidationCodes.WorkflowReferenceProjectionMismatch, "$.references"));
    }

    [Fact]
    public async Task ValidateAsync_ShouldReportWrongEnvelopeVersion()
    {
        var projected = AgentReference(1, "1.0", "urn:pulsestack:agent:one");
        var wrongVersion = projected with { Version = "2.0" };
        var document = CreateWorkflow(
            [Run(1, projected)],
            [wrongVersion]);

        var result = await validator.ValidateAsync(document);

        ProjectionErrors(result).Should().Equal(
            Error(AIAssetDocumentValidationCodes.WorkflowReferenceProjectionMismatch, "$.references"));
    }

    [Fact]
    public async Task ValidateAsync_ShouldPreserveNestedAuthoredRunOrder()
    {
        var first = AgentReference(1, "1.0", "urn:pulsestack:agent:one");
        var second = AgentReference(2, "1.0", "urn:pulsestack:agent:two");
        var third = AgentReference(3, "1.0", "urn:pulsestack:agent:three");

        var nested = new ParallelStepDocument(
            Id(1),
            "parallel",
            [
                Run(2, first),
                new ConditionalStepDocument(
                    Id(3),
                    "conditional",
                    new NamedConditionDocument("condition"),
                    Run(4, second),
                    Run(5, first)),
                Run(6, third)
            ]);

        var document = CreateWorkflow([nested], [first, second, third]);

        var result = await validator.ValidateAsync(document);

        ProjectionErrors(result).Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_ShouldExcludeMalformedRunReferenceFromProjection()
    {
        var malformed = AgentReference(1, "1.0", "urn:pulsestack:agent:one") with
        {
            AssetId = Guid.Empty.ToString("D")
        };
        var document = CreateWorkflow([Run(1, malformed)], []);

        var result = await validator.ValidateAsync(document);

        result.Errors.Should().ContainSingle(error =>
            error.Code == AIAssetDocumentValidationCodes.InvalidRunAgentReference
            && error.Path == "$.steps[0].agent");
        ProjectionErrors(result).Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_ShouldExcludeMissingRunReferenceFromProjection()
    {
        var document = CreateWorkflow(
            [new RunStepDocument(Id(1), null!)],
            []);

        var result = await validator.ValidateAsync(document);

        result.Errors.Should().ContainSingle(error =>
            error.Code == AIAssetDocumentValidationCodes.MissingRunAgentReference
            && error.Path == "$.steps[0].agent");
        ProjectionErrors(result).Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_ShouldExcludeWrongTypeRunReferenceFromProjection()
    {
        var wrongType = AgentReference(1, "1.0", "urn:pulsestack:prompt:one") with
        {
            AssetType = AIAssetDocumentType.Prompt
        };
        var document = CreateWorkflow(
            [Run(1, wrongType)],
            []);

        var result = await validator.ValidateAsync(document);

        result.Errors.Should().ContainSingle(error =>
            error.Code == AIAssetDocumentValidationCodes.InvalidRunAgentReferenceType
            && error.Path == "$.steps[0].agent");
        ProjectionErrors(result).Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_ShouldReportNestedConflictingUrnAtLaterRunPath()
    {
        var first = AgentReference(1, "1.0", "urn:pulsestack:agent:one");
        var conflicting = AgentReference(1, "1.0", "urn:pulsestack:agent:other");
        var nested = new ParallelStepDocument(
            Id(1),
            "parallel",
            [
                Run(2, first),
                new ConditionalStepDocument(
                    Id(3),
                    "conditional",
                    new NamedConditionDocument("condition"),
                    Run(4, AgentReference(2, "1.0", "urn:pulsestack:agent:two")),
                    new RetryStepDocument(Id(5), "retry", Run(6, conflicting), 2))
            ]);
        var second = AgentReference(2, "1.0", "urn:pulsestack:agent:two");
        var document = CreateWorkflow([nested], [first, second]);

        var result = await validator.ValidateAsync(document);

        ProjectionErrors(result).Should().Equal(
            Error(
                AIAssetDocumentValidationCodes.ConflictingRunReferenceUrn,
                "$.steps[0].steps[1].elseStep.step.agent.urn"));
    }

    [Fact]
    public async Task ValidateAsync_ShouldReportCommonEnvelopeErrorAndProjectionMismatchIndependently()
    {
        var projected = AgentReference(1, "1.0", "urn:pulsestack:agent:one");
        var malformedEnvelope = projected with { AssetId = Guid.Empty.ToString("D") };
        var document = CreateWorkflow(
            [Run(1, projected)],
            [malformedEnvelope]);

        var result = await validator.ValidateAsync(document);

        result.Errors.Should().Contain(error =>
            error.Code == AIAssetDocumentValidationCodes.InvalidReferenceAssetId
            && error.Path == "$.references[0].assetId");
        ProjectionErrors(result).Should().Equal(
            Error(AIAssetDocumentValidationCodes.WorkflowReferenceProjectionMismatch, "$.references"));
    }

    [Fact]
    public async Task ValidateAsync_ShouldAggregateUrnConflictBeforeEnvelopeMismatch()
    {
        var first = AgentReference(1, "1.0", "urn:pulsestack:agent:one");
        var conflicting = AgentReference(1, "1.0", "urn:pulsestack:agent:other");
        var document = CreateWorkflow(
            [Run(1, first), Run(2, conflicting)],
            []);

        var result = await validator.ValidateAsync(document);

        ProjectionErrors(result).Should().Equal(
            Error(
                AIAssetDocumentValidationCodes.ConflictingRunReferenceUrn,
                "$.steps[1].agent.urn"),
            Error(
                AIAssetDocumentValidationCodes.WorkflowReferenceProjectionMismatch,
                "$.references"));
    }

    [Fact]
    public void Validate_ShouldHonorCancellationDuringProjectionTraversal()
    {
        using var source = new CancellationTokenSource();
        var errors = new CancellingErrorCollection(
            source,
            AIAssetDocumentValidationCodes.ConflictingRunReferenceUrn);
        var first = AgentReference(1, "1.0", "urn:pulsestack:agent:one");
        var conflicting = AgentReference(1, "1.0", "urn:pulsestack:agent:other");
        var later = AgentReference(2, "1.0", "urn:pulsestack:agent:two");
        var document = CreateWorkflow(
            [Run(1, first), Run(2, conflicting), Run(3, later)],
            [first, later]);

        var act = () => WorkflowDocumentStructuralValidator.Validate(
            document,
            errors,
            source.Token);

        act.Should().Throw<OperationCanceledException>();
        errors.Where(error => error.Code == AIAssetDocumentValidationCodes.ConflictingRunReferenceUrn)
            .Should().ContainSingle()
            .Which.Path.Should().Be("$.steps[1].agent.urn");
        errors.Should().NotContain(error =>
            error.Code == AIAssetDocumentValidationCodes.WorkflowReferenceProjectionMismatch);
    }

    private static IEnumerable<(string Code, string Path)> ProjectionErrors(
        AIAssetDocumentValidationResult result)
        => result.Errors
            .Where(error => error.Code is
                AIAssetDocumentValidationCodes.ConflictingRunReferenceUrn
                or AIAssetDocumentValidationCodes.WorkflowReferenceProjectionMismatch)
            .Select(error => (error.Code, error.Path));

    private static (string Code, string Path) Error(string code, string path)
        => (code, path);

    private static RunStepDocument Run(int id, AIAssetReferenceDocument agent)
        => new(Id(id), agent);

    private static AIAssetReferenceDocument AgentReference(
        int id,
        string version,
        string urn)
        => new()
        {
            AssetType = AIAssetDocumentType.Agent,
            AssetId = Guid.Parse($"00000000-0000-0000-0000-{id:D12}").ToString("D"),
            Urn = urn,
            Version = version
        };

    private static WorkflowAssetDocument CreateWorkflow(
        IEnumerable<WorkflowStepDocument> steps,
        IEnumerable<AIAssetReferenceDocument> references)
        => new(
            AIAssetSchemaVersion.V1,
            new AIAssetIdentityDocument
            {
                Id = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                Urn = "urn:pulsestack:workflow:reference-projection",
                Version = "1.0"
            },
            new AIAssetMetadataDocument("Workflow"),
            AIAssetLifecycleDocument.Draft,
            steps,
            references);

    private static string Id(int value)
        => Guid.Parse($"10000000-0000-0000-0000-{value:D12}").ToString("D");

    private sealed class CancellingErrorCollection : ICollection<AIAssetDocumentValidationError>
    {
        private readonly List<AIAssetDocumentValidationError> inner = [];
        private readonly CancellationTokenSource source;
        private readonly string cancelOnCode;

        public CancellingErrorCollection(
            CancellationTokenSource source,
            string cancelOnCode)
        {
            this.source = source;
            this.cancelOnCode = cancelOnCode;
        }

        public int Count => inner.Count;

        public bool IsReadOnly => false;

        public void Add(AIAssetDocumentValidationError item)
        {
            inner.Add(item);
            if (item.Code == cancelOnCode)
            {
                source.Cancel();
            }
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
