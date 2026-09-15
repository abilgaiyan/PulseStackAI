using PulseStack.Abstractions.Runtime.Invocation.Application;
using PulseStack.Core.Runtime.Invocation.Application;
using Xunit;

namespace PulseStack.Tests.Runtime.Invocation.Application;

public sealed class ApplicationInvocationContextProjectorTests
{
    [Fact]
    public void Project_ShouldRequireRequest()
    {
        Assert.Throws<ArgumentNullException>(
            () => ApplicationInvocationContextProjector.Project(null!));
    }

    [Fact]
    public void Project_ShouldInitializeFreshContextFromExactInput()
    {
        const string input = "  exact input  ";
        var request = new ApplicationInvocationRequest(input);

        var context = ApplicationInvocationContextProjector.Project(request);

        Assert.Equal(input, context.Input);
        Assert.Equal(input, context.CurrentOutput);
        Assert.Empty(context.Items);
        Assert.Empty(context.Steps);
        Assert.Empty(context.ToolResults);
    }

    [Fact]
    public void Project_ShouldCopyAllItemsWithOrdinalKeyIdentityAndExactValues()
    {
        var first = new object();
        var second = new object();
        var request = new ApplicationInvocationRequest(
            "input",
            new Dictionary<string, object?>
            {
                ["Key"] = first,
                ["key"] = second,
                ["runtime-looking-key"] = null
            });

        var context = ApplicationInvocationContextProjector.Project(request);

        Assert.Equal(3, context.Items.Count);
        Assert.Same(first, context.Items["Key"]);
        Assert.Same(second, context.Items["key"]);
        Assert.True(context.Items.ContainsKey("runtime-looking-key"));
        Assert.Null(context.Items["runtime-looking-key"]);
        Assert.False(context.Items.ContainsKey("KEY"));
    }

    [Fact]
    public void Project_ShouldStructurallyIsolateContextItemsFromRequestSnapshot()
    {
        var sharedValue = new object();
        var request = new ApplicationInvocationRequest(
            "input",
            new Dictionary<string, object?>
            {
                ["original"] = sharedValue
            });

        var context = ApplicationInvocationContextProjector.Project(request);

        context.Items["added-by-runtime"] = new object();
        context.Items.Remove("original");

        Assert.Single(request.Items);
        Assert.True(request.Items.ContainsKey("original"));
        Assert.Same(sharedValue, request.Items["original"]);
        Assert.False(request.Items.ContainsKey("added-by-runtime"));
    }

    [Fact]
    public void Project_ShouldCreateIndependentMutableStructuresForEveryInvocation()
    {
        var request = new ApplicationInvocationRequest(
            "input",
            new Dictionary<string, object?> { ["item"] = new object() });

        var first = ApplicationInvocationContextProjector.Project(request);
        var second = ApplicationInvocationContextProjector.Project(request);

        Assert.NotSame(first, second);
        Assert.NotSame(first.Items, second.Items);
        Assert.NotSame(first.Steps, second.Steps);
        Assert.NotSame(first.ToolResults, second.ToolResults);

        first.Input = "changed-input";
        first.CurrentOutput = "changed-output";
        first.Items["first-only"] = true;

        Assert.Equal("input", second.Input);
        Assert.Equal("input", second.CurrentOutput);
        Assert.False(second.Items.ContainsKey("first-only"));
        Assert.Equal("input", request.Input);
        Assert.False(request.Items.ContainsKey("first-only"));
    }
}
