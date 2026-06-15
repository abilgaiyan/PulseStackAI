using FluentAssertions;
using PulseStack.Abstractions.Agents;
using PulseStack.Agents.Pipelines;
using PulseStack.Abstractions.Runtime.Pipeline;
using Xunit;

namespace PulseStack.Tests.Agents;
public class PipelineNodeTests
{
    [Fact]
    public void Agent_Should_Implement_IPipelineNode()
    {
        typeof(IAgent)
            .GetInterfaces()
            .Should()
            .Contain(typeof(IPipelineNode));
    }

    [Fact]
    public void SequentialPipeline_Should_Implement_IPipelineNode()
    {
        typeof(SequentialPipeline)
            .GetInterfaces()
            .Should()
            .Contain(typeof(IPipelineNode));
    }

    [Fact]
    public void RouterPipeline_Should_Implement_IPipelineNode()
    {
        typeof(RouterPipeline)
            .GetInterfaces()
            .Should()
            .Contain(typeof(IPipelineNode));
    }

    [Fact]
    public void ParallelPipeline_Should_Implement_IPipelineNode()
    {
        typeof(ParallelPipeline)
            .GetInterfaces()
            .Should()
            .Contain(typeof(IPipelineNode));
    }

    [Fact]
    public void ConditionalPipeline_Should_Implement_IPipelineNode()
    {
        typeof(ConditionalPipeline)
            .GetInterfaces()
            .Should()
            .Contain(typeof(IPipelineNode));
    }
}