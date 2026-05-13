using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Chat;
using PulseStack.Agents.Builders;
using PulseStack.Agents.Pipelines;
using PulseStack.Core.DependencyInjection;
using PulseStack.Providers.OpenRouter.DependencyInjection;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var apiKey = configuration["OpenRouter:ApiKey"];

var services = new ServiceCollection();

services.AddPulseStack()
    .UseOpenRouter(apiKey!);

await using var provider =
    services.BuildServiceProvider();

var factory = provider
    .GetRequiredService<IChatClientFactory>();

// Planner
var planner = new AgentBuilder(
        "Planner",
        factory)
    .WithModel("meta-llama/llama-3.3-70b-instruct")
    .WithInstructions("""
        You are a planning agent.

        Break the problem into concise steps.

        Do not provide the final answer.
        """)
    .Build();

// Writer
var writer = new AgentBuilder(
        "Writer",
        factory)
    .WithModel("deepseek/deepseek-chat-v3-0324")
    .WithInstructions("""
        You are a professional technical writer.

        Use the provided planning steps
        to produce a polished final response.
        """)
    .Build();

var pipeline = new SequentialPipeline(
        "EnterpriseAgentsPipeline")
    .Add(planner)
    .Add(writer);

Console.WriteLine("PulseStackAI - Sequential Pipeline");
Console.WriteLine(new string('-', 50));

var result = await pipeline.RunAsync(
    """
    Explain how AI agents work
    in modern enterprise systems.
    """);

Console.WriteLine();
Console.WriteLine("=== Final Output ===");
Console.WriteLine(result.FinalOutput);

Console.WriteLine();
Console.WriteLine("=== Pipeline Steps ===");

foreach (var step in result.Steps)
{
    Console.WriteLine();
    Console.WriteLine($"Agent: {step.AgentName}");

    if (!string.IsNullOrWhiteSpace(step.Model))
    {
        Console.WriteLine($"Model: {step.Model}");
    }

    Console.WriteLine("Output:");
    Console.WriteLine(step.Output);
}