using System.Reflection;
using FluentAssertions;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Abstractions.Persistence.AIAssets.Validation;
using PulseStack.Core.Persistence.AIAssets.Validation;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets.Validation;

public sealed class WorkflowAssetDocumentStructuralValidationTests
{
    private readonly IAIAssetDocumentValidator validator = new AIAssetDocumentValidator();

    [Fact]
    public async Task ValidateAsync_ShouldExposeWorkflowValidationThroughPublicBoundary()
    {
        var document = CreateWorkflow(new ParallelStepDocument(Id(1), " ", [null!]));

        var result = await validator.ValidateAsync(document);

        result.Errors.Should().ContainEquivalentOf(Error(
            AIAssetDocumentValidationCodes.MissingWorkflowStepName,
            "Workflow step name is required.",
            "$.steps[0].name"));
        result.Errors.Should().ContainEquivalentOf(Error(
            AIAssetDocumentValidationCodes.MissingWorkflowStep,
            "Workflow step is required.",
            "$.steps[0].steps[0]"));
        result.Errors.Should().NotContain(error =>
            error.Code == AIAssetDocumentValidationCodes.AssetTypeMismatch);
    }

    [Fact]
    public async Task ValidateAsync_ShouldCoverStepDiagnostics_AD740_Through_AD820()
    {
        var malformed = new AIAssetReferenceDocument
        {
            AssetType = AIAssetDocumentType.Agent,
            AssetId = Guid.Empty.ToString(),
            Urn = "urn:agent:malformed",
            Version = "1.0"
        };
        var wrongType = Reference(AIAssetDocumentType.Prompt, 9);
        var missingRun = new RunStepDocument(Id(6), null!);
        var invalidRun = new RunStepDocument(Id(7), malformed);
        var wrongTypeRun = new RunStepDocument(Id(8), wrongType);
        var unsupported = new UnsupportedStepDocument(new RunStepDocument(Id(2), AgentReference(2)));
        var mismatch = new RunStepDocument(Id(3), AgentReference(3));
        SetKind(mismatch, WorkflowStepDocumentKind.Parallel);

        var document = CreateWorkflow(
            new ParallelStepDocument(Id(1), "parallel", [null!]),
            unsupported,
            mismatch,
            new RunStepDocument("NOT-A-GUID", AgentReference(4)),
            new RunStepDocument(Id(5), AgentReference(5)),
            new RunStepDocument(Id(5), AgentReference(5)),
            missingRun,
            invalidRun,
            wrongTypeRun);

        var result = await validator.ValidateAsync(document);

        result.Errors.Should().Contain(error => error.Code == AIAssetDocumentValidationCodes.MissingWorkflowStep && error.Path == "$.steps[0].steps[0]");
        result.Errors.Should().Contain(error => error.Code == AIAssetDocumentValidationCodes.UnsupportedWorkflowStep && error.Path == "$.steps[1]");
        result.Errors.Should().Contain(error => error.Code == AIAssetDocumentValidationCodes.WorkflowStepTypeMismatch && error.Path == "$.steps[2].kind");
        result.Errors.Should().Contain(error => error.Code == AIAssetDocumentValidationCodes.InvalidWorkflowStepId && error.Path == "$.steps[3].stepId");
        result.Errors.Should().Contain(error => error.Code == AIAssetDocumentValidationCodes.DuplicateWorkflowStepId && error.Path == "$.steps[5].stepId");
        result.Errors.Should().Contain(error => error.Code == AIAssetDocumentValidationCodes.MissingRunAgentReference && error.Path == "$.steps[6].agent");
        result.Errors.Should().Contain(error => error.Code == AIAssetDocumentValidationCodes.InvalidRunAgentReference && error.Path == "$.steps[7].agent");
        result.Errors.Should().Contain(error => error.Code == AIAssetDocumentValidationCodes.InvalidRunAgentReferenceType && error.Path == "$.steps[8].agent");

        result.Errors.Where(error => error.Path == "$.steps[6].agent").Select(error => error.Code)
            .Should().Equal(AIAssetDocumentValidationCodes.MissingRunAgentReference);
        result.Errors.Where(error => error.Path == "$.steps[7].agent").Select(error => error.Code)
            .Should().Equal(AIAssetDocumentValidationCodes.InvalidRunAgentReference);
        result.Errors.Where(error => error.Path == "$.steps[8].agent").Select(error => error.Code)
            .Should().Equal(AIAssetDocumentValidationCodes.InvalidRunAgentReferenceType);
    }

