using FluentAssertions;
using PulseStack.Tests.Fakes;
using PulseStack.Agents.Pipelines;
using Xunit;

namespace PulseStack.Tests.Agents;

public class AgentPipelineTests
{
    [Fact]
    public async Task Pipeline_Should_Execute_All_Agents()
    {
        var pipeline = new AgentPipeline("Test");

        pipeline.AddAgent(new FakeAgent(
            "Researcher",
            ["Research result"]));

        pipeline.AddAgent(new FakeAgent(
            "Writer",
            ["Final summary"]));

        var result = await pipeline.RunAsync("AI");

        result.FinalOutput.Should().Be("Final summary");
    }
}