using FluentAssertions;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Agents.Runtime.Composition;
using PulseStack.Tests.Fakes;
using Xunit;

namespace PulseStack.Tests.Workflows;

public class SwitchNodeExecutorTests
{
    [Fact]
    public async Task SwitchNode_Should_Execute_Matching_Case()
    {
        var runtime = WorkflowRuntimeFactory.CreateWithNestedWorkflowSupport();

        var workflow = new WorkflowPipeline("TestWorkflow")
            .Add(new FakeAgent("Step1", "Research Complete"))
            .Add(new SwitchNode(
                name: "StatusSwitch",
                selector: ctx => "Approved",           // hardcoded for this test
                cases: [
                    new SwitchCase("Approved", 
                        new FakeAgent("ApproverStep", "Approved"))
                ]));

        var context = new PipelineContext();
        var result = await runtime.ExecuteAsync(workflow, context);

        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Be("Approved");
    }

    [Fact]
    public async Task SwitchNode_Should_Execute_Default_Case()
    {
        var runtime = WorkflowRuntimeFactory.CreateWithNestedWorkflowSupport();

        var workflow = new WorkflowPipeline("TestWorkflow")
            .Add(new FakeAgent("Step1", "Research Complete"))
            .Add(new SwitchNode(
                name: "StatusSwitch",
                selector: ctx => "Pending",                    // no matching case
                cases: [
                    new SwitchCase("Approved", new FakeAgent("Approver", "Approved")),
                    new SwitchCase("Rejected", new FakeAgent("Rejector", "Rejected"))
                ],
                defaultNode: new FakeAgent("DefaultHandler", "Default Path Taken")));

        var context = new PipelineContext();
        var result = await runtime.ExecuteAsync(workflow, context);

        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Be("Default Path Taken");
    }

    [Fact]
    public async Task SwitchNode_Should_Return_Success_When_No_Match_And_No_Default()
    {
        var runtime = WorkflowRuntimeFactory.CreateWithNestedWorkflowSupport();

        var workflow = new WorkflowPipeline("TestWorkflow")
            .Add(new FakeAgent("Step1", "Initial Output"))
            .Add(new SwitchNode(
                name: "StatusSwitch",
                selector: ctx => "NonExistentValue",
                cases: [
                    new SwitchCase("Approved", new FakeAgent("Approver", "Approved"))
                ])); // No default

        var context = new PipelineContext();
        var result = await runtime.ExecuteAsync(workflow, context);

        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Be("Initial Output"); // Context not changed
    }

    [Fact]
    public async Task SwitchNode_Should_Match_Case_Insensitively()
    {
        var runtime = WorkflowRuntimeFactory.CreateWithNestedWorkflowSupport();

        var workflow = new WorkflowPipeline("TestWorkflow")
            .Add(new SwitchNode(
                name: "CaseTest",
                selector: _ => "approved",
                cases: [
                    new SwitchCase("Approved", new FakeAgent("Approver", "Matched"))
                ]));

        var result = await runtime.ExecuteAsync(workflow, new PipelineContext());

        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Be("Matched");
    }

    [Fact]
    public async Task SwitchNode_Should_Use_Selector_From_Context()
    {
        var runtime = WorkflowRuntimeFactory.CreateWithNestedWorkflowSupport();
        
        var switchNode = new SwitchNode(
                name: "RoleRouter",
                selector: ctx => ctx.Items.TryGetValue("UserRole", out var role) 
                               ? role?.ToString() 
                               : null,
                cases: [
                    new SwitchCase("admin", new FakeAgent("AdminStep", "Admin Processed")),
                    new SwitchCase("user",  new FakeAgent("UserStep", "User Processed"))
                ],
                defaultNode: new FakeAgent("GuestStep", "Guest Processed"));

        var workflow = new WorkflowPipeline("TestWorkflow")
            .Add(switchNode);

        var context = new PipelineContext();
        context.Items["UserRole"] = "admin";

        var result = await runtime.ExecuteAsync(workflow, context);

        result.Nodes.Should().ContainSingle();

        var node = result.Nodes.Single();

        node.NodeName.Should().Be(switchNode.Name);

        node.Success.Should().BeTrue();
        node.Output.Should().Be("Admin Processed"); 
        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Be("Admin Processed");
    }

    [Fact]
    public async Task Workflow_Should_Execute_Switch_Node()
    {
        var runtime = WorkflowRuntimeFactory.CreateWithNestedWorkflowSupport();

        var workflow = new WorkflowPipeline("TestWorkflow")
            .Add(new SwitchNode(
                name: "RoleRouter",
                selector: ctx => ctx.Items.TryGetValue("UserRole", out var role) 
                            ? role?.ToString() 
                            : null,
                cases: [
                    new SwitchCase("admin", new FakeAgent("AdminStep", "Admin Processed")),
                    new SwitchCase("user",  new FakeAgent("UserStep", "User Processed"))
                ],
                defaultNode: new FakeAgent("GuestStep", "Guest Processed")));

        var context = new PipelineContext();
        context.Items["UserRole"] = "admin";

        var result = await runtime.ExecuteAsync(workflow, context);

        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Be("Admin Processed");
    }

