using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PulseStack.Core.Chat;
using PulseStack.Core.DependencyInjection;
using PulseStack.Providers.Gemini.DependencyInjection;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var apiKey = configuration["Gemini:ApiKey"];
var model = configuration["Gemini:Model"] ?? "gemini-2.0-flash";

var services = new ServiceCollection();

services.AddLogging(builder =>
{
    builder.AddConsole();
});

services.AddPulseStack()
    .UseGemini(options =>
    {
        options.ApiKey = apiKey!;
        options.Model = model;

        options.UseFunctionInvocation = false;
        options.UseLogging = true;
        options.UseOpenTelemetry = false;
    });

await using var serviceProvider =
    services.BuildServiceProvider();

var client = serviceProvider
    .GetRequiredService<IChatClient>();

Console.WriteLine("PulseStackAI - Gemini Sample");
Console.WriteLine(new string('-', 40));

var answer = await client.AskAsync(
    """
    Explain why Gemini is useful for AI applications
    in 3 bullet points.
    """);

Console.WriteLine();
Console.WriteLine("Assistant:");
Console.WriteLine(answer);
