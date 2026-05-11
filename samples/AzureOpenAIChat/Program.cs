using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Core.Chat;
using PulseStack.Core.DependencyInjection;
using PulseStack.Providers.AzureOpenAI.DependencyInjection;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var endpoint = config["AzureOpenAI:Endpoint"];
var apiKey = config["AzureOpenAI:ApiKey"];
var deployment = config["AzureOpenAI:Deployment"];

var services = new ServiceCollection();

services.AddPulseStack()
    .UseAzureOpenAI(
        endpoint!,
        apiKey!,
        deployment!);

await using var serviceProvider =
    services.BuildServiceProvider();

var client = serviceProvider
    .GetRequiredService<IChatClient>();

Console.WriteLine("PulseStackAI Azure OpenAI Sample");
Console.WriteLine(new string('-', 40));

var answer = await client.AskAsync(
    "Explain Azure OpenAI in one sentence.");

Console.WriteLine(answer);