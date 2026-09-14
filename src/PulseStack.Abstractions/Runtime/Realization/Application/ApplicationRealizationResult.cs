using PulseStack.Abstractions.Workflows;

namespace PulseStack.Abstractions.Runtime.Realization.Application;

public abstract class ApplicationRealizationResult
{
    private ApplicationRealizationResult()
    {
    }

    public sealed class Success : ApplicationRealizationResult
    {
        public Success(Workflow workflow)
        {
            ArgumentNullException.ThrowIfNull(workflow);
            Workflow = workflow;
        }

        public Workflow Workflow { get; }
    }

    public sealed class UnsupportedRoot : ApplicationRealizationResult
    {
        public UnsupportedRoot(ApplicationRealizationUnsupportedRootContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            Context = context;
        }

        public ApplicationRealizationUnsupportedRootContext Context { get; }
    }

    public sealed class EntryWorkflowUnresolved : ApplicationRealizationResult
    {
        public EntryWorkflowUnresolved(ApplicationRealizationEntryWorkflowUnresolvedContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            Context = context;
        }

        public ApplicationRealizationEntryWorkflowUnresolvedContext Context { get; }
    }

    public sealed class EntryWorkflowTypeIncoherent : ApplicationRealizationResult
    {
        public EntryWorkflowTypeIncoherent(
            ApplicationRealizationEntryWorkflowTypeIncoherentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            Context = context;
        }

        public ApplicationRealizationEntryWorkflowTypeIncoherentContext Context { get; }
    }
}
