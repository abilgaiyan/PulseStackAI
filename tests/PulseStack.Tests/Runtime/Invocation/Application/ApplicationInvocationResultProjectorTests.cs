using System.Reflection;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Runtime.Realization.Application;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Core.Runtime.Invocation.Application;
using Xunit;

namespace PulseStack.Tests.Runtime.Invocation.Application;

public sealed class ApplicationInvocationResultProjectorTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Project_ShouldPreserveExactCompletedResultAndApplicationProvenance(bool success)
    {
        var application = Application();
        var first = new StepExecutionResult
        {
            StepName = "first",
            Success = true,
            Output = null,
            Usage = null
        };
        var second = new StepExecutionResult
        {
            StepName = "second",
            Success = false,
            Output = "output"
        };
        var steps = new List<StepExecutionResult> { first, second };
        var execution = new WorkflowExecutionResult
        {
            Success = success,
            FinalOutput = "  exact final output  ",
            Steps = steps
        };

        var result = ApplicationInvocationResultProjector.Project(application, execution);

        Assert.Same(application.Project, result.Project);
        Assert.Same(application.EntryWorkflow, result.EntryWorkflow);
        Assert.Equal(success, result.Success);
        Assert.Equal("  exact final output  ", result.FinalOutput);
        Assert.Equal(new[] { first, second }, result.Steps);
        Assert.Same(first, result.Steps[0]);
        Assert.Same(second, result.Steps[1]);

        steps.Clear();
        Assert.Equal(new[] { first, second }, result.Steps);
    }

    [Fact]
    public void Project_ShouldRejectNullApplicationAsPublicInputError()
    {
        Assert.Throws<ArgumentNullException>(
            () => ApplicationInvocationResultProjector.Project(
                null!,
                new WorkflowExecutionResult()));
    }

    [Fact]
    public void Project_ShouldClassifyNullExecutionResultAsMalformedPredecessorOutput()
    {
        Assert.Throws<InvalidOperationException>(
            () => ApplicationInvocationResultProjector.Project(Application(), null!));
    }

    [Fact]
    public void Project_ShouldValidateFinalOutputBeforeSteps()
    {
        var execution = new WorkflowExecutionResult
        {
            FinalOutput = null!,
            Steps = null!
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => ApplicationInvocationResultProjector.Project(Application(), execution));

        Assert.Contains("final output", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Project_ShouldValidateStepsCollectionAfterFinalOutput()
    {
        var execution = new WorkflowExecutionResult
        {
            FinalOutput = "valid",
            Steps = null!
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => ApplicationInvocationResultProjector.Project(Application(), execution));

        Assert.Contains("steps collection", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Project_ShouldValidateAndSnapshotStepsInOnePass()
    {
        var first = new StepExecutionResult { StepName = "first", Success = true };
        var execution = new WorkflowExecutionResult
        {
            FinalOutput = "valid",
            Steps = new StepExecutionResult[] { first, null! }
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => ApplicationInvocationResultProjector.Project(Application(), execution));

        Assert.Contains("null step", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static RealizedApplication Application()
    {
        var project = new AssetReference(
            AssetType.Project,
            AssetId.New(),
            new AssetUrn("urn:pulsestack:project:test"),
            new AssetVersion("1.0.0"));
        var entryWorkflow = new AssetReference(
            AssetType.Workflow,
            AssetId.New(),
            new AssetUrn("urn:pulsestack:workflow:test"),
            new AssetVersion("1.0.0"));
        var constructor = typeof(RealizedApplication).GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic).Single();

        return (RealizedApplication)constructor.Invoke(
            [project, entryWorkflow, new Workflow("entry")]);
    }
}
