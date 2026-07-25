using Xunit;
using FluentAssertions;
using  PulseStack.Abstractions.WorkflowPackages.Identity;


namespace PulseStack.Tests.WorkflowPackages.Identity;
public class WorkflowPackageIdentityTests
{
    [Fact]
    public void ShouldStoreIdAndVersion()
    {
        // Arrange
        var id = WorkflowPackageId.New();
        const string version = "2.3.1";

        // Act
        var identity = new WorkflowPackageIdentity(id, version);

        // Assert
        identity.Id.Should().Be(id);
        identity.Version.Should().Be(version);
    }

    [Fact]
    public void ShouldUseDefaultVersion()
    {
        var identity = new WorkflowPackageIdentity(
            WorkflowPackageId.New());

        identity.Version.Should().Be("1.0.0");
    }
}