using PulseStack.Showcase.Infrastructure;
using PulseStack.Showcase.Scenarios;

var services =
    ServiceConfiguration.Configure();

Console.Title = "PulseStackAI Showcase";

//
// Runtime
//

await SequentialPipelineScenario.RunAsync(services);
await ParallelPipelineScenario.RunAsync(services);
await ConditionalPipelineScenario.RunAsync(services);
await RouterPipelineScenario.RunAsync(services);

//
// Reliability
//

await RetryScenario.RunAsync(services);
await TimeoutScenario.RunAsync(services);
await PartialFailureScenario.RunAsync(services);

//
// Observability
//

await OpenTelemetryScenario.RunAsync(services);
await OpenTelemetryMetricsScenario.RunAsync(services);
await UsageAndCostScenario.RunAsync();

//
// Governance
//

await RuntimeGovernanceScenario.RunAsync(services);

//
// Tools
//

await ToolCallingScenario.RunAsync(services);
await ERPInvoiceLookupToolCallingScenario.RunAsync(services);

//
// Persistence
//

//await WorkflowPersistenceScenario.RunAsync(services);

Console.WriteLine();
Console.WriteLine("Showcase complete.");