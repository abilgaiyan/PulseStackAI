using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Tools;
using PulseStack.Agents.Builders;
using PulseStack.Agents.Pipelines;
using PulseStack.Core.DependencyInjection;
using PulseStack.Providers.OpenAI.DependencyInjection;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var apiKey = config["OpenAI:ApiKey"];
var model = config["OpenAI:Model"] ?? "gpt-4o-mini";

var services = new ServiceCollection();

services.AddPulseStack()
    .UseOpenAI(apiKey!, model);

await using var sp = services.BuildServiceProvider();

var client = sp.GetRequiredService<IChatClient>();

var toolRegistry = sp.GetRequiredService<IToolRegistry>();
var toolExecutor = sp.GetRequiredService<IToolExecutor>();

// Research Agent
var researcher = new AgentBuilder("Researcher", client, toolExecutor)
    .WithInstructions("""
        Research the topic and provide key findings.
        Keep response concise.
        """)
    .Build();

// Writer Agent
var writer = new AgentBuilder("Writer", client, toolExecutor)
    .WithInstructions("""
        Convert the research into a concise executive summary.
        """)
    .Build();

// Pipeline
var pipeline = new SequentialPipeline("ResearchPipeline")
    .Add(researcher)
    .Add(writer);

// Execute
var result = await pipeline.RunAsync(
    "AI adoption in enterprise ERP systems.");

Console.WriteLine("PulseStackAI Multi-Agent Pipeline");
Console.WriteLine(new string('-', 40));

foreach (var step in result.Steps)
{
    Console.WriteLine($"\n[{step.AgentName}]");
    Console.WriteLine(step.Output);
}

Console.WriteLine("\nFinal Output");
Console.WriteLine(new string('-', 40));
Console.WriteLine(result.FinalOutput);