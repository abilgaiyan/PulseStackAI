using Xunit;
using FluentAssertions;
using PulseStack.Abstractions.WorkflowPackages;

namespace PulseStack.Tests.WorkflowPackages;

public class WorkflowPackageMetadataTests
{
    [Fact]
    public void ShouldInitializeWithDefaults()
    {
        // Act
        var metadata = new WorkflowPackageMetadata();

        // Assert
        metadata.Title.Should().BeEmpty();
        metadata.Description.Should().BeEmpty();
        metadata.Author.Should().BeEmpty();
        metadata.License.Should().BeEmpty();
        metadata.Tags.Should().NotBeNull().And.BeEmpty();
        
    }

    [Fact]
    public void ShouldAllowCustomMetadata()
    {
        // Arrange
        var tags = new List<string> { "automation", "finance", "premium" };

        // Act
        var metadata = new WorkflowPackageMetadata
        {
            Title = "Invoice Processor",
            Description = "Automates invoice approval workflow",
            Author = "PulseStack Team",
            License = "MIT",
            Tags = ["automation", "finance", "premium"]
        };

        // Assert
        metadata.Title.Should().Be("Invoice Processor");
        metadata.Description.Should().Be("Automates invoice approval workflow");
        metadata.Author.Should().Be("PulseStack Team");
        metadata.License.Should().Be("MIT");
        metadata.Tags.Should().BeEquivalentTo(tags);
    }
}