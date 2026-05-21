using FluentAssertions;
using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;
using PulseStack.Agents.Runtime;
using Xunit;

namespace PulseStack.Tests.Agents;

public class AgentExecutionContextTests
{
    [Fact]
    public void CreateBranch_Should_Preserve_ExecutionId_And_Create_Branch_Diagnostics()
    {
        var context = new AgentExecutionContext(
            new PipelineContext
            {
                Input = "input",
                CurrentOutput = "input"
            },
            new List<ChatMessage>(),
            CancellationToken.None);

        context.Metadata["pipeline"] = "parallel";

        var branch = context.CreateBranch();

        context.BranchId.Should().BeNull();
        branch.BranchId.Should().NotBeNull();
        branch.ExecutionId.Should().Be(context.ExecutionId);
        branch.StartedAt.Should().BeOnOrAfter(context.StartedAt);
        branch.Metadata.Should().NotBeSameAs(context.Metadata);
        branch.Metadata["pipeline"].Should().Be("parallel");
    }
}