    [Fact]
    public async Task Workflow_Should_Execute_Default_Switch_Node()
    {
        var runtime = WorkflowRuntimeFactory.CreateWithNestedWorkflowSupport();

        var workflow = new WorkflowPipeline("TestWorkflow")
            .Add(new FakeAgent("Step1", "Started"))
            .Add(new SwitchNode(
                name: "RoleRouter",
                selector: ctx => ctx.Items.TryGetValue("UserRole", out var role) 
                               ? role?.ToString() 
                               : "guest",
                cases: [
                    new SwitchCase("admin", new FakeAgent("AdminStep", "Admin Path"))
                ],
                defaultNode: new FakeAgent("DefaultStep", "Default Path")));

        var context = new PipelineContext();
        context.Items["UserRole"] = "manager"; // no match

        var result = await runtime.ExecuteAsync(workflow, context);

        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Be("Default Path");
    }

    [Fact]
    public async Task SwitchNode_Should_Handle_Null_Selector_Result()
    {
        var runtime = WorkflowRuntimeFactory.CreateWithNestedWorkflowSupport();

        var workflow = new WorkflowPipeline("TestWorkflow")
            .Add(new FakeAgent("Step1", "Initial Output"))
            .Add(new SwitchNode(
                name: "NullSelectorSwitch",
                selector: _ => null,                    // Explicitly returns null
                cases: [
                    new SwitchCase("Approved", new FakeAgent("Approver", "Approved")),
                    new SwitchCase("Rejected", new FakeAgent("Rejector", "Rejected"))
                ],
                defaultNode: new FakeAgent("DefaultHandler", "Default Path Executed")));

        var context = new PipelineContext();
        var result = await runtime.ExecuteAsync(workflow, context);

        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Be("Default Path Executed");
    }

    [Fact]
    public async Task SwitchNode_Should_Handle_Null_Selector_Result_Without_Default()
    {
        var runtime = WorkflowRuntimeFactory.CreateWithNestedWorkflowSupport();

        var workflow = new WorkflowPipeline("TestWorkflow")
            .Add(new FakeAgent("Step1", "Initial Output"))
            .Add(new SwitchNode(
                name: "NullSelectorSwitch",
                selector: _ => null,           // Returns null
                cases: [
                    new SwitchCase("Approved", new FakeAgent("Approver", "Approved"))
                ])); // No defaultNode

        var context = new PipelineContext();
        var result = await runtime.ExecuteAsync(workflow, context);

        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Be("Initial Output"); // Context unchanged
    }    

    [Fact]
    public async Task SwitchNode_Should_Handle_Null_From_Context_Selector()
    {
        var runtime = WorkflowRuntimeFactory.CreateWithNestedWorkflowSupport();

        var workflow = new WorkflowPipeline("TestWorkflow")
            .Add(new FakeAgent("Step1", "Started"))
            .Add(new SwitchNode(
                name: "RoleSwitch",
                selector: ctx => ctx.Items.TryGetValue("UserRole", out var role) 
                            ? role?.ToString() 
                            : null,
                cases: [
                    new SwitchCase("admin", new FakeAgent("AdminStep", "Admin Path"))
                ],
                defaultNode: new FakeAgent("DefaultStep", "Fallback Path")));

        var context = new PipelineContext();
        // "UserRole" key is missing → selector returns null

        var result = await runtime.ExecuteAsync(workflow, context);

        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Be("Fallback Path");
    }    

    [Fact]
    public async Task SwitchNode_Should_Select_First_Matching_Case_When_Duplicates_Exist()
    {
        var runtime = WorkflowRuntimeFactory.CreateWithNestedWorkflowSupport();

        var firstCase  = new FakeAgent("FirstApprover",  "First Approved Path");
        var secondCase = new FakeAgent("SecondApprover", "Second Approved Path");

        var switchNode = new SwitchNode(
                name: "DuplicateCaseSwitch",
                selector: _ => "Approved",
                cases: [
                    new SwitchCase("Approved", firstCase),
                    new SwitchCase("Rejected", new FakeAgent("Rejector", "Rejected")),
                    new SwitchCase("Approved", secondCase)   // Duplicate
                ]);
        var workflow = new WorkflowPipeline("TestWorkflow")
            .Add(new FakeAgent("Step1", "Initial Output"))
            .Add(switchNode);

        var result = await runtime.ExecuteAsync(workflow, new PipelineContext());

        result.Nodes.Should().HaveCount(2);

        result.Nodes[0].NodeName.Should().Be("Step1");

        result.Nodes[1].NodeName.Should().Be("DuplicateCaseSwitch");
        result.Nodes[1].Success.Should().BeTrue();
        result.Nodes[1].Output.Should().Be("First Approved Path");

        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Be("First Approved Path");

    }    
}