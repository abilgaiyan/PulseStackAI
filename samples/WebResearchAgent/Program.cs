using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PulseStack.Abstractions.Tools;
using PulseStack.Agents.Builders;
using PulseStack.Core.DependencyInjection;
using PulseStack.Providers.OpenAI.DependencyInjection;
using PulseStack.Tools.BuiltIn;
using PulseStack.Tools.DependencyInjection;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var apiKey = configuration["OpenAI:ApiKey"];
var model = configuration["OpenAI:Model"] ?? "gpt-4o-mini";

var services = new ServiceCollection();

services.AddLogging(builder =>
{
    builder.AddConsole();
});

services.AddPulseStack()
    .UseOpenAI(apiKey!, model);

services.AddTool<HttpTool>();

await using var serviceProvider =
    services.BuildServiceProvider();

var client = serviceProvider
    .GetRequiredService<IChatClient>();

var toolRegistry = serviceProvider
    .GetRequiredService<IToolRegistry>();

var agent = new AgentBuilder(
        "WebResearcher",
        client)
    .WithInstructions("""
        You are a web research assistant.

        Use tools whenever required.

        If the user asks about a website or URL,
        use the http tool to fetch content first.
        """)
    .WithTools(toolRegistry)
    .Build();

Console.WriteLine("PulseStackAI - Web Research Agent");
Console.WriteLine(new string('-', 50));

var response = await agent.RunAsync(
    """
    Fetch content from:
    https://example.com

    Then summarize it in 3 bullet points.
    """);

Console.WriteLine();
Console.WriteLine("Assistant:");
Console.WriteLine(response.Text);