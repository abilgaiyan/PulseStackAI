using System.Reflection;
using FluentAssertions;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Abstractions.Persistence.AIAssets.Validation;
using PulseStack.Core.Persistence.AIAssets.Validation;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets.Validation;

public sealed class WorkflowLiteralCanonicalValidationTests
{
    private readonly IAIAssetDocumentValidator validator = new AIAssetDocumentValidator();

    [Fact]
    public async Task ValidateAsync_ShouldRejectUnsupportedLiteralAndDiscriminatorMismatch()
    {
        var unsupported = new UnsupportedLiteralDocument(new StringWorkflowLiteralDocument("value"));
        var mismatched = new StringWorkflowLiteralDocument("value");
        SetKind(mismatched, WorkflowLiteralDocumentKind.Boolean);

        var document = CreateWorkflow(
            LiteralLoop(1, unsupported),
            LiteralLoop(2, mismatched));

        var result = await validator.ValidateAsync(document);

        LiteralErrors(result).Should().Equal(
            Error(AIAssetDocumentValidationCodes.UnsupportedWorkflowLiteral, "$.steps[0].items.literal"),
            Error(AIAssetDocumentValidationCodes.WorkflowLiteralTypeMismatch, "$.steps[1].items.literal.kind"));
    }

    [Fact]
    public async Task ValidateAsync_ShouldValidateStringNullButAllowEmptyString()
    {
        var document = CreateWorkflow(
            LiteralLoop(1, new StringWorkflowLiteralDocument(null!)),
            LiteralLoop(2, new StringWorkflowLiteralDocument(string.Empty)));

        var result = await validator.ValidateAsync(document);

        LiteralErrors(result).Should().Equal(
            Error(AIAssetDocumentValidationCodes.MissingWorkflowStringLiteralValue, "$.steps[0].items.literal.value"));
    }

    [Fact]
    public async Task ValidateAsync_ShouldValidateArrayAndObjectMembersRecursively()
    {
        var literal = new ArrayWorkflowLiteralDocument(
            [
                null!,
                new ObjectWorkflowLiteralDocument(
                    [
                        null!,
                        new WorkflowLiteralPropertyDocument("value", null!),
                        new WorkflowLiteralPropertyDocument("nested", new StringWorkflowLiteralDocument(null!))
                    ])
            ]);

        var result = await validator.ValidateAsync(CreateWorkflow(LiteralLoop(1, literal)));

        LiteralErrors(result).Should().Equal(
            Error(AIAssetDocumentValidationCodes.MissingWorkflowArrayItem, "$.steps[0].items.literal.items[0]"),
            Error(AIAssetDocumentValidationCodes.MissingWorkflowObjectProperty, "$.steps[0].items.literal.items[1].properties[0]"),
            Error(AIAssetDocumentValidationCodes.MissingWorkflowObjectPropertyValue, "$.steps[0].items.literal.items[1].properties[1].value"),
            Error(AIAssetDocumentValidationCodes.NonCanonicalWorkflowObjectPropertyOrder, "$.steps[0].items.literal.items[1].properties[2].name"),
            Error(AIAssetDocumentValidationCodes.MissingWorkflowStringLiteralValue, "$.steps[0].items.literal.items[1].properties[2].value.value"));
    }

    [Fact]
    public async Task ValidateAsync_ShouldUseOnlyValidNamesForDuplicateAndOrdinalOrdering()
    {
        var literal = new ObjectWorkflowLiteralDocument(
            [
                new WorkflowLiteralPropertyDocument("b", new NullWorkflowLiteralDocument()),
                new WorkflowLiteralPropertyDocument(" ", new NullWorkflowLiteralDocument()),
                null!,
                new WorkflowLiteralPropertyDocument("a", null!),
                new WorkflowLiteralPropertyDocument("a", new NullWorkflowLiteralDocument()),
                new WorkflowLiteralPropertyDocument("A", new NullWorkflowLiteralDocument())
            ]);

        var result = await validator.ValidateAsync(CreateWorkflow(LiteralLoop(1, literal)));

        LiteralErrors(result).Should().Equal(
            Error(AIAssetDocumentValidationCodes.InvalidWorkflowObjectPropertyName, "$.steps[0].items.literal.properties[1].name"),
            Error(AIAssetDocumentValidationCodes.MissingWorkflowObjectProperty, "$.steps[0].items.literal.properties[2]"),
            Error(AIAssetDocumentValidationCodes.MissingWorkflowObjectPropertyValue, "$.steps[0].items.literal.properties[3].value"),
            Error(AIAssetDocumentValidationCodes.NonCanonicalWorkflowObjectPropertyOrder, "$.steps[0].items.literal.properties[3].name"),
            Error(AIAssetDocumentValidationCodes.DuplicateWorkflowObjectPropertyName, "$.steps[0].items.literal.properties[4].name"),
            Error(AIAssetDocumentValidationCodes.NonCanonicalWorkflowObjectPropertyOrder, "$.steps[0].items.literal.properties[5].name"));
    }

    [Fact]
    public async Task ValidateAsync_ShouldKeepDuplicateAndOrderingDiagnosticsIndependent()
    {
        var literal = new ObjectWorkflowLiteralDocument(
            [
                new WorkflowLiteralPropertyDocument("b", new NullWorkflowLiteralDocument()),
                new WorkflowLiteralPropertyDocument("a", new NullWorkflowLiteralDocument()),
                new WorkflowLiteralPropertyDocument("b", new NullWorkflowLiteralDocument())
            ]);

        var result = await validator.ValidateAsync(CreateWorkflow(LiteralLoop(1, literal)));

        LiteralErrors(result).Should().Equal(
            Error(AIAssetDocumentValidationCodes.NonCanonicalWorkflowObjectPropertyOrder, "$.steps[0].items.literal.properties[1].name"),
            Error(AIAssetDocumentValidationCodes.DuplicateWorkflowObjectPropertyName, "$.steps[0].items.literal.properties[2].name"));
    }

    [Fact]
    public async Task ValidateAsync_ShouldPreserveNestedLiteralDiagnosticOrder()
    {
        var literal = new ObjectWorkflowLiteralDocument(
            [
                new WorkflowLiteralPropertyDocument(
                    "a",
                    new ArrayWorkflowLiteralDocument(
                        [
                            new ObjectWorkflowLiteralDocument(
                                [
                                    new WorkflowLiteralPropertyDocument("z", new StringWorkflowLiteralDocument(null!)),
                                    new WorkflowLiteralPropertyDocument("a", new NullWorkflowLiteralDocument())
                                ]),
                            null!
                        ])),
                new WorkflowLiteralPropertyDocument("b", new NullWorkflowLiteralDocument())
            ]);

        var result = await validator.ValidateAsync(CreateWorkflow(LiteralLoop(1, literal)));

        LiteralErrors(result).Should().Equal(
            Error(AIAssetDocumentValidationCodes.MissingWorkflowStringLiteralValue, "$.steps[0].items.literal.properties[0].value.items[0].properties[0].value.value"),
            Error(AIAssetDocumentValidationCodes.NonCanonicalWorkflowObjectPropertyOrder, "$.steps[0].items.literal.properties[0].value.items[0].properties[1].name"),
            Error(AIAssetDocumentValidationCodes.MissingWorkflowArrayItem, "$.steps[0].items.literal.properties[0].value.items[1]"));
    }

    private static IEnumerable<(string Code, string Path)> LiteralErrors(AIAssetDocumentValidationResult result)
        => result.Errors
            .Where(error => error.Code is
                AIAssetDocumentValidationCodes.UnsupportedWorkflowLiteral
                or AIAssetDocumentValidationCodes.WorkflowLiteralTypeMismatch
                or AIAssetDocumentValidationCodes.MissingWorkflowArrayItem
                or AIAssetDocumentValidationCodes.MissingWorkflowObjectProperty
                or AIAssetDocumentValidationCodes.MissingWorkflowObjectPropertyValue
                or AIAssetDocumentValidationCodes.InvalidWorkflowObjectPropertyName
                or AIAssetDocumentValidationCodes.DuplicateWorkflowObjectPropertyName
                or AIAssetDocumentValidationCodes.NonCanonicalWorkflowObjectPropertyOrder
                or AIAssetDocumentValidationCodes.MissingWorkflowStringLiteralValue)
            .Select(error => (error.Code, error.Path));

    private static (string Code, string Path) Error(string code, string path)
        => (code, path);

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
                Urn = "urn:pulsestack:workflow:literal-validation",
                Version = "1.0"
            },
            new AIAssetMetadataDocument("Workflow"),
            AIAssetLifecycleDocument.Draft,
            steps);

    private static string Id(int value)
        => Guid.Parse($"10000000-0000-0000-0000-{value:D12}").ToString("D");

    private static void SetKind(WorkflowLiteralDocument literal, WorkflowLiteralDocumentKind kind)
        => typeof(WorkflowLiteralDocument)
            .GetField("<Kind>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(literal, kind);

    private sealed record UnsupportedLiteralDocument : WorkflowLiteralDocument
    {
        public UnsupportedLiteralDocument(WorkflowLiteralDocument source)
            : base(source)
        {
        }
    }
}
