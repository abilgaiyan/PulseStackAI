using FluentAssertions;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Workflow.Nodes;
using PulseStack.Agents.Runtime.Diagnostics.Events;
using PulseStack.Tests.Fakes;
using Xunit;

namespace PulseStack.Tests.Workflows;

public class WorkflowObservabilityTests
{
    [Fact]
    public async Task Workflow_Should_Emit_WorkflowStartedEvent()
    {
        var observer =
            new RecordingRuntimeObserver();

        var runtime =
            WorkflowRuntimeFactory.Create(
                observer);

        await runtime.ExecuteAsync(
            CreateWorkflow(),
            new PipelineContext());

        observer.Events
            .OfType<WorkflowStartedEvent>()
            .Should()
            .ContainSingle();
    }

    [Fact]
    public async Task Workflow_Should_Emit_WorkflowCompletedEvent()
    {
        var observer =
            new RecordingRuntimeObserver();

        var runtime =
            WorkflowRuntimeFactory.Create(
                observer);

        await runtime.ExecuteAsync(
            CreateWorkflow(),
            new PipelineContext());

        observer.Events
            .OfType<WorkflowCompletedEvent>()
            .Should()
            .ContainSingle();
    }

    [Fact]
    public async Task Workflow_Should_Emit_NodeStartedEvent()
    {
        var observer =
            new RecordingRuntimeObserver();

        var runtime =
            WorkflowRuntimeFactory.Create(
                observer);

        await runtime.ExecuteAsync(
            CreateWorkflow(),
            new PipelineContext());

        observer.Events
            .OfType<NodeStartedEvent>()
            .Should()
            .ContainSingle();
    }

   [Fact]
    public async Task Workflow_Should_Emit_NodeCompletedEvent()
    {
        var observer =
            new RecordingRuntimeObserver();

        var runtime =
            WorkflowRuntimeFactory.Create(
                observer);

        await runtime.ExecuteAsync(
            CreateWorkflow(),
            new PipelineContext());

        observer.Events
            .OfType<NodeCompletedEvent>()
            .Should()
            .ContainSingle();
    }

    private static WorkflowDefinition CreateWorkflow()
    {
        return new WorkflowDefinition("Workflow")
            .Add(
                new FakeAgent(
                    "Researcher",
                    "Research Complete"));
    }
}