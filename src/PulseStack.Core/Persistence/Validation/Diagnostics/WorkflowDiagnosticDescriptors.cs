
namespace PulseStack.Core.Persistence.Validation.Diagnostics;

public static class WorkflowDiagnosticDescriptors
{
    // Identity (WF1xx)

    public static readonly WorkflowDiagnosticDescriptor WorkflowIdMissing =
        new(
            "WF100",
            WorkflowDiagnosticCategory.Identity,
            "Workflow Id is required.");

    public static readonly WorkflowDiagnosticDescriptor WorkflowVersionMissing =
        new(
            "WF101",
            WorkflowDiagnosticCategory.Identity,
            "Workflow version is required.");

    // Definition (WF2xx)

    public static readonly WorkflowDiagnosticDescriptor WorkflowNameMissing =
        new(
            "WF200",
            WorkflowDiagnosticCategory.Definition,
            "Workflow name is required.");

    // Structure (WF3xx)

    public static readonly WorkflowDiagnosticDescriptor EmptyWorkflow =
        new(
            "WF300",
            WorkflowDiagnosticCategory.Structure,
            "Workflow must contain at least one step.");

    public static readonly WorkflowDiagnosticDescriptor StepIdMissing =
        new(
            "WF301",
            WorkflowDiagnosticCategory.Structure,
            "Workflow step Id is required.");

    public static readonly WorkflowDiagnosticDescriptor DuplicateStepId =
        new(
            "WF302",
            WorkflowDiagnosticCategory.Structure,
            "Duplicate workflow step Id.");

    public static readonly WorkflowDiagnosticDescriptor StepNameMissing =
        new(
            "WF303",
            WorkflowDiagnosticCategory.Structure,
            "Workflow step name is required.");

    // References (WF4xx)

    public static readonly WorkflowDiagnosticDescriptor InvalidStepReference =
        new(
            "WF400",
            WorkflowDiagnosticCategory.Reference,
            "Workflow contains an invalid step reference.");
}
