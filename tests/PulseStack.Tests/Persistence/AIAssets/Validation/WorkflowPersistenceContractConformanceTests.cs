using FluentAssertions;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Abstractions.Persistence.AIAssets.Validation;
using PulseStack.Core.Persistence.AIAssets.Validation;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets.Validation;

public sealed class WorkflowPersistenceContractConformanceTests
{
    private readonly IAIAssetDocumentValidator validator = new AIAssetDocumentValidator();

    [Fact]
    public async Task ValidateAsync_ShouldAcceptCompleteDeclarativeWorkflowWithoutRuntimeResolution()
    {
        var agentA = AgentReference(1);
        var agentB = AgentReference(2);
        var agentC = AgentReference(3);

        var document = CreateWorkflow(
            [
                new ParallelStepDocument(
                    Id(1),
                    "parallel",
                    [
                        Run(2, agentA),
                        new ConditionalStepDocument(
                            Id(3),
                            "conditional",
                            new NamedConditionDocument("is-approved"),
                            new RetryStepDocument(
                                Id(4),
                                "retry",
                                Run(5, agentB),
                                3),
                            new LoopStepDocument(
                                Id(6),
                                "loop",
                                new InputValueDocument(),
                                new SwitchStepDocument(
                                    Id(7),
                                    "switch",
                                    new CurrentOutputValueDocument(),
                                    [new SwitchCaseDocument("approved", Run(8, agentC))],
                                    Run(9, agentA))))
                    ])
            ],
            [agentA, agentB, agentC]);

        var result = await validator.ValidateAsync(document);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_ShouldKeepReadinessOnlyEmptyStatesStructurallyValid()
    {
        var emptyWorkflow = CreateWorkflow([], []);
        var emptyCompositeWorkflow = CreateWorkflow(
            [
                new ParallelStepDocument(Id(1), "parallel", []),
                new SwitchStepDocument(
                    Id(2),
                    "switch",
                    new InputValueDocument(),
                    [])
            ],
            []);

        var emptyResult = await validator.ValidateAsync(emptyWorkflow);
        var compositeResult = await validator.ValidateAsync(emptyCompositeWorkflow);

        emptyResult.IsValid.Should().BeTrue();
        emptyResult.Errors.Should().BeEmpty();
        compositeResult.IsValid.Should().BeTrue();
        compositeResult.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_ShouldTreatNamedConditionsAndExactAgentReferencesAsStructuralSymbols()
    {
        var agent = AgentReference(42);
        var document = CreateWorkflow(
            [
                new ConditionalStepDocument(
                    Id(1),
                    "conditional",
                    new NamedConditionDocument("external-condition-not-resolved-here"),
                    Run(2, agent))
            ],
            [agent]);

        var result = await validator.ValidateAsync(document);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    private static WorkflowAssetDocument CreateWorkflow(
        IEnumerable<WorkflowStepDocument> steps,
        IEnumerable<AIAssetReferenceDocument> references)
        => new(
            AIAssetSchemaVersion.V1,
            new AIAssetIdentityDocument
            {
                Id = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                Urn = "urn:pulsestack:workflow:contract-conformance",
                Version = "1.0"
            },
            new AIAssetMetadataDocument("Workflow Contract Conformance"),
            AIAssetLifecycleDocument.Draft,
            steps,
            references);

    private static RunStepDocument Run(int id, AIAssetReferenceDocument agent)
        => new(Id(id), agent);

    private static AIAssetReferenceDocument AgentReference(int id)
        => new()
        {
            AssetType = AIAssetDocumentType.Agent,
            AssetId = Guid.Parse($"00000000-0000-0000-0000-{id:D12}").ToString("D"),
            Urn = $"urn:pulsestack:agent:{id}",
            Version = "1.0"
        };

    private static string Id(int value)
        => Guid.Parse($"10000000-0000-0000-0000-{value:D12}").ToString("D");
}
