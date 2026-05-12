using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PulseStack.Core.Chat;
using PulseStack.Core.DependencyInjection;
using PulseStack.Providers.Groq.DependencyInjection;

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