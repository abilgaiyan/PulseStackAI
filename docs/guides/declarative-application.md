# Build and Execute a Declarative Application

> **Author stable AI Asset definitions, persist them, publish them, and execute the persisted Project through `IApplicationOperation`.**

This guide teaches one current public consumer path through PulseStackAI:

```text
Author
  Model → Prompt → Agent → Workflow → Project
        ↓
Persist all required definitions
        ↓
Publish all definitions
        ↓
Project AssetDefinitionKey
        ↓
IApplicationOperation.ExecuteAsync(...)
        ↓
ApplicationOperationResult
```

The example is intentionally small and domain-neutral. It uses current public contracts rather than the older `Workflow.Create(...)` builder grammar.

## What the guide owns

This is procedural consumer guidance. It shows how to compose the public services and APIs needed for one persisted declarative application.

The detailed semantics of canonical serialization, storage conflicts, catalog publication, resolution, graph loading, realization, invocation coordination, Workflow Runtime execution, and providers remain owned by their architecture and contract boundaries.

In particular, the **store-all → publish-all** sequence below is an application composition sequence. It does not introduce a transaction spanning storage and publication.

## 1. Compose the required services

A file-backed application using OpenRouter can compose the current public services as follows:

```csharp
var services = new ServiceCollection();

services
    .AddPulseStack()
    .AddPulseStackAgents()
    .AddPulseStackWorkflows()
    .AddFileAIAssetStorage(
        assetStoragePath,
        new AIAssetStorageOptions
        {
            MaximumRepresentationSizeBytes = 1024 * 1024
        })
    .AddFileAIAssetCatalog(assetCatalogPath)
    .AddAIAssetGraphLoading()
    .UseOpenRouter(
        openRouterApiKey,
        "deepseek/deepseek-chat-v3-0324");

await using var provider = services.BuildServiceProvider(
    new ServiceProviderOptions
    {
        ValidateScopes = true
    });
```

The ordering around the persistent asset platform is meaningful: storage establishes the loader authority, catalog composition builds publication and persistent resolution over that loader, and graph loading requires exactly one persistent resolver authority.

The provider registration is also relevant while authoring the Model Asset because `ModelAssetFactory` validates the requested provider/model against the configured model catalog.

## 2. Choose stable identities

A persisted application must recreate the same authored identities when the application is started again.

Use explicit, stable `AssetId` values for the persisted definitions in this example:

```csharp
var modelId = new AssetId(
    Guid.Parse("10000000-0000-0000-0000-000000000001"));

var promptId = new AssetId(
    Guid.Parse("10000000-0000-0000-0000-000000000002"));

var agentId = new AssetId(
    Guid.Parse("10000000-0000-0000-0000-000000000003"));

var workflowId = new AssetId(
    Guid.Parse("10000000-0000-0000-0000-000000000004"));

var projectId = new AssetId(
    Guid.Parse("10000000-0000-0000-0000-000000000005"));

var runStepId = new WorkflowStepId(
    Guid.Parse("20000000-0000-0000-0000-000000000001"));
```

The literal GUIDs are only example identities. A real application should own and preserve its chosen identities rather than generate new ones on each startup.

This applies to the Workflow step as well as the persisted Asset definitions. `DurableWorkflowStep` is the current explicit-identity authoring authority for identity-complete declarative Workflow-step subtrees.

## 3. Author Model and Prompt Assets

Resolve the current factories from the composed provider:

```csharp
var modelFactory = provider.GetRequiredService<ModelAssetFactory>();
var promptFactory = provider.GetRequiredService<PromptAssetFactory>();
var workflowFactory = provider.GetRequiredService<WorkflowAssetFactory>();
var projectFactory = provider.GetRequiredService<ProjectAssetFactory>();
```

Create the Model and Prompt with explicit identities:

```csharp
var model = modelFactory.Create(
    modelId,
    new ModelAssetOptions(
        Provider: "OpenRouter",
        Model: "deepseek/deepseek-chat-v3-0324"));

var prompt = promptFactory.Create(
    promptId,
    new PromptAssetOptions
    {
        Name = "Analysis Prompt",
        SystemInstructions =
            "Analyze the supplied text and return a concise, useful response."
    });
```

References between Assets carry the referenced definition's type, identity, URN, and version. A small helper keeps the example readable:

```csharp
static AssetReference Reference(IAsset asset) =>
    new(
        asset.Type,
        asset.Id,
        asset.Urn,
        asset.Version);
```

## 4. Author the Agent

`AgentBuilder` supports explicit Agent identity through `WithId`.

```csharp
var agent = new AgentBuilder("Analysis Agent")
    .WithId(agentId)
    .WithGoal("Analyze the supplied text.")
    .WithRole("Business analyst")
    .AddResponsibility("Return a concise and useful analysis.")
    .UseModel(Reference(model))
    .UsePrompt(Reference(prompt))
    .Build();
```

The Agent references the Model and Prompt definitions; it does not resolve or execute them while it is being authored.

## 5. Author the Workflow with explicit step identity

For a persisted Workflow, use the identity-complete authoring surface rather than relying on a step definition's generated default identity.

```csharp
var runAnalysis = DurableWorkflowStep.Run(
    runStepId,
    Reference(agent));

var workflow = workflowFactory.Create(
    workflowId,
    new IdentityCompleteWorkflowAssetOptions
    {
        Name = "Analysis Workflow",
        Description = "Runs the analysis agent.",
        Steps = [runAnalysis]
    });
```

The same rule extends to nested declarative steps. `DurableWorkflowStep` provides explicit-identity authoring for Run, Parallel, Conditional, Retry, Loop, and Switch subtrees.

Stable Workflow-step identity matters for persistence: recreating the logical Workflow with newly generated step identities creates a different serialized definition instead of recreating the same one.

## 6. Author the Project application root

The Project identifies its entry Workflow and can list the definitions it owns.

```csharp
var project = projectFactory.Create(
    projectId,
    new ProjectAssetOptions
    {
        Name = "Analysis Application",
        Description = "Small persisted declarative application.",
        EntryWorkflow = Reference(workflow),
        OwnedAssets =
        [
            Reference(model),
            Reference(prompt),
            Reference(agent),
            Reference(workflow)
        ]
    });
```

The Project is the persisted application root used later by `IApplicationOperation`.

## 7. Map and persist all required definitions

Collect the definitions in a deterministic application-owned sequence:

```csharp
IAsset[] definitions =
[
    model,
    prompt,
    agent,
    workflow,
    project
];
```

Resolve the public mapping and writing authorities:

```csharp
var mapper = provider.GetRequiredService<IAIAssetDocumentMapper>();
var writer = provider.GetRequiredService<IAIAssetWriter>();
```

Persist every definition before beginning publication:

```csharp
foreach (var definition in definitions)
{
    var key = AssetDefinitionKey.From(definition);
    var document = mapper.ToDocument(definition);

    var result = await writer.WriteAsync(key, document);

    switch (result)
    {
        case AIAssetWriteResult.Created:
        case AIAssetWriteResult.AlreadyPresent:
            break;

        case AIAssetWriteResult.Conflict:
            throw new InvalidOperationException(
                $"Stored definition conflicts with {key}.");

        default:
            throw new InvalidOperationException(
                $"Unexpected write result '{result}' for {key}.");
    }
}
```

The current write result supports the repeat-run distinction needed by this guide:

```text
Created
    exact definition was stored

AlreadyPresent
    the exact stored definition already exists

Conflict
    the requested write conflicts with existing storage state
```

The loop deliberately accepts `Created` and `AlreadyPresent`. It does not treat `Conflict` as idempotent success.

## 8. Publish only after the storage pass succeeds

After every required definition has completed the storage pass, publish the definitions:

```csharp
var publisher = provider.GetRequiredService<IAIAssetPublisher>();

foreach (var definition in definitions)
{
    var key = AssetDefinitionKey.From(definition);
    var result = await publisher.PublishAsync(key);

    switch (result)
    {
        case AIAssetPublicationResult.Published:
        case AIAssetPublicationResult.AlreadyPublished:
            break;

        case AIAssetPublicationResult.DefinitionNotStored:
            throw new InvalidOperationException(
                $"Cannot publish unstored definition {key}.");

        case AIAssetPublicationResult.IdentityConflict:
            throw new InvalidOperationException(
                $"Published identity conflicts with {key}.");

        default:
            throw new InvalidOperationException(
                $"Unexpected publication result '{result}' for {key}.");
    }
}
```

The resulting application sequence is:

```text
store Model
store Prompt
store Agent
store Workflow
store Project
        ↓
all storage accepted
        ↓
publish Model
publish Prompt
publish Agent
publish Workflow
publish Project
```

This sequencing avoids deliberately beginning publication while the application's own storage pass is still incomplete. It does **not** claim cross-definition or storage/catalog transactionality. Conflict, atomicity, publication, catalog, and resolution semantics remain defined by the Persistent Asset Platform contracts.

For repeat execution of the same authored definitions, `AlreadyPresent` and `AlreadyPublished` are the explicit idempotent outcomes accepted by this example.

## 9. Execute by Project definition key

The normal consumer execution boundary is `IApplicationOperation`.

Create the exact Project definition key:

```csharp
var projectKey = AssetDefinitionKey.From(project);
```

Create the invocation request. The current input contract is a `string`:

```csharp
var request = new ApplicationInvocationRequest(
    "Summarize the main risks in this proposal.");
```

Resolve the scoped operation and execute the persisted Project:

```csharp
await using var scope = provider.CreateAsyncScope();

var operation =
    scope.ServiceProvider.GetRequiredService<IApplicationOperation>();

var result = await operation.ExecuteAsync(
    projectKey,
    request);
```

That is the normal integrated execution path taught by this guide.

## 10. Handle ApplicationOperationResult

`ApplicationOperationResult` preserves the stage that produced a terminal non-exceptional outcome:

```csharp
switch (result)
{
    case ApplicationOperationResult.InvocationOutcome invocation:
        Console.WriteLine(invocation.Result.FinalOutput);
        break;

    case ApplicationOperationResult.LoadOutcome load:
        Console.WriteLine(
            $"Application graph could not be loaded: {load.Result}");
        break;

    case ApplicationOperationResult.RealizationOutcome realization:
        Console.WriteLine(
            $"Application could not be realized: {realization.Result}");
        break;
}
```

Once load and realization succeed, the operation returns an `InvocationOutcome` containing the `ApplicationInvocationResult`. That result carries Project and entry-Workflow provenance together with `Success`, `FinalOutput`, and top-level Workflow step results.

Exceptions and cancellation remain separate from these non-exceptional operation outcomes.

## What IApplicationOperation coordinates

Developer code should remain at the integrated boundary:

```text
Project AssetDefinitionKey
        +
ApplicationInvocationRequest
        ↓
IApplicationOperation.ExecuteAsync(...)
        ↓
ApplicationOperationResult
```

Internally, the framework coordinates the already-defined architecture:

```text
Graph Load
    ↓
Application Realization
    ↓
Application Invocation
    ↓
Workflow Runtime
```

Those stages are useful for understanding diagnostics and architecture, but normal consumer code should not reproduce that chain manually.

## Complete lifecycle

The current persisted declarative application path is therefore:

```text
stable Model
    ↓
stable Prompt
    ↓
stable Agent
    ↓
stable Workflow + stable Workflow-step identities
    ↓
stable Project
    ↓
map to canonical AI Asset documents
    ↓
store all required definitions
    ↓
publish all definitions
    ↓
Project AssetDefinitionKey
    ↓
ApplicationInvocationRequest(string input)
    ↓
IApplicationOperation
    ↓
ApplicationOperationResult
```

This is the developer-facing bridge between declarative AI Asset authoring and the architecture documented by the Persistent Asset Platform and Application Execution layers.

## Reference application

MeridianWorks is the external reference application used to prove that the same public lifecycle works from a NuGet-consuming application. It validates the practicality of stable declarative authoring, persistence/publication, persistent graph loading, realization, and integrated application execution.

This guide intentionally does not reproduce MeridianWorks' RFQ domain code. The reference application is evidence for the public path; it is not the source of this guide's domain example.

## Out of scope

This guide intentionally does not teach:

- the legacy `Workflow.Create(...)` grammar;
- Application Language or specification reconciliation;
- manual `IAIAssetGraphLoader` usage as the normal execution path;
- manual `IApplicationRealizer` usage;
- manual `IApplicationInvoker` usage;
- internal composers, resolvers, or step executors;
- Agent Runtime internals;
- tool or provider internals;
- NuGet package production;
- local-feed maintenance;
- MeridianWorks RFQ implementation;
- README restructuring;
- cleanup of older overlapping guides.

Those concerns retain their existing architecture, specification, engineering-record, or later refactoring ownership.
