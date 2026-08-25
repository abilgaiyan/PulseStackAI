using FluentAssertions;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Workflows.Values;
using PulseStack.Core.Runtime.Realization.Evaluation;
using Xunit;

namespace PulseStack.Tests.Runtime.Realization;

public sealed class WorkflowValueEvaluatorTests
{
    private readonly WorkflowValueEvaluator _evaluator = new();

    [Fact]
    public void Evaluate_ShouldReadWorkflowInput()
    {
        var context = CreateContext();

        var result = _evaluator.Evaluate(
            new InputValueDefinition(),
            context);

        result.Should().Be("original input");
    }

    [Fact]
    public void Evaluate_ShouldReadCurrentOutput()
    {
        var context = CreateContext();

        var result = _evaluator.Evaluate(
            new CurrentOutputValueDefinition(),
            context);

        result.Should().Be("latest output");
    }

    [Fact]
    public void Evaluate_ShouldReadContextItem()
    {
        var context = CreateContext();
        context.Items["documents"] = new[] { "one", "two" };

        var result = _evaluator.Evaluate(
            new ContextItemValueDefinition
            {
                Key = "documents"
            },
            context);

        result.Should().BeEquivalentTo(new[] { "one", "two" });
    }

    [Fact]
    public void Evaluate_ShouldReturnNull_WhenContextItemDoesNotExist()
    {
        var context = CreateContext();

        var result = _evaluator.Evaluate(
            new ContextItemValueDefinition
            {
                Key = "missing"
            },
            context);

        result.Should().BeNull();
    }

    [Fact]
    public void Evaluate_ShouldReturnLiteralValue()
    {
        var context = CreateContext();
        var value = new[] { "billing", "technical" };

        var result = _evaluator.Evaluate(
            new LiteralValueDefinition
            {
                Value = value
            },
            context);

        result.Should().BeSameAs(value);
    }

    private static PipelineContext CreateContext() =>
        new()
        {
            Input = "original input",
            CurrentOutput = "latest output"
        };
}
