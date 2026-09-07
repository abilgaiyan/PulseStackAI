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
}
