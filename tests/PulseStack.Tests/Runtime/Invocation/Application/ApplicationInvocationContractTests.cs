using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Runtime.Invocation.Application;
using PulseStack.Abstractions.Runtime.Realization.Application;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Steps;
using Xunit;

namespace PulseStack.Tests.Runtime.Invocation.Application;

public sealed class ApplicationInvocationContractTests
{
    [Fact]
    public void RealizedApplication_ShouldExposeRestrictedConstructionAndPreserveExactValues()
    {
        var project = Reference(AssetType.Project);
        var entryWorkflow = Reference(AssetType.Workflow);
        var workflow = new Workflow("entry");
        var constructors = typeof(RealizedApplication).GetConstructors(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        constructors.Should().ContainSingle();
        constructors[0].IsAssembly.Should().BeTrue();

        var application = ConstructRealizedApplication(project, entryWorkflow, workflow);
        application.Project.Should().BeSameAs(project);
        application.EntryWorkflow.Should().BeSameAs(entryWorkflow);
        application.Workflow.Should().BeSameAs(workflow);
    }

    [Fact]
    public void RealizedApplication_ShouldRejectInvalidProvenanceAndNullWorkflow()
    {
        AssertRealizedApplicationRejected(Reference(AssetType.Workflow), Reference(AssetType.Workflow), new Workflow("entry"));
        AssertRealizedApplicationRejected(Reference(AssetType.Project), Reference(AssetType.Agent), new Workflow("entry"));
        AssertRealizedApplicationRejected(MalformedReference(AssetType.Project), Reference(AssetType.Workflow), new Workflow("entry"));
        AssertRealizedApplicationRejected(Reference(AssetType.Project), MalformedReference(AssetType.Workflow), new Workflow("entry"));
        AssertRealizedApplicationRejected(Reference(AssetType.Project), Reference(AssetType.Workflow), null!);
    }

    [Fact]
    public void Request_ShouldPreserveInputAndCreateReadOnlyOrdinalSnapshot()
    {
        var shared = new object();
        var source = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Key"] = shared,
            ["key"] = null
        };

        var request = new ApplicationInvocationRequest("  input  ", source);
        source["Key"] = new object();
        source["later"] = 42;

        request.Input.Should().Be("  input  ");
        request.Items.Should().HaveCount(2);
        request.Items["Key"].Should().BeSameAs(shared);
        request.Items.Should().ContainKey("key").WhoseValue.Should().BeNull();
        request.Items.Should().NotContainKey("later");

        var mutableView = request.Items.Should()
            .BeAssignableTo<IDictionary<string, object?>>().Subject;
        new Action(() => mutableView.Add("forbidden", 1))
            .Should().Throw<NotSupportedException>();
        request.Items.Should().NotContainKey("forbidden");
    }

    [Fact]
    public void Request_ShouldUseOrdinalSnapshotSemanticsRegardlessOfSourceComparer()
    {
        var source = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Key"] = 1
        };

        var request = new ApplicationInvocationRequest("input", source);

