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
                Step = new ParallelStepDefinition
                {
                    Name = "empty",
                    Steps = []
                }
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
        var workflow = CreateWorkflow(
            new LoopStepDefinition
            {
                Name = "literal-loop",
                Items = new LiteralValueDefinition
                {
                    Value = new BlankKeyReadOnlyMap()
                },
                Step = new ParallelStepDefinition
                {
                    Name = "empty",
                    Steps = []
                }
            });

        var act = () => new AIAssetDocumentMapper().ToDocument(workflow);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*$.steps[0].items.literal*null, empty, or whitespace key*");
    }

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
}
