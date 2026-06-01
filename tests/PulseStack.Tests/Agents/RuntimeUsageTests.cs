using FluentAssertions;
using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Runtime.Usage;
using PulseStack.Agents.Runtime;
using PulseStack.Agents.Runtime.Costing;
using PulseStack.Agents.Runtime.Usage;
using Xunit;

namespace PulseStack.Tests.Agents;

public class RuntimeUsageTests
{
    [Fact]
    public void UsageAggregator_Should_Combine_Token_Counts()
    {
        var aggregator = new UsageAggregator();

        var usage = aggregator.Aggregate(
        [
            new AIUsage
            {
                Provider = "OpenAI",
                Model = "GPT-5",
                PromptTokens = 1_200,
                CompletionTokens = 300
            },
            new AIUsage
            {
                Provider = "OpenAI",
                Model = "GPT-5",
                PromptTokens = 800,
                CompletionTokens = 200
            },
            null
        ]);

        usage.Provider.Should().Be("OpenAI");
        usage.Model.Should().Be("GPT-5");
        usage.PromptTokens.Should().Be(2_000);
        usage.CompletionTokens.Should().Be(500);
        usage.TotalTokens.Should().Be(2_500);
    }

    [Fact]
    public void UsageAggregator_Should_Mark_Mixed_Providers_And_Models()
    {
        var aggregator = new UsageAggregator();

        var usage = aggregator.Aggregate(
        [
            new AIUsage
            {
                Provider = "OpenAI",
                Model = "GPT-5",
                PromptTokens = 1
            },
            new AIUsage
            {
                Provider = "Groq",
                Model = "llama",
                CompletionTokens = 2
            }
        ]);

        usage.Provider.Should().Be("Multiple");
        usage.Model.Should().Be("Multiple");
        usage.TotalTokens.Should().Be(3);
    }

    [Fact]
    public void UsageExtractorRegistry_Should_Extract_ChatResponse_Usage()
    {
        var registry =
            UsageExtractorRegistry.CreateDefault();

        var response =
            new ChatResponse(
                new ChatMessage(
                    ChatRole.Assistant,
                    "done"))
            {
                ModelId = "test-model",
                Usage = new UsageDetails
                {
                    InputTokenCount = 12,
                    OutputTokenCount = 5
                }
            };

        var usage =
            registry.Extract(
                response,
                new UsageExtractionContext
                {
                    Provider = "TestProvider",
                    Model = "fallback-model"
                });

        usage.Should().NotBeNull();
        usage!.Provider.Should().Be("TestProvider");
        usage.Model.Should().Be("test-model");
        usage.PromptTokens.Should().Be(12);
        usage.CompletionTokens.Should().Be(5);
        usage.TotalTokens.Should().Be(17);
    }

    [Fact]
    public async Task PipelineRuntime_Should_Aggregate_Response_Usage()
    {
        var runtime =
            new PipelineRuntime();

        var context =
            new PipelineContext
            {
                Input = "input",
                CurrentOutput = "input"
            };

        var result =
            await runtime.ExecuteAsync(
                "UsagePipeline",
                [
                    new UsageAgent(
                        "First",
                        "first",
                        "model-a",
                        10,
                        2),
                    new UsageAgent(
                        "Second",
                        "second",
                        "model-a",
                        4,
                        1)
                ],
                context,
                new SequentialPipelineExecutionStrategy(),
                new PipelineExecutionPolicy(),
                CancellationToken.None);

        result.TotalUsage.Model.Should().Be("model-a");
        result.TotalUsage.PromptTokens.Should().Be(14);
        result.TotalUsage.CompletionTokens.Should().Be(3);
        result.TotalUsage.TotalTokens.Should().Be(17);
    }

    [Fact]
    public void CostCalculator_Should_Calculate_Per_Million_Token_Costs()
    {
        var calculator = new CostCalculator();

        var cost = calculator.Calculate(
            new AIUsage
            {
                Provider = "OpenAI",
                Model = "GPT-5",
                PromptTokens = 1_200,
                CompletionTokens = 300
            },
            new ModelPricing
            {
                Provider = "OpenAI",
                Model = "GPT-5",
                InputPricePerMillionTokens = 1.25m,
                OutputPricePerMillionTokens = 10.00m
            });

        cost.PromptCost.Should().Be(0.0015m);
        cost.CompletionCost.Should().Be(0.003m);
        cost.TotalCost.Should().Be(0.0045m);
        cost.Currency.Should().Be("USD");
    }

    private sealed class UsageAgent : IAgent
    {
        private readonly string _response;
        private readonly long _promptTokens;
        private readonly long _completionTokens;

        public UsageAgent(
            string name,
            string response,
            string model,
            long promptTokens,
            long completionTokens)
        {
            Name = name;
            _response = response;
            Model = model;
            _promptTokens = promptTokens;
            _completionTokens = completionTokens;
        }

        public string Name { get; }

        public string? Model { get; }

        public Task<ChatResponse> RunAsync(
            string input,
            CancellationToken cancellationToken = default)
        {
            var context =
                new PipelineContext
                {
                    Input = input,
                    CurrentOutput = input
                };

            return RunAsync(
                context,
                cancellationToken);
        }

        public Task<ChatResponse> RunAsync(
            PipelineContext context,
            CancellationToken cancellationToken = default)
        {
            context.CurrentOutput = _response;

            return Task.FromResult(
                new ChatResponse(
                    new ChatMessage(
                        ChatRole.Assistant,
                        _response))
                {
                    ModelId = Model,
                    Usage = new UsageDetails
                    {
                        InputTokenCount = _promptTokens,
                        OutputTokenCount = _completionTokens
                    }
                });
        }

        public async IAsyncEnumerable<string> StreamAsync(
            string input,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            yield return _response;

            await Task.CompletedTask;
        }
    }
}
