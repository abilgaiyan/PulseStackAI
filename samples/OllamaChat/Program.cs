using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Core.Chat;
using PulseStack.Core.DependencyInjection;
using PulseStack.Providers.Ollama.DependencyInjection;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var endpoint = configuration["Ollama:Endpoint"];
var model = configuration["Ollama:Model"] ?? "llama3.2";

var services = new ServiceCollection();

services.AddPulseStack()
    .UseOllama(
        endpoint!,
        model);

await using var serviceProvider =
    services.BuildServiceProvider();

var client = serviceProvider
    .GetRequiredService<IChatClient>();

Console.WriteLine("PulseStackAI - Ollama Sample");
Console.WriteLine(new string('-', 40));

var answer = await client.AskAsync(
    "Explain local AI models in one sentence.");

Console.WriteLine();
Console.WriteLine(answer);