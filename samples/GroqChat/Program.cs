using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PulseStack.Core.Chat; 
using PulseStack.Abstractions.Tools;
using PulseStack.Agents.Builders;
using PulseStack.Core.DependencyInjection;
using PulseStack.Providers.Groq.DependencyInjection;
using PulseStack.Tools.BuiltIn;
using PulseStack.Tools.DependencyInjection;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var apiKey = configuration["Groq:ApiKey"];
var model = configuration["Groq:Model"]
    ?? "llama-3.3-70b-versatile";
var endpoint = configuration["Groq:Endpoint"]
    ?? "https://api.groq.com/openai/v1";

var services = new ServiceCollection();

services.AddLogging(builder =>
{
    builder.AddConsole();
});

services.AddPulseStack()
    .UseGroq(apiKey!, model);

await using var serviceProvider =
    services.BuildServiceProvider();

var client = serviceProvider
    .GetRequiredService<IChatClient>();

Console.WriteLine("PulseStackAI - Groq Sample");
Console.WriteLine(new string('-', 40));

var answer = await client.AskAsync(
    """
    Explain why Groq is fast for AI inference
    in 3 bullet points.
    """);

Console.WriteLine();
Console.WriteLine("Assistant:");
Console.WriteLine(answer);


services.AddLogging(builder =>
{
    builder.AddConsole();
});

services.AddPulseStack()
    .UseGroq(apiKey!, model);

// Register tools
services.AddTool<CalculatorTool>();
services.AddTool<DateTimeTool>();

await using var serviceGroqProvider =
    services.BuildServiceProvider();

var clientGroq = serviceGroqProvider
    .GetRequiredService<IChatClient>();

var toolRegistry = serviceGroqProvider
    .GetRequiredService<IToolRegistry>();

var agent = new AgentBuilder(
        "GroqAssistant",
        clientGroq)
    .WithInstructions("""
        You are a helpful AI assistant.

        You have access to tools.

        Use tools whenever needed.

        Available tools include:
        - calculator
        - datetime
        """)
    .WithTools(toolRegistry)
    .Build();

Console.WriteLine("PulseStackAI - Groq Agent Sample");
Console.WriteLine(new string('-', 50));

var response = await agent.RunAsync(
    """
    What is:
    
    (125 * 12) + 450
    
    Also tell me the current UTC date and time.
    """);

Console.WriteLine();
Console.WriteLine("Assistant:");
Console.WriteLine(response.Text);