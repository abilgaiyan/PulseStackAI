using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Abstractions.Persistence.Documents;
using PulseStack.Abstractions.Persistence.Mapping;

namespace PulseStack.Core.Persistence.Mapping;

public sealed class WorkflowMapper : IWorkflowMapper
{
    public WorkflowDocument ToDocument(Workflow workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        return new WorkflowDocument
        {
            Schema = "pulsestack.workflow",
            SchemaVersion = "1.0",

            Identity = workflow.Identity,
            Definition = workflow.Definition,

            Steps = workflow.Steps
                .Select(MapStep)
                .ToList()
        };
    }

   public Workflow FromDocument(
        WorkflowDocument document,
        IAgentResolver agentResolver)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(agentResolver);

        var workflow = new Workflow(
            document.Identity,
            document.Definition);

        foreach (var step in document.Steps)
        {
            workflow.Add(BuildStep(step, agentResolver));
        }

        return workflow;
    }

    private WorkflowStepDocument MapStep(IWorkflowStep step)
    {
        return step switch
        {
            RunStep run => MapRunStep(run),

            _ => throw new NotSupportedException(
                $"Workflow step '{step.GetType().Name}' is not supported.")
        };
    }

    private IWorkflowStep BuildStep(
        WorkflowStepDocument document,
        IAgentResolver resolver)
    {
        return document switch
        {
            RunStepDocument run => BuildRunStep(run, resolver),

            _ => throw new NotSupportedException(
                $"Workflow step '{document.Kind}' is not supported.")
        };
    }

   private RunStepDocument MapRunStep(RunStep step)
   {
        return new RunStepDocument
        {
            Id = step.Id,
            Kind = WorkflowStepKinds.Run,
            Name = step.Name,
            AgentReference = step.Agent.Name,
            Children = step.Children
                .Select(MapStep)
                .ToList()
        };
   }

    private static RunStep BuildRunStep(
        RunStepDocument document,
        IAgentResolver resolver)
    {
        var agent = resolver.Resolve(document.AgentReference);

        return new RunStep(agent);
    }
}
