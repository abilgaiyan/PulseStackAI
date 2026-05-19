using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PulseStack.Abstractions.Tools;
using PulseStack.Agents.Builders;
using PulseStack.Core.DependencyInjection;
using PulseStack.Core.Tools;
using PulseStack.Providers.OpenAI.DependencyInjection;
using PulseStack.Tools.BuiltIn;
using PulseStack.Tools.DependencyInjection;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var apiKey = config["OpenAI:ApiKey"];
var model = config["OpenAI:Model"] ?? "gpt-4o-mini";

var services = new ServiceCollection();

services.AddLogging(builder =>
{
    builder.AddConsole();
});

services.AddPulseStack()
    .UseOpenAI(apiKey!, model);

services.AddTool<CalculatorTool>();
services.AddTool<DateTimeTool>();

await using var serviceProvider =
    services.BuildServiceProvider();

var registry = new ToolRegistry()
    .PopulateFromServices(serviceProvider);

var client = serviceProvider
    .GetRequiredService<IChatClient>();

var toolExecutor = serviceProvider
    .GetRequiredService<IToolExecutor>();    

var agent = new AgentBuilder(
        "UtilityAssistant",
        client,
        toolExecutor)
    .WithInstructions("""
        You are a helpful AI assistant.

        Use tools whenever required.
        Never guess dates, times, or calculations.
        """)
    .WithTools(registry)
    .Build();

Console.WriteLine("PulseStackAI - Basic Agent");
Console.WriteLine(new string('-', 40));

var response = await agent.RunAsync(
    "What day is today and what is 25 * 48?");

Console.WriteLine();
Console.WriteLine("Assistant:");
Console.WriteLine(response.Text);