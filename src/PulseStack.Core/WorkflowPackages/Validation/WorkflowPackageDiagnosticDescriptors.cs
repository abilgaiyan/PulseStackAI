using PulseStack.Abstractions.WorkflowPackages.Validation;

namespace PulseStack.Core.WorkflowPackages.Validation;
internal static class WorkflowPackageDiagnosticDescriptors
{
    public static readonly WorkflowPackageDiagnostic PackageIdMissing =
        new(
            "PKG100",
            "Package Id is required.");

    public static readonly WorkflowPackageDiagnostic PackageVersionMissing =
        new(
            "PKG101",
            "Package version is required.");

    public static readonly WorkflowPackageDiagnostic PackageMetadataMissing =
            new(
                "PKG150",
                "Package metadata is required.");              

    public static readonly WorkflowPackageDiagnostic PackageFormatMissing =
        new(
            "PKG200",
            "Package format version is required.");

    public static readonly WorkflowPackageDiagnostic UnsupportedPackageFormat =
        new(
            "PKG201",
            "Unsupported package format version.");

    public static readonly WorkflowPackageDiagnostic EntryWorkflowMissing =
        new(
            "PKG300",
            "Entry workflow is required.");

    public static readonly WorkflowPackageDiagnostic WorkflowValidationFailed =
        new(
            "PKG400",
            "Workflow validation failed.");

}