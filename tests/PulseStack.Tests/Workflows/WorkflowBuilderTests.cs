using FluentAssertions;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Abstractions.Workflows.Conditions;
using PulseStack.Abstractions.Workflows.Routing;
using PulseStack.Tests.Fakes;
using Xunit;

namespace PulseStack.Tests.Workflows;

public class WorkflowBuilderTests
{
    [Fact]
    public void WorkflowBuilder_Should_Create_Workflow()
    {
        var builder =
            Workflow.Create(
                "Research");

        var workflow =
            builder.Build();

        workflow.Name.Should().Be("Research");
        workflow.Steps.Should().BeEmpty();
    }

    [Fact]
    public void WorkflowBuilder_Should_Add_Agent()
    {
        var agent =
            new FakeAgent(
                "Researcher",
                "Done");

        var workflow =
            Workflow.Create(
                    "Research")
                .Run(agent)
                .Build();

        var steps = workflow.Steps.OfType<RunStep>().ToList(); 
        steps.Should().ContainSingle();
        steps[0].Agent.Should().BeSameAs(agent);
    }

    [Fact]
    public void WorkflowBuilder_Should_Add_Nested_Workflow()
    {
        var childWorkflow =
            new Workflow(
                "Child");

        var workflow =
            Workflow.Create(
                    "Parent")
                .Workflow(childWorkflow)
                .Build();

        workflow.Steps.Should().ContainSingle();
        workflow.Steps.Single().Should().BeSameAs(childWorkflow);
    }

    [Fact]
    public void WorkflowBuilder_Should_Return_WorkflowDefinition()
    {
        var workflow =
            Workflow.Create(
                    "Research")
                .Build();

        workflow.Should().BeOfType<Workflow>();
    }

    [Fact]
    public void Build_Should_Return_Same_Workflow()
    {

        var builder =
            Workflow.Create("Research");

        var workflow1 =
            builder.Build();

        var workflow2 =
            builder.Build();

        workflow1.Should().BeSameAs(workflow2);
    }

    [Fact]
    public void Builder_Should_Support_Chaining()
    {
        var workflow =
            Workflow.Create("Research")
                .Run(new FakeAgent("A", "Done"))
                .Run(new FakeAgent("B", "Done"))
                .Build();

        workflow.Steps.Should().HaveCount(2);
    }

    [Fact]
    public void Workflow_Create_Should_Throw_When_Name_Is_Empty()
    {
        Action action =
            () => Workflow.Create("");

        action.Should()
            .Throw<ArgumentException>();
    }

    [Fact]
    public void Workflow_Create_Should_Throw_When_Name_Is_Null()
    {
        Action action =
            () => Workflow.Create(null!);

        action.Should()
            .Throw<ArgumentException>();
    }

    [Fact]
    public void WorkflowBuilder_If_Should_Throw_When_Condition_Is_Null()
    {
        Action action = () => Workflow.Create("Test")
            .If(null!, new RunStep(new FakeAgent("Test", "Done")));

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WorkflowBuilder_If_Should_Throw_When_Step_Is_Null()
    {
        Action action = () => Workflow.Create("Test")
            .If(new DelegateCondition(_ => true), (IWorkflowStep)null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WorkflowBuilder_If_Should_Use_Default_Name()
    {
        var condition = new DelegateCondition(_ => true);
        var thenStep = new RunStep(new FakeAgent("ApprovalStep", "Approved"));

        var workflow = Workflow.Create("Research")
            .Run(new FakeAgent("ResearchStep", "Done"))
            .If(condition, thenStep)           // overload without name
            .Build();

        var conditional = workflow.Steps.OfType<ConditionalStep>().Single();
        conditional.Name.Should().Be("If");
        conditional.Condition.Should().BeSameAs(condition);
        conditional.Step.Should().BeSameAs(thenStep);
    }

    [Fact]
    public void WorkflowBuilder_If_Should_Use_Custom_Name()
    {
        var condition = new DelegateCondition(_ => true);
        var thenStep = new FakeAgent("ApprovalStep", "Approved");

        var workflow = Workflow.Create("Research")
            .If("IsUserApproved", condition, thenStep)   // overload with name
            .Build();

        var conditional = workflow.Steps.OfType<ConditionalStep>().Single();
        conditional.Name.Should().Be("IsUserApproved");
    }

    [Fact]
    public void WorkflowBuilder_Should_Support_Chaining_Both_Overloads()
    {
        var workflow = Workflow.Create("Test")
            .If(new DelegateCondition(_ => true), new FakeAgent("If1", "Path1"))
            .If("CheckPermission", new DelegateCondition(_ => false), new FakeAgent("If2", "Path2"))
            .Build();

        workflow.Steps.OfType<ConditionalStep>().Should().HaveCount(2);
    }

    [Fact]
    public void WorkflowBuilder_If_Should_Throw_When_Name_Is_Empty()
    {
        Action action =
            () => Workflow.Create("Test")
                .If(
                    "",
                    new DelegateCondition(_ => true),
                    new FakeAgent("A", "Done"));

        action.Should()
            .Throw<ArgumentException>();
    }

    [Fact]
    public void WorkflowBuilder_If_Should_Throw_When_Name_Is_Null()
    {
        Action action =
            () => Workflow.Create("Test")
                .If(
                    null!,
                    new DelegateCondition(_ => true),
                    new FakeAgent("A", "Done"));

        action.Should()
            .Throw<ArgumentException>();
    }

    [Fact]
    public void WorkflowBuilder_Retry_Should_Use_Default_Name()
    {
        var agent = new FakeAgent("ValidationStep", "Done");

        var workflow = Workflow.Create("Research")
            .Retry(new RunStep(agent), 3)
            .Build();

        var retryStep = workflow.Steps.OfType<RetryStep>().Single();

        retryStep.Name.Should().Be("Retry");
        retryStep.MaxAttempts.Should().Be(3);
        retryStep.Step.As<RunStep>().Agent.Should().BeSameAs(agent);
    }

    [Fact]
    public void WorkflowBuilder_Retry_Should_Use_Custom_Name()
    {
        var agent = new FakeAgent("ValidationStep", "Done");

        var workflow = Workflow.Create("Research")
            .Retry("Retry Validation",new RunStep(agent), 5)
            .Build();

        var retryStep = workflow.Steps.OfType<RetryStep>().Single();

        retryStep.Name.Should().Be("Retry Validation");
        retryStep.MaxAttempts.Should().Be(5);
        retryStep.Step.As<RunStep>().Agent.Should().BeSameAs(agent);
    }

    [Fact]
    public void WorkflowBuilder_Retry_Should_Support_Chaining_Both_Overloads()
    {
        var workflow = Workflow.Create("Test")
            .Retry(new RunStep(new FakeAgent("A", "Done")), 2)
            .Retry("CustomRetry", new RunStep(new FakeAgent("B", "Done")), 4)
            .Build();

        workflow.Steps.OfType<RetryStep>().Should().HaveCount(2);
    }

    [Fact]
    public void WorkflowBuilder_Retry_Should_Throw_When_Step_Is_Null()
    {
        Action action = () => Workflow.Create("Test").Retry(null!, 3);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WorkflowBuilder_Retry_Should_Throw_When_Name_Is_Empty()
    {
        Action action = () => Workflow.Create("Test").Retry("", new RunStep(new FakeAgent("A", "Done")), 3);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WorkflowBuilder_Retry_Should_Throw_When_MaxAttempts_Is_Zero()
    {
        Action action = () => Workflow.Create("Test").Retry(new RunStep(new FakeAgent("A", "Done")), 0);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void WorkflowBuilder_Retry_Should_Throw_When_MaxAttempts_Is_Negative()
    {
        Action action = () => Workflow.Create("Test").Retry(new RunStep(new FakeAgent("A", "Done")), -1);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void WorkflowBuilder_Retry_Should_Use_Default_MaxAttempts()
    {
        var workflow =
            Workflow.Create("Test")
                .Retry(
                    new RunStep(new FakeAgent("A", "Done")))
                .Build();

        var retry =
            workflow.Steps
                .OfType<RetryStep>()
                .Single();

        retry.MaxAttempts.Should().Be(3);
    }

    [Fact]
    public void WorkflowBuilder_Retry_Should_Throw_When_Name_Is_Null()
    {
         Action action =
            () => Workflow.Create("Test")
                .Retry(null!,
                    new RunStep(new FakeAgent("A", "Done")));

        action.Should()
            .Throw<ArgumentException>();
    }

    [Fact]
    public void WorkflowBuilder_ForEach_Should_Use_Default_Name()
    {
        Func<PipelineContext, IEnumerable<object>> itemsSelector = 
            _ => [1, 2, 3 ];

        var processor = new RunStep(new FakeAgent("ProcessItem", "Processed"));

        var workflow = Workflow.Create("Research")
            .ForEach(itemsSelector, processor)
            .Build();

        var loopStep = workflow.Steps.OfType<LoopStep>().Single();

        loopStep.Name.Should().Be("ForEach");
        loopStep.Items.Should().BeSameAs(itemsSelector);
        loopStep.Step.Should().BeSameAs(processor);
    }

    [Fact]
    public void WorkflowBuilder_ForEach_Should_Use_Custom_Name()
    {
        Func<PipelineContext, IEnumerable<object>> itemsSelector = 
            _ => [];

        var processor = new RunStep(new FakeAgent("ProcessItem", "Processed"));

        var workflow = Workflow.Create("Research")
            .ForEach("Process Documents", itemsSelector, processor)
            .Build();

        var loopStep = workflow.Steps.OfType<LoopStep>().Single();

        loopStep.Name.Should().Be("Process Documents");
        loopStep.Items.Should().BeSameAs(itemsSelector);
        loopStep.Step.Should().BeSameAs(processor);
    }

    [Fact]
    public void WorkflowBuilder_ForEach_Should_Support_Chaining_Both_Overloads()
    {
        var workflow = Workflow.Create("Test")
            .ForEach(_ => [ "a", "b" ], new RunStep(new FakeAgent("A", "Done")))
            .ForEach("CustomLoop", _ => new[] { 1, 2 }.Select(x => (object)x), new RunStep(new FakeAgent("B", "Done")))
            .Build();

        workflow.Steps.OfType<LoopStep>().Should().HaveCount(2);
    }

    [Fact]
    public void WorkflowBuilder_ForEach_Should_Throw_When_Items_Is_Null()
    {
        Action action = () => Workflow.Create("Test").ForEach(null!, new RunStep(new FakeAgent("A", "Done")));
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WorkflowBuilder_ForEach_Should_Throw_When_Step_Is_Null()
    {
        Action action = () => Workflow.Create("Test").ForEach(_ => [0], null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WorkflowBuilder_ForEach_Should_Throw_When_Name_Is_Empty()
    {
        Action action = () => Workflow.Create("Test").ForEach("", _ => [0], new RunStep(new FakeAgent("A", "Done")));
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WorkflowBuilder_ForEach_Should_Throw_When_Name_Is_Null()
    {
        Action action = () => Workflow.Create("Test").ForEach(null!, _ => [0], new RunStep(new FakeAgent("A", "Done")));
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WorkflowBuilder_Parallel_Should_Use_Default_Name()
    {
        var step1 = new RunStep(new FakeAgent("A", "Done"));
        var step2 = new RunStep(new FakeAgent("B", "Done"));

        var workflow = Workflow.Create("Test")
            .Parallel(step1, step2)
            .Build();

        var parallel = workflow.Steps.OfType<ParallelStep>().Single();

        parallel.Name.Should().Be("Parallel");
        parallel.Steps.Should().HaveCount(2);
        parallel.Steps.Should().Contain(step1);
        parallel.Steps.Should().Contain(step2);
    }

    [Fact]
    public void WorkflowBuilder_Parallel_Should_Use_Custom_Name()
    {
        var step1 = new RunStep(new FakeAgent("Summarizer", "Done"));
        var step2 = new RunStep(new FakeAgent("Classifier", "Done"));

        var workflow = Workflow.Create("Test")
            .Parallel("Analysis", step1, step2)
            .Build();

        var parallel = workflow.Steps.OfType<ParallelStep>().Single();
        parallel.Name.Should().Be("Analysis");
        parallel.Steps.Should().HaveCount(2);
    }

    [Fact]
    public void WorkflowBuilder_Parallel_Should_Support_Chaining_Both_Overloads()
    {
        var workflow = Workflow.Create("Test")
            .Parallel(new RunStep(new FakeAgent("A", "Done")))
            .Parallel("Custom", new RunStep(new FakeAgent("B", "Done")), new RunStep(new FakeAgent("C", "Done")))
            .Build();

        workflow.Steps.OfType<ParallelStep>().Should().HaveCount(2);
    }

    [Fact]
    public void WorkflowBuilder_Parallel_Should_Throw_When_Array_Is_Empty()
    {
        Action action = () => Workflow.Create("Test")
            .Parallel(Array.Empty<IWorkflowStep>());
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WorkflowBuilder_Parallel_Should_Throw_When_Step_Is_Null()
    {
        Action action = () => Workflow.Create("Test").Parallel(new RunStep(new FakeAgent("A", "Done")), null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WorkflowBuilder_Parallel_Should_Throw_When_Name_Is_Empty()
    {
        Action action = () => Workflow.Create("Test").Parallel("", new RunStep(new FakeAgent("A", "Done")));
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WorkflowBuilder_Parallel_Should_Throw_When_Name_Is_Null()
    {
        Action action =
            () => Workflow.Create("Test")
                .Parallel(
                    name:null!,
                    steps: new RunStep(new FakeAgent("A", "Done")));

        action.Should()
            .Throw<ArgumentException>();
    }

    [Fact]
    public void WorkflowBuilder_Parallel_Should_Preserve_Step_Order()
    {
        var step1 =
            new RunStep(new FakeAgent("A", "Done"));

        var step2 =
            new RunStep(new FakeAgent("B", "Done"));

        var step3 =
            new RunStep(new FakeAgent("C", "Done"));

        var workflow =
            Workflow.Create("Test")
                .Parallel(step1, step2, step3)
                .Build();

        var parallel =
            workflow.Steps
                .OfType<ParallelStep>()
                .Single();

        parallel.Steps.Should().ContainInOrder(
            step1,
            step2,
            step3);
    }

    [Fact]  
    public void WorkflowBuilder_Switch_Should_Use_Default_Name()
    {
        var cases = new[]
        {
            new SwitchCase("Approved", new FakeAgent("Approve", "Approved")),
            new SwitchCase("Rejected", new FakeAgent("Reject", "Rejected"))
        };

        var workflow = Workflow.Create("Test")
            .Switch(ctx => "Approved", cases)
            .Build();

        var switchStep = workflow.Steps.OfType<SwitchStep>().Single();

        switchStep.Name.Should().Be("Switch");
        switchStep.Selector.Should().NotBeNull();
        switchStep.Cases.Should().BeEquivalentTo(cases);
        switchStep.DefaultStep.Should().BeNull();
    }

    [Fact]
    public void WorkflowBuilder_Switch_Should_Use_Custom_Name()
    {
        var cases = new[]
        {
            new SwitchCase("admin", new FakeAgent("AdminPath", "Admin"))
        };

        var defaultStep = new RunStep(new FakeAgent("Default", "Default Path"));

        var workflow = Workflow.Create("Test")
            .Switch("UserRoleRouter", ctx => ctx.Items["Role"]?.ToString(), cases, defaultStep)
            .Build();

        var switchStep = workflow.Steps.OfType<SwitchStep>().Single();

        switchStep.Name.Should().Be("UserRoleRouter");
        switchStep.DefaultStep.Should().BeSameAs(defaultStep);
    }

    [Fact]
    public void WorkflowBuilder_Switch_Should_Preserve_Cases()
    {
        var case1 = new SwitchCase("yes", new FakeAgent("Yes", "Yes"));
        var case2 = new SwitchCase("no", new FakeAgent("No", "No"));

        var workflow = Workflow.Create("Test")
            .Switch("Decision", _ => "yes", new[] { case1, case2 })
            .Build();

        var switchStep = workflow.Steps.OfType<SwitchStep>().Single();
        switchStep.Cases.Should().ContainInOrder(case1, case2);
    }

    [Fact]
    public void WorkflowBuilder_Switch_Should_Support_Chaining_Both_Overloads()
    {
        var cases = new[] { new SwitchCase("A", new FakeAgent("A", "Done")) };

        var workflow = Workflow.Create("Test")
            .Switch(ctx => "A", cases)
            .Switch("Another", ctx => "B", cases)
            .Build();

        workflow.Steps.OfType<SwitchStep>().Should().HaveCount(2);
    }

    [Fact]
    public void WorkflowBuilder_Switch_Should_Throw_When_Selector_Is_Null()
    {
        Action action = () => Workflow.Create("Test").Switch(null!, new List<SwitchCase>());
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WorkflowBuilder_Switch_Should_Throw_When_Cases_Is_Null()
    {
        Action action = () => Workflow.Create("Test").Switch(_ => "test", null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WorkflowBuilder_Switch_Should_Throw_When_Cases_Is_Empty()
    {
        Action action = () => Workflow.Create("Test").Switch(_ => "test", new List<SwitchCase>());
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WorkflowBuilder_Switch_Should_Throw_When_Name_Is_Empty()
    {
        var cases = new[] { new SwitchCase("A", new FakeAgent("A", "Done")) };
        Action action = () => Workflow.Create("Test").Switch("", _ => "A", cases);
        action.Should().Throw<ArgumentException>();
    }    

    [Fact]
    public void WorkflowBuilder_Switch_Should_Throw_When_Name_Is_Null()
    {
        var cases = new[]
        {
            new SwitchCase("A", new RunStep(new FakeAgent("A", "Done")))
        };

        Action action = () => Workflow.Create("Test")
            .Switch(
                null!,           // name = null
                _ => "A",
                cases);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WorkflowBuilder_Switch_Should_Preserve_Default_Step()
    {
        var defaultStep =
            new RunStep(
                new FakeAgent(
                    "Default",
                    "Done"));

        var workflow =
            Workflow.Create("Test")
                .Switch(
                    _ => "Unknown",
                    [
                        new SwitchCase(
                            "A",
                            new RunStep(new FakeAgent("A", "Done")))
                    ],
                    defaultStep)
                .Build();

        var step =
            workflow.Steps
                .OfType<SwitchStep>()
                .Single();

        step.DefaultStep
            .Should()
            .BeSameAs(defaultStep);
    }
}
