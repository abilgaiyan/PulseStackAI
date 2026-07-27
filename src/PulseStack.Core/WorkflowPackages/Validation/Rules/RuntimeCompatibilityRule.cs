using PulseStack.Abstractions.WorkflowPackages;

namespace PulseStack.Core.WorkflowPackages.Validation.Rules;
internal static class RuntimeCompatibilityRule
{
    public static void Validate(
        WorkflowPackage package,
        WorkflowPackageValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(package.Identity.Version))
        {
            return;
        }

        // Future:
        // Compare package/runtime compatibility.
    }
}