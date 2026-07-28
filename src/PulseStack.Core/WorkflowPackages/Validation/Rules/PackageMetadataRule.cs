using PulseStack.Abstractions.WorkflowPackages;

namespace PulseStack.Core.WorkflowPackages.Validation.Rules;
internal static class PackageMetadataRule
{
    public static void Validate(
        WorkflowPackage package,
        WorkflowPackageValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(context);

        if (package.Metadata is null)
        {
            context.Add(
                WorkflowPackageDiagnosticDescriptors.PackageMetadataMissing);
        }
    }
}