using FluentAssertions;
using PulseStack.Abstractions.Agents;
using PulseStack.Agents.Runtime.Composition;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Abstractions.Workflows.Routing;
using PulseStack.Abstractions.Runtime.Usage;
using PulseStack.Tests.Fakes;
using Xunit;

namespace PulseStack.Tests.Workflows;

public class SwitchStepExecutorTests
{
    [Fact]
    public async Task SwitchStep_Should_Execute_Matching_Case()
    {
        var runtime = WorkflowTestRuntimeFactory.CreateWithNestedWorkflowSupport();

        var workflow = new Workflow("TestWorkflow")
            .Add(new RunStep(new FakeAgent("Step1", "Research Complete")))
            .Add(new SwitchStep(
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
    public async Task SwitchStep_Should_Return_Own_Name_And_Preserve_Child_Result()
    {
        var usage =
            new AIUsage
            {
                Provider = "Test",
                Model = "model",
                PromptTokens = 8,
                CompletionTokens = 9
            };

        var executors =
            new List<IStepExecutor>
            {
                new FakeStepExecutor(
                    success: false,
                    output: "Rejected",
                    usage: usage)
            };

        var resolver =
            new StepExecutorResolver(
                executors);

        var executor =
            new SwitchStepExecutor(
                resolver);

        var step =
            new SwitchStep(
                name: "StatusSwitch",
                selector: _ => "Rejected",
                cases:
                [
                    new SwitchCase(
                        "Rejected",
                        new FakeAgent(
                            "Rejector",
                            "Ignored"))
                ]);

        var result =
            await executor.ExecuteAsync(
                step,
                new PipelineContext());

        result.StepName.Should().Be("StatusSwitch");
        result.Success.Should().BeFalse();
        result.Output.Should().Be("Rejected");
        result.Usage.Should().BeSameAs(usage);
    }

    [Fact]
    public async Task SwitchStep_Should_Execute_Default_Case()
    {
        var runtime = WorkflowTestRuntimeFactory.CreateWithNestedWorkflowSupport();

        var workflow = new Workflow("TestWorkflow")
            .Add(new FakeAgent("Step1", "Research Complete"))
            .Add(new SwitchStep(
                name: "StatusSwitch",
                selector: ctx => "Pending",                    // no matching case
                cases: [
                    new SwitchCase("Approved", new FakeAgent("Approver", "Approved")),
                    new SwitchCase("Rejected", new FakeAgent("Rejector", "Rejected"))
                ],
                defaultStep: new RunStep(new FakeAgent("DefaultHandler", "Default Path Taken"))));

        var context = new PipelineContext();
        var result = await runtime.ExecuteAsync(workflow, context);

        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Be("Default Path Taken");
    }

    [Fact]
    public async Task SwitchStep_Should_Return_Success_When_No_Match_And_No_Default()
    {
        var runtime = WorkflowTestRuntimeFactory.CreateWithNestedWorkflowSupport();

        var workflow = new Workflow("TestWorkflow")
            .Add(new FakeAgent("Step1", "Initial Output"))
            .Add(new SwitchStep(
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
    public async Task SwitchStep_Should_Match_Case_Insensitively()
    {
        var runtime = WorkflowTestRuntimeFactory.CreateWithNestedWorkflowSupport();

        var workflow = new Workflow("TestWorkflow")
            .Add(new SwitchStep(
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
    public async Task SwitchStep_Should_Use_Selector_From_Context()
    {
        var runtime = WorkflowTestRuntimeFactory.CreateWithNestedWorkflowSupport();
        
        var switchStep = new SwitchStep(
                name: "RoleRouter",
                selector: ctx => ctx.Items.TryGetValue("UserRole", out var role) 
                               ? role?.ToString() 
                               : null,
                cases: [
                    new SwitchCase("admin", new FakeAgent("AdminStep", "Admin Processed")),
                    new SwitchCase("user",  new FakeAgent("UserStep", "User Processed"))
                ],
                defaultStep: new RunStep(new FakeAgent("GuestStep", "Guest Processed")));

        var workflow = new Workflow("TestWorkflow")
            .Add(switchStep);

        var context = new PipelineContext();
        context.Items["UserRole"] = "admin";

        var result = await runtime.ExecuteAsync(workflow, context);

        result.Steps.Should().ContainSingle();

        var step = result.Steps.Single();

        step.StepName.Should().Be(switchStep.Name);

        step.Success.Should().BeTrue();
        step.Output.Should().Be("Admin Processed"); 
        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Be("Admin Processed");
    }

    [Fact]
    public async Task Workflow_Should_Execute_Switch_Step()
    {
        var runtime = WorkflowTestRuntimeFactory.CreateWithNestedWorkflowSupport();

        var workflow = new Workflow("TestWorkflow")
            .Add(new SwitchStep(
                name: "RoleRouter",
                selector: ctx => ctx.Items.TryGetValue("UserRole", out var role) 
                            ? role?.ToString() 
                            : null,
                cases: [
                    new SwitchCase("admin", new FakeAgent("AdminStep", "Admin Processed")),
                    new SwitchCase("user",  new FakeAgent("UserStep", "User Processed"))
                ],
                defaultStep: new RunStep(new FakeAgent("GuestStep", "Guest Processed"))));

        var context = new PipelineContext();
        context.Items["UserRole"] = "admin";

        var result = await runtime.ExecuteAsync(workflow, context);

        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Be("Admin Processed");
    }

    [Fact]
    public async Task Workflow_Should_Execute_Default_Switch_Step()
    {
        var runtime = WorkflowTestRuntimeFactory.CreateWithNestedWorkflowSupport();

        var workflow = new Workflow("TestWorkflow")
            .Add(new FakeAgent("Step1", "Started"))
            .Add(new SwitchStep(
                name: "RoleRouter",
                selector: ctx => ctx.Items.TryGetValue("UserRole", out var role) 
                               ? role?.ToString() 
                               : "guest",
                cases: [
                    new SwitchCase("admin", new FakeAgent("AdminStep", "Admin Path"))
                ],
                defaultStep: new RunStep(new FakeAgent("DefaultStep", "Default Path"))));

        var context = new PipelineContext();
        context.Items["UserRole"] = "manager"; // no match

        var result = await runtime.ExecuteAsync(workflow, context);

        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Be("Default Path");
    }

    [Fact]
    public async Task SwitchStep_Should_Handle_Null_Selector_Result()
    {
        var runtime = WorkflowTestRuntimeFactory.CreateWithNestedWorkflowSupport();

        var workflow = new Workflow("TestWorkflow")
            .Add(new FakeAgent("Step1", "Initial Output"))
            .Add(new SwitchStep(
                name: "NullSelectorSwitch",
                selector: _ => null,                    // Explicitly returns null
                cases: [
                    new SwitchCase("Approved", new FakeAgent("Approver", "Approved")),
                    new SwitchCase("Rejected", new FakeAgent("Rejector", "Rejected"))
                ],
                defaultStep: new RunStep(new FakeAgent("DefaultHandler", "Default Path Executed"))));

        var context = new PipelineContext();
        var result = await runtime.ExecuteAsync(workflow, context);

        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Be("Default Path Executed");
    }

    [Fact]
    public async Task SwitchStep_Should_Handle_Null_Selector_Result_Without_Default()
    {
        var runtime = WorkflowTestRuntimeFactory.CreateWithNestedWorkflowSupport();

        var workflow = new Workflow("TestWorkflow")
            .Add(new FakeAgent("Step1", "Initial Output"))
            .Add(new SwitchStep(
                name: "NullSelectorSwitch",
                selector: _ => null,           // Returns null
                cases: [
                    new SwitchCase("Approved", new FakeAgent("Approver", "Approved"))
                ])); // No defaultStep

        var context = new PipelineContext();
        var result = await runtime.ExecuteAsync(workflow, context);

        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Be("Initial Output"); // Context unchanged
    }    

    [Fact]
    public async Task SwitchStep_Should_Handle_Null_From_Context_Selector()
    {
        var runtime = WorkflowTestRuntimeFactory.CreateWithNestedWorkflowSupport();

        var workflow = new Workflow("TestWorkflow")
            .Add(new RunStep(new FakeAgent("Step1", "Started")))
            .Add(new SwitchStep(
                name: "RoleSwitch",
                selector: ctx => ctx.Items.TryGetValue("UserRole", out var role) 
                            ? role?.ToString() 
                            : null,
                cases: [
                    new SwitchCase("admin", new RunStep(new FakeAgent("AdminStep", "Admin Path")))
                ],
                defaultStep: new RunStep(new FakeAgent("DefaultStep", "Fallback Path"))));

        var context = new PipelineContext();
        // "UserRole" key is missing → selector returns null

        var result = await runtime.ExecuteAsync(workflow, context);

        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Be("Fallback Path");
    }    

    [Fact]
    public async Task SwitchStep_Should_Select_First_Matching_Case_When_Duplicates_Exist()
    {
        var runtime = WorkflowTestRuntimeFactory.CreateWithNestedWorkflowSupport();

        var firstCase  = new RunStep(new FakeAgent("FirstApprover",  "First Approved Path"));
        var secondCase = new RunStep(new FakeAgent("SecondApprover", "Second Approved Path"));

        var switchStep = new SwitchStep(
                name: "DuplicateCaseSwitch",
                selector: _ => "Approved",
                cases: [
                    new SwitchCase("Approved", firstCase),
                    new SwitchCase("Rejected", new RunStep(new FakeAgent("Rejector", "Rejected"))),
                    new SwitchCase("Approved", secondCase)   // Duplicate
                ]);
        var workflow = new Workflow("TestWorkflow")
            .Add(new RunStep(new FakeAgent("Step1", "Initial Output")))
            .Add(switchStep);

        var result = await runtime.ExecuteAsync(workflow, new PipelineContext());

        result.Steps.Should().HaveCount(2);

        result.Steps[0].StepName.Should().Be("Step1");

        result.Steps[1].StepName.Should().Be("DuplicateCaseSwitch");
        result.Steps[1].Success.Should().BeTrue();
        result.Steps[1].Output.Should().Be("First Approved Path");

        result.Success.Should().BeTrue();
        result.FinalOutput.Should().Be("First Approved Path");

    }    
}
