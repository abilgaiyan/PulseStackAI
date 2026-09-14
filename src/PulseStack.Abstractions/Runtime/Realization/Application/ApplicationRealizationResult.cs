using PulseStack.Abstractions.Workflows;

namespace PulseStack.Abstractions.Runtime.Realization.Application;

public abstract record ApplicationRealizationResult
{
    private ApplicationRealizationResult()
    {
    }

    public sealed record Success : ApplicationRealizationResult
    {
        public Success(Workflow workflow)
        {
            ArgumentNullException.ThrowIfNull(workflow);
            Workflow = workflow;
        }

        public Workflow Workflow { get; }
    }

    public sealed record UnsupportedRoot : ApplicationRealizationResult
    {
        public UnsupportedRoot(ApplicationRealizationUnsupportedRootContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            Context = context;
        }

        public ApplicationRealizationUnsupportedRootContext Context { get; }
    }

    public sealed record EntryWorkflowUnresolved : ApplicationRealizationResult
    {
        public EntryWorkflowUnresolved(ApplicationRealizationEntryWorkflowUnresolvedContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            Context = context;
        }

        public ApplicationRealizationEntryWorkflowUnresolvedContext Context { get; }
    }

    public sealed record EntryWorkflowTypeIncoherent : ApplicationRealizationResult
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