        request.Items.Should().ContainKey("Key");
        request.Items.ContainsKey("key").Should().BeFalse();
    }

    [Fact]
    public void Request_ShouldRejectSourceThatEnumeratesDuplicateOrdinalKeys()
    {
        IReadOnlyDictionary<string, object?> source = new DuplicateOrdinalKeyDictionary();

        new Action(() => new ApplicationInvocationRequest("input", source))
            .Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Request_ShouldPreserveEmptyAndWhitespaceInput(string input)
    {
        new ApplicationInvocationRequest(input).Input.Should().Be(input);
    }

    [Fact]
    public void Request_ShouldRejectNullInputAndInvalidItemKeys()
    {
        new Action(() => new ApplicationInvocationRequest(null!))
            .Should().Throw<ArgumentNullException>();

        foreach (var key in new[] { "", " " })
        {
            var items = new Dictionary<string, object?>(StringComparer.Ordinal) { [key] = null };
            new Action(() => new ApplicationInvocationRequest("input", items))
                .Should().Throw<ArgumentException>();
        }
    }

    [Fact]
    public void Request_ShouldExposeEmptyReadOnlyItemsWhenOmitted()
    {
        var request = new ApplicationInvocationRequest("input");
        request.Items.Should().BeEmpty();

        var mutableView = request.Items.Should()
            .BeAssignableTo<IDictionary<string, object?>>().Subject;
        new Action(() => mutableView.Add("forbidden", 1))
            .Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Result_ShouldPreserveProvenanceAndSnapshotStepStructure()
    {
        var project = Reference(AssetType.Project);
        var entryWorkflow = Reference(AssetType.Workflow);
        var first = new StepExecutionResult { StepName = "first", Success = true };
        var second = new StepExecutionResult { StepName = "second", Success = false };
        var source = new List<StepExecutionResult> { first, second };

        var result = new ApplicationInvocationResult(project, entryWorkflow, false, "final", source);
        source.Clear();

        result.Project.Should().BeSameAs(project);
        result.EntryWorkflow.Should().BeSameAs(entryWorkflow);
        result.Success.Should().BeFalse();
        result.FinalOutput.Should().Be("final");
        result.Steps.Should().Equal(first, second);
        result.Steps[0].Should().BeSameAs(first);

        var mutableView = result.Steps.Should()
            .BeAssignableTo<IList<StepExecutionResult>>().Subject;
        new Action(() => mutableView.Add(new StepExecutionResult()))
            .Should().Throw<NotSupportedException>();
        result.Steps.Should().Equal(first, second);
    }

    [Fact]
    public void Result_ShouldRejectInvalidProvenanceAndMalformedState()
    {
        var project = Reference(AssetType.Project);
        var workflow = Reference(AssetType.Workflow);
        var steps = Array.Empty<StepExecutionResult>();

        new Action(() => new ApplicationInvocationResult(Reference(AssetType.Workflow), workflow, true, "output", steps)).Should().Throw<ArgumentException>();
        new Action(() => new ApplicationInvocationResult(project, Reference(AssetType.Agent), true, "output", steps)).Should().Throw<ArgumentException>();
        new Action(() => new ApplicationInvocationResult(MalformedReference(AssetType.Project), workflow, true, "output", steps)).Should().Throw<ArgumentException>();
        new Action(() => new ApplicationInvocationResult(project, MalformedReference(AssetType.Workflow), true, "output", steps)).Should().Throw<ArgumentException>();
        new Action(() => new ApplicationInvocationResult(project, workflow, true, null!, steps)).Should().Throw<ArgumentNullException>();
        new Action(() => new ApplicationInvocationResult(project, workflow, true, "output", null!)).Should().Throw<ArgumentNullException>();
        new Action(() => new ApplicationInvocationResult(project, workflow, true, "output", new StepExecutionResult[] { null! })).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Invoker_ShouldExposeExactlyOneFrozenOperation()
    {
        var methods = typeof(IApplicationInvoker).GetMethods();
        methods.Should().ContainSingle();
        methods[0].Name.Should().Be(nameof(IApplicationInvoker.InvokeAsync));
        methods[0].ReturnType.Should().Be(typeof(Task<ApplicationInvocationResult>));
        methods[0].GetParameters().Select(static parameter => parameter.ParameterType)
            .Should().Equal(typeof(RealizedApplication), typeof(ApplicationInvocationRequest), typeof(CancellationToken));
    }

    private static RealizedApplication ConstructRealizedApplication(
        AssetReference project,
        AssetReference entryWorkflow,
        Workflow workflow)
    {
        var constructor = typeof(RealizedApplication).GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic).Single();
        return (RealizedApplication)constructor.Invoke([project, entryWorkflow, workflow]);
    }

    private static void AssertRealizedApplicationRejected(
        AssetReference project,
        AssetReference entryWorkflow,
        Workflow workflow)
    {
        var action = () => ConstructRealizedApplication(project, entryWorkflow, workflow);
        action.Should().Throw<TargetInvocationException>()
            .Which.InnerException.Should().BeAssignableTo<ArgumentException>();
    }

    private static AssetReference Reference(AssetType type) =>
        new(type, AssetId.New(), new AssetUrn($"urn:pulsestack:{type.ToString().ToLowerInvariant()}:test"), new AssetVersion("1.0.0"));

    private static AssetReference MalformedReference(AssetType type) =>
        new(type, AssetId.Empty, new AssetUrn($"urn:pulsestack:{type.ToString().ToLowerInvariant()}:test"), new AssetVersion("1.0.0"));

    private sealed class DuplicateOrdinalKeyDictionary : IReadOnlyDictionary<string, object?>
    {
        private static readonly KeyValuePair<string, object?>[] Entries =
        [
            new("duplicate", 1),
            new("duplicate", 2)
        ];

        public object? this[string key] => throw new KeyNotFoundException();

        public IEnumerable<string> Keys => Entries.Select(static entry => entry.Key);

        public IEnumerable<object?> Values => Entries.Select(static entry => entry.Value);

        public int Count => Entries.Length;

        public bool ContainsKey(string key) =>
            Entries.Any(entry => StringComparer.Ordinal.Equals(entry.Key, key));

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() =>
            ((IEnumerable<KeyValuePair<string, object?>>)Entries).GetEnumerator();

        public bool TryGetValue(string key, out object? value)
        {
            foreach (var entry in Entries)
            {
                if (StringComparer.Ordinal.Equals(entry.Key, key))
                {
                    value = entry.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
