using FluentAssertions;
using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;
using PulseStack.Agents.Pipelines;
using Xunit;

namespace PulseStack.Tests.Agents;

public class AgentPipelineTests
{
    [Fact]
    public async Task Pipeline_Should_Execute_All_Agents()
    {
        var pipeline = new AgentPipeline("Test");

        pipeline.AddAgent(new FakeAgent("Agent1", "Step1"));
        pipeline.AddAgent(new FakeAgent("Agent2", "Step2"));

        var result = await pipeline.RunAsync("input");

        result.Steps.Should().HaveCount(2);

        result.FinalOutput.Should().Be("Step2");
    }
}

internal sealed class FakeAgent : IAgent
{
    private readonly string _response;

    public string Name { get; }

    public FakeAgent(string name, string response)
    {
        Name = name;
        _response = response;
    }

    public Task<ChatResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            new ChatResponse(
                new ChatMessage(ChatRole.Assistant, _response)));
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        yield return _response;
        await Task.CompletedTask;
    }
}