    [Fact]
    public async Task ValidateAsync_ShouldCoverConditionRetryAndSwitchDiagnostics_AD830_Through_AD900()
    {
        var unsupportedCondition = new UnsupportedConditionDocument(new NamedConditionDocument("ok"));
        var mismatchedCondition = new NamedConditionDocument("ok");
        SetKind(mismatchedCondition, (WorkflowConditionDocumentKind)999);

        var document = CreateWorkflow(
            new ConditionalStepDocument(Id(1), "conditional", null!, Run(2)),
            new ConditionalStepDocument(Id(3), "conditional", unsupportedCondition, Run(4)),
            new ConditionalStepDocument(Id(5), "conditional", mismatchedCondition, Run(6)),
            new ConditionalStepDocument(Id(7), "conditional", new NamedConditionDocument(" "), Run(8)),
            new RetryStepDocument(Id(9), "retry", Run(10), 0),
            new SwitchStepDocument(
                Id(11),
                "switch",
                new InputValueDocument(),
                [
                    null!,
                    new SwitchCaseDocument(" ", Run(12)),
                    new SwitchCaseDocument("Approve", Run(13)),
                    new SwitchCaseDocument("approve", Run(14))
                ]));

        var result = await validator.ValidateAsync(document);

        result.Errors.Should().Contain(error => error.Code == AIAssetDocumentValidationCodes.MissingWorkflowCondition && error.Path == "$.steps[0].condition");
        result.Errors.Should().Contain(error => error.Code == AIAssetDocumentValidationCodes.UnsupportedWorkflowCondition && error.Path == "$.steps[1].condition");
        result.Errors.Should().Contain(error => error.Code == AIAssetDocumentValidationCodes.WorkflowConditionTypeMismatch && error.Path == "$.steps[2].condition.kind");
        result.Errors.Should().Contain(error => error.Code == AIAssetDocumentValidationCodes.MissingNamedConditionName && error.Path == "$.steps[3].condition.name");
        result.Errors.Should().Contain(error => error.Code == AIAssetDocumentValidationCodes.InvalidRetryMaxAttempts && error.Path == "$.steps[4].maxAttempts");
        result.Errors.Should().Contain(error => error.Code == AIAssetDocumentValidationCodes.MissingSwitchCase && error.Path == "$.steps[5].cases[0]");
        result.Errors.Should().Contain(error => error.Code == AIAssetDocumentValidationCodes.InvalidSwitchCaseValue && error.Path == "$.steps[5].cases[1].value");
        result.Errors.Should().Contain(error => error.Code == AIAssetDocumentValidationCodes.DuplicateSwitchCaseValue && error.Path == "$.steps[5].cases[3].value");
    }

    [Fact]
    public async Task ValidateAsync_ShouldCoverValueDiagnostics_AD910_Through_AD950()
    {
        var unsupportedValue = new UnsupportedValueDocument(new InputValueDocument());
        var mismatchedValue = new ContextItemValueDocument("key");
        SetKind(mismatchedValue, WorkflowValueDocumentKind.Input);

        var document = CreateWorkflow(
            new LoopStepDocument(Id(1), "loop", null!, Run(2)),
            new LoopStepDocument(Id(3), "loop", unsupportedValue, Run(4)),
            new LoopStepDocument(Id(5), "loop", mismatchedValue, Run(6)),
            new LoopStepDocument(Id(7), "loop", new ContextItemValueDocument(" "), Run(8)),
            new LoopStepDocument(Id(9), "loop", new LiteralValueDocument(null!), Run(10)));

        var result = await validator.ValidateAsync(document);

        result.Errors.Should().Contain(error => error.Code == AIAssetDocumentValidationCodes.MissingWorkflowValue && error.Path == "$.steps[0].items");
        result.Errors.Should().Contain(error => error.Code == AIAssetDocumentValidationCodes.UnsupportedWorkflowValue && error.Path == "$.steps[1].items");
        result.Errors.Should().Contain(error => error.Code == AIAssetDocumentValidationCodes.WorkflowValueTypeMismatch && error.Path == "$.steps[2].items.kind");
        result.Errors.Should().Contain(error => error.Code == AIAssetDocumentValidationCodes.MissingContextItemKey && error.Path == "$.steps[3].items.key");
        result.Errors.Should().Contain(error => error.Code == AIAssetDocumentValidationCodes.MissingWorkflowLiteral && error.Path == "$.steps[4].items.literal");
    }

    [Fact]
    public async Task ValidateAsync_ShouldPreserveDeepPathsAndDeterministicAggregationOrder()
    {
        var duplicateId = Id(7);
        var document = CreateWorkflow(
            new ParallelStepDocument(
                Id(1),
                "parallel",
                [
                    new ConditionalStepDocument(
                        Id(2),
                        "conditional",
                        new NamedConditionDocument("condition"),
                        new RetryStepDocument(
                            Id(3),
                            "retry",
                            new LoopStepDocument(
                                Id(4),
                                "loop",
                                new ContextItemValueDocument(" "),
                                new SwitchStepDocument(
                                    Id(5),
                                    "switch",
                                    new LiteralValueDocument(
                                        new ObjectWorkflowLiteralDocument(
                                            [new WorkflowLiteralPropertyDocument(
                                                "payload",
                                                new ArrayWorkflowLiteralDocument(
                                                    [new StringWorkflowLiteralDocument("ok")]))])),
                                    [new SwitchCaseDocument("A", new RunStepDocument(duplicateId, null!))],
                                    new RunStepDocument(duplicateId, AgentReference(8)))),
                            0),
                        elseStep: null)
                ]));

        var result = await validator.ValidateAsync(document);

        var workflowErrors = result.Errors
            .Where(error => error.Code.StartsWith("AD", StringComparison.Ordinal)
                && int.TryParse(error.Code.AsSpan(2), out var number)
                && number >= 740)
            .ToArray();

        workflowErrors.Select(error => (error.Code, error.Path)).Should().Equal(
            (AIAssetDocumentValidationCodes.InvalidRetryMaxAttempts, "$.steps[0].steps[0].thenStep.maxAttempts"),
            (AIAssetDocumentValidationCodes.MissingContextItemKey, "$.steps[0].steps[0].thenStep.step.items.key"),
            (AIAssetDocumentValidationCodes.MissingRunAgentReference, "$.steps[0].steps[0].thenStep.step.step.cases[0].step.agent"),
            (AIAssetDocumentValidationCodes.DuplicateWorkflowStepId, "$.steps[0].steps[0].thenStep.step.step.defaultStep.stepId"));
    }

    [Fact]
    public async Task ValidateAsync_ShouldAllowReadinessOnlyEmptyStates()
    {
        var document = CreateWorkflow(
            new ParallelStepDocument(Id(1), "parallel", []),
            new SwitchStepDocument(Id(2), "switch", new InputValueDocument(), []));

        var result = await validator.ValidateAsync(document);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_ShouldHonorPreCancelledToken()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        var act = () => validator.ValidateAsync(CreateWorkflow(Run(1)), source.Token).AsTask();

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ValidateAsync_ShouldHonorCancellationAtTraversalBoundary()
    {
        using var source = new CancellationTokenSource();
        var document = CreateWorkflow(
            new ParallelStepDocument(
                Id(1),
                "parallel",
                new CancellingStepList(source, Run(2), Run(3))));

        var act = () => validator.ValidateAsync(document, source.Token).AsTask();

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static WorkflowAssetDocument CreateWorkflow(params WorkflowStepDocument[] steps)
        => new(
            AIAssetSchemaVersion.V1,
            new AIAssetIdentityDocument(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa").ToString(),
                "urn:pulsestack:workflow:test",
                "1.0"),
            new AIAssetMetadataDocument("Workflow"),
            AIAssetLifecycleDocument.Draft,
            steps);

    private static RunStepDocument Run(int id)
        => new(Id(id), AgentReference(id));

    private static AIAssetReferenceDocument AgentReference(int id)
        => Reference(AIAssetDocumentType.Agent, id);

    private static AIAssetReferenceDocument Reference(AIAssetDocumentType type, int id)
        => new()
        {
            AssetType = type,
            AssetId = Guid.Parse($"00000000-0000-0000-0000-{id:D12}").ToString(),
            Urn = $"urn:pulsestack:{type.ToString().ToLowerInvariant()}:{id}",
            Version = "1.0"
        };

    private static string Id(int value)
        => Guid.Parse($"10000000-0000-0000-0000-{value:D12}").ToString("D");

    private static AIAssetDocumentValidationError Error(string code, string message, string path)
        => new(code, message, path);

    private static void SetKind(WorkflowStepDocument step, WorkflowStepDocumentKind kind)
        => SetBackingField(typeof(WorkflowStepDocument), step, "<Kind>k__BackingField", kind);

    private static void SetKind(WorkflowConditionDocument condition, WorkflowConditionDocumentKind kind)
        => SetBackingField(typeof(WorkflowConditionDocument), condition, "<Kind>k__BackingField", kind);

    private static void SetKind(WorkflowValueDocument value, WorkflowValueDocumentKind kind)
        => SetBackingField(typeof(WorkflowValueDocument), value, "<Kind>k__BackingField", kind);

    private static void SetBackingField(Type declaringType, object target, string fieldName, object value)
    {
        declaringType
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(target, value);
    }

    private sealed record UnsupportedStepDocument : WorkflowStepDocument
    {
        public UnsupportedStepDocument(WorkflowStepDocument source)
            : base(source)
        {
        }
    }

    private sealed record UnsupportedConditionDocument : WorkflowConditionDocument
    {
        public UnsupportedConditionDocument(WorkflowConditionDocument source)
            : base(source)
        {
        }
    }

    private sealed record UnsupportedValueDocument : WorkflowValueDocument
    {
        public UnsupportedValueDocument(WorkflowValueDocument source)
            : base(source)
        {
        }
    }

    private sealed class CancellingStepList : IEnumerable<WorkflowStepDocument>
    {
        private readonly CancellationTokenSource source;
        private readonly WorkflowStepDocument first;
        private readonly WorkflowStepDocument second;

        public CancellingStepList(
            CancellationTokenSource source,
            WorkflowStepDocument first,
            WorkflowStepDocument second)
        {
            this.source = source;
            this.first = first;
            this.second = second;
        }

        public IEnumerator<WorkflowStepDocument> GetEnumerator()
        {
            yield return first;
            source.Cancel();
            yield return second;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            => GetEnumerator();
    }
}
