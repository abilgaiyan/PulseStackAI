using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

var agent = new AgentBuilder("Tutor", client)
    .WithInstructions("You are a helpful programming tutor. Be concise.")
    .WithTemperature(0.3f)
    .Build();

Console.WriteLine("PulseStackAI Basic Agent");
Console.WriteLine(new string('-', 30));

var result = await agent.RunAsync(
    "Explain dependency injection in simple terms.");

Console.WriteLine(result.Text);