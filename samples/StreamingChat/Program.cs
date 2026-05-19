using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Tools;
using PulseStack.Agents.Builders;
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

var toolExecutor = sp.GetRequiredService<IToolExecutor>();

var agent = new AgentBuilder("Streamer", client, toolExecutor)
    .WithInstructions("Be concise.")
    .Build();

Console.WriteLine("Streaming Response");
Console.WriteLine(new string('-', 40));

await foreach (var chunk in agent.StreamAsync(
    "Explain dependency injection in simple terms."))
{
    Console.Write(chunk);
}