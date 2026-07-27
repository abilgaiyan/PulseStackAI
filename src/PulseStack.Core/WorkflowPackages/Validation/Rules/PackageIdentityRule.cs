using PulseStack.Abstractions.WorkflowPackages;

namespace PulseStack.Core.WorkflowPackages.Validation.Rules;
internal static class PackageIdentityRule
{
    public static void Validate(
        WorkflowPackage package,
        WorkflowPackageValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(context);

        if (package.Identity.Id.IsEmpty)
        {
            context.Add(
                WorkflowPackageDiagnosticDescriptors.PackageIdMissing);
        }

        if (string.IsNullOrWhiteSpace(package.Identity.Version))
        {
            context.Add(
                WorkflowPackageDiagnosticDescriptors.PackageVersionMissing);
        }
    }
}