using Xunit;
using FluentAssertions;
using PulseStack.Abstractions.WorkflowPackages;

namespace PulseStack.Tests.WorkflowPackages;

public class WorkflowPackageManifestTests
{
    [Fact]
    public void ShouldInitializeWithDefaultValues()
    {
    // Act
        var manifest = new WorkflowPackageManifest();

        // Assert
        manifest.PackageFormatVersion.Should().Be("1.0");
        manifest.MinimumRuntimeVersion.Should().Be("0.8.0");
        manifest.EntryWorkflow.Should().Be("workflow.json");
        manifest.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
       
    }
}