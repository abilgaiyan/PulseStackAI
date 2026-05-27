using PulseStack.Showcase.Infrastructure;
using PulseStack.Showcase.Scenarios;

var services =
    ServiceConfiguration.Configure();

Console.Title =
    "PulseStackAI Showcase";

await SequentialPipelineScenario.RunAsync(services);

await ParallelPipelineScenario.RunAsync(services);

await RuntimeGovernanceScenario.RunAsync(services);

await ToolCallingScenario.RunAsync(services);

await ERPInvoiceLookupToolCallingScenario.RunAsync(services);

Console.WriteLine();
Console.WriteLine("Showcase complete.");