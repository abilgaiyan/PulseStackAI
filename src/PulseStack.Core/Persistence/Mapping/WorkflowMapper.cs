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
            Schema = WorkflowDocumentSchema.Name,
            SchemaVersion = WorkflowDocumentSchema.Version,

            Identity = workflow.Identity,
            Id = workflow.Id, 
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

        if (document.Schema != WorkflowDocumentSchema.Name)
        {
            throw new NotSupportedException(
                $"Unsupported workflow schema '{document.Schema}'.");
        }

        if (document.SchemaVersion != WorkflowDocumentSchema.Version)
        {
            throw new NotSupportedException(
                $"Unsupported workflow schema version '{document.SchemaVersion}'.");
        }

        var workflow = new Workflow(
            document.Identity,
            document.Id,
            document.Definition);

        foreach (var step in document.Steps)
        {
            workflow.Add(BuildStep(step, agentResolver));
        }

        return workflow;
    }

    private WorkflowStepDocument MapStep(IWorkflowStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        return step switch
        {
            RunStep run => MapRunStep(run),

            _ => ThrowUnsupportedStep(step)
        };
    }

    private static WorkflowStepDocument ThrowUnsupportedStep(
        IWorkflowStep step)
    {
        throw new NotSupportedException(
            $"Workflow step '{step.GetType().FullName}' is not supported.");
    }

    private IWorkflowStep BuildStep(
        WorkflowStepDocument document,
        IAgentResolver resolver)
    {
        return document switch
        {
            RunStepDocument run => BuildRunStep(run, resolver),

            _ => ThrowUnsupportedKind(document)
        };
    }

     private static IWorkflowStep ThrowUnsupportedKind(
       WorkflowStepDocument document)
    {
        throw new NotSupportedException(
             $"Workflow document type '{document.GetType().FullName}' with kind '{document.Kind}' is not supported.");
    }


   private RunStepDocument MapRunStep(RunStep step)
   {
        return new RunStepDocument
        {
            Id = step.Id,
            Kind = WorkflowStepKinds.Run,
            Name = step.Name,
            AgentReference = step.Agent.Name,
            Children = MapChildren(step.Children)
        };
   }

    private static RunStep BuildRunStep(
        RunStepDocument document,
        IAgentResolver resolver)
    {
        var agent = resolver.Resolve(document.AgentReference);

        return new RunStep(agent);
    }

    private IReadOnlyList<WorkflowStepDocument> MapChildren(
        IEnumerable<IWorkflowStep> children)
    {
        ArgumentNullException.ThrowIfNull(children);

        return children.Select(MapStep).ToList();
    }

    private IReadOnlyList<IWorkflowStep> BuildChildren(
        IEnumerable<WorkflowStepDocument> children,
        IAgentResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(children);

        return children
            .Select(child => BuildStep(child, resolver))
            .ToList();
    }
    
}
