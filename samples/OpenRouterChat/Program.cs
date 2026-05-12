using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Tools;
using PulseStack.Agents.Builders;
using PulseStack.Core.DependencyInjection;
using PulseStack.Providers.OpenRouter.DependencyInjection;
using PulseStack.Tools.BuiltIn;
using PulseStack.Tools.DependencyInjection;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var apiKey = configuration["OpenRouter:ApiKey"];

var model = configuration["OpenRouter:Model"]
    ?? "openai/gpt-4.1-mini";

var services = new ServiceCollection();

services.AddPulseStack()
    .UseOpenRouter(apiKey!, model);

services.AddTool<CalculatorTool>();
services.AddTool<DateTimeTool>();

await using var serviceProvider =
    services.BuildServiceProvider();

var client = serviceProvider
    .GetRequiredService<IChatClient>();

var tools = serviceProvider
    .GetRequiredService<IToolRegistry>();

var agent = new AgentBuilder(
        "OpenRouterAssistant",
        client)
    .WithInstructions("""
        You are a helpful AI assistant.

        Use tools whenever needed.

        After tool execution results are returned,
        provide a final response using ONLY tool results.
        """)
    .WithTools(tools)
    .Build();

Console.WriteLine("PulseStackAI - OpenRouter Sample");
Console.WriteLine(new string('-', 50));

var response = await agent.RunAsync(
    """
    Calculate:
    (250 * 4) + 300

    Also provide the current UTC time.
    """);

Console.WriteLine();
Console.WriteLine("Assistant:");
Console.WriteLine(response.Text);