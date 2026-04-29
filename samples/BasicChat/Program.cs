using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Core.Chat;
using PulseStack.Core.DependencyInjection;
using PulseStack.Providers.OpenAI.DependencyInjection;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var apiKey = config["OpenAI:ApiKey"];
var model  = config["OpenAI:Model"] ?? "gpt-4o-mini";

var services = new ServiceCollection();

services.AddPulseStack()
        .UseOpenAI(apiKey!, model);

await using var sp = services.BuildServiceProvider();

var ai = sp.GetRequiredService<IChatClient>();

Console.WriteLine("PulseStackAI Basic Chat");
Console.WriteLine(new string('-', 30));

var answer = await ai.AskAsync("What is the capital of India? Reply in one sentence.");

Console.WriteLine(answer);