using System.Text.Json;

namespace PulseStack.Core.WorkflowPackages.Packaging;

internal static class WorkflowPackageJsonOptions
{
    public static readonly JsonSerializerOptions Default =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
}