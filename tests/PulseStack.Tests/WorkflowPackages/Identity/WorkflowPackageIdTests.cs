using Xunit;
using FluentAssertions;
using PulseStack.Abstractions.WorkflowPackages.Identity;

namespace PulseStack.Tests.WorkflowPackages.Identity;
public class WorkflowPackageIdTests
{
    [Fact]
    public void New_ShouldGenerateNonEmptyId()
    {
        // Act
        var id = WorkflowPackageId.New();

        // Assert
        id.Value.Should().NotBe(Guid.Empty);
        id.IsEmpty.Should().BeFalse();
        id.Should().NotBe(WorkflowPackageId.Empty);
    }
    [Fact]
    public void Empty_ShouldBeEmpty()
    {
        // Assert
        WorkflowPackageId.Empty.Value.Should().Be(Guid.Empty);
        WorkflowPackageId.Empty.IsEmpty.Should().BeTrue();
    }
    [Fact]
    public void EnsureValid_ShouldThrowForEmptyId()
    {
        // Arrange
        var emptyId = WorkflowPackageId.Empty;

        // Act & Assert
        Action act = () => emptyId.EnsureValid();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*empty*");
    }

    [Fact]
    public void ExplicitOperator_ShouldCreateWorkflowPackageId()
    {
        var guid = Guid.NewGuid();

        var id = (WorkflowPackageId)guid;

        id.Value.Should().Be(guid);
    }    

    [Fact]
    public void ImplicitOperator_ShouldReturnGuid()
    {
        var id = WorkflowPackageId.New();

        Guid guid = id;

        guid.Should().Be(id.Value);
    }
}