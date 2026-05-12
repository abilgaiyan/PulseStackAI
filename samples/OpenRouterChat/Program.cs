using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Chat;
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

var services = new ServiceCollection();

services.AddPulseStack()
    .UseOpenRouter(apiKey!);

// Tools
services.AddTool<CalculatorTool>();
services.AddTool<DateTimeTool>();
services.AddTool<HttpTool>();

await using var serviceProvider =
    services.BuildServiceProvider();

var factory = serviceProvider
    .GetRequiredService<IChatClientFactory>();

var tools = serviceProvider
    .GetRequiredService<IToolRegistry>();

// Planner Agent
var plannerAgent = new AgentBuilder(
        "PlannerAgent",
        factory)
    .WithModel("meta-llama/llama-3.3-70b-instruct")
    .WithInstructions("""
        You are a planning agent.

        Break problems into smaller steps.

        Be concise.
        """)
    .Build();

// Math Agent
var mathAgent = new AgentBuilder(
        "MathAgent",
        factory)
    .WithModel("deepseek/deepseek-chat-v3-0324")
    .WithInstructions("""
        You are a math and reasoning agent.

        Use tools whenever required.

        After tool execution results are returned,
        provide a final response using ONLY tool results.
        """)
    .WithTools(tools)
    .Build();

// Research Agent
var researchAgent = new AgentBuilder(
        "ResearchAgent",
        factory)
    .WithModel("meta-llama/llama-3.3-70b-instruct")
    .WithInstructions("""
        You are a lightweight research assistant.

        Keep answers short and factual.
        """)
    .Build();

Console.WriteLine("PulseStackAI - Multi Model Sample");
Console.WriteLine(new string('-', 55));


// STEP 1 — Planning

var plan = await plannerAgent.RunAsync(
    """
    I need to:
    
    1. Calculate:
       (250 * 4) + 300

    2. Get current UTC date and time.

    Create a short execution plan.
    """);

Console.WriteLine();
Console.WriteLine("=== Planner Agent ===");
Console.WriteLine(plan.Text);


// STEP 2 — Math + Tools

var mathResult = await mathAgent.RunAsync(
    """
    Calculate:
    (250 * 4) + 300

    Also provide the current UTC time.
    """);

Console.WriteLine();
Console.WriteLine("=== Math Agent ===");
Console.WriteLine(mathResult.Text);


// STEP 3 — Final Summary

var summary = await researchAgent.RunAsync(
    $"""
    Summarize this result in a concise way:

    {mathResult.Text}
    """);

Console.WriteLine();
Console.WriteLine("=== Research Agent ===");
Console.WriteLine(summary.Text);