# MeridianWorks Reference Application

> **MeridianWorks is the canonical external reference application for PulseStackAI. It is conformance and reference evidence, not a second PulseStackAI tutorial, an embedded framework sample, or a source of new framework contracts.**

## Evidence baseline

This document was established from the following verified states:

```text
PulseStackAI documentation parent
ee2aaad665412d37a8b4a9c84a3e4dc49d85fa5f

Verified MeridianWorks evidence baseline
6dbbef8441430863e1d6f0a8c526e7acbd0d2eb8
```

The MeridianWorks commit records **the external evidence verified for this documentation state**. It is not a runtime, build, package, or source dependency of PulseStackAI, and PulseStackAI does not require MeridianWorks to remain at that commit.

For normal exploration, use the current [MeridianWorks repository](https://github.com/abilgaiyan/MeridianWorks). The commit above provides the reproducible evidence point used for the statements in this document.

## What MeridianWorks proves

MeridianWorks exercises PulseStackAI from a separate repository through the public distribution and composition boundaries.

Its verified evidence chain is:

```text
external repository boundary
        ↓
exact NuGet development packages
        ↓
public DI composition
        ↓
stable declarative identities
        ↓
Model → Prompt → Agent → Workflow → Project
        ↓
persistent storage + publication
        ↓
Project-rooted graph loading
        ↓
application realization
        ↓
IApplicationOperation
        ↓
Workflow / Agent Runtime
        ↓
OpenRouter provider
        ↓
business-readable result
```

This chain is evidence that the separately documented framework boundaries compose successfully in an external application. It does not redefine those boundaries.

## Evidence matrix

| Framework boundary | Verified MeridianWorks evidence |
| --- | --- |
| External repository boundary | MeridianWorks is maintained in its own repository rather than under the PulseStackAI solution. |
| NuGet development-package consumption | The application consumes PulseStackAI through exact SHA-qualified development `PackageReference` versions rather than PulseStackAI project references or copied framework binaries. |
| Direct and transitive package consumption | MeridianWorks directly references `PulseStack.Agents`, `PulseStack.Core`, and `PulseStack.Providers.OpenRouter`; NuGet resolves their package dependencies transitively. |
| Public DI composition | The application composes `AddPulseStack()`, `AddPulseStackAgents()`, `AddPulseStackWorkflows()`, file-backed AI Asset storage/catalog, aggregate graph loading, and `UseOpenRouter(...)`. |
| Stable Asset identity | Model, Prompt, Agent, Workflow, and Project are recreated with explicit stable `AssetId` values. |
| Stable Workflow-step identity | The persisted Run step is authored through `DurableWorkflowStep.Run(...)` with an explicit `WorkflowStepId`. |
| Declarative application graph | The application authors Model → Prompt → Agent → Workflow → Project references and verifies their relationships. |
| Persistence | Each definition is mapped through `IAIAssetDocumentMapper` and stored through `IAIAssetWriter`, accepting the current created/already-present outcomes. |
| Publication | Stored definitions are published through `IAIAssetPublisher`, accepting the current published/already-published outcomes. |
| Aggregate graph loading | The persisted Project definition key is loaded through `IAIAssetGraphLoader`, and the resulting Project-rooted graph is verified. |
| Application realization | The loaded graph is explicitly realized through `IApplicationRealizer`, and Project/entry-Workflow realization provenance is verified. |
| Integrated application execution | The persisted Project is executed through `IApplicationOperation.ExecuteAsync(...)`. |
| Invocation input | The integrated operation receives an `ApplicationInvocationRequest` whose input is a `string`. |
| Invocation provenance | The returned invocation result is checked against the expected Project and entry Workflow references. |
| Runtime/provider execution | The integrated operation reaches Workflow and Agent execution backed by the configured OpenRouter provider. |
| Useful terminal output | The proof requires successful invocation and a non-empty, business-readable application result. |

The matrix describes what was observed at the verified MeridianWorks baseline. Normative framework semantics remain in PulseStackAI's architecture, contracts, and guides.

## Proof steps versus recommended application code

MeridianWorks deliberately performs some framework stages twice in different roles.

Before integrated execution, it explicitly loads the persisted graph and realizes the application:

```text
MeridianWorks conformance proof

Project AssetDefinitionKey
        ↓
IAIAssetGraphLoader
        ↓
verify AIAssetGraph
        ↓
IApplicationRealizer
        ↓
verify RealizedApplication
```

Those calls are **proof observations**. They demonstrate that persistent aggregate loading and application realization independently work across the external consumer boundary.

They are not the recommended normal execution sequence for application code.

The integrated consumer boundary remains:

```text
Recommended application code

Project AssetDefinitionKey
        +
ApplicationInvocationRequest
        ↓
IApplicationOperation.ExecuteAsync(...)
        ↓
ApplicationOperationResult
```

`IApplicationOperation` coordinates graph loading, realization, and invocation internally. Applications do not need to reproduce those stages manually merely to execute a persisted Project.

See the declarative application guide for the framework-owned procedural path.

## Package-consumption evidence

At the verified baseline, MeridianWorks directly references these PulseStackAI packages:

```text
PulseStack.Agents
PulseStack.Core
PulseStack.Providers.OpenRouter
```

They use one exact SHA-qualified development package version. MeridianWorks does not reference every package in PulseStackAI's ten-package production set; package dependencies are allowed to resolve transitively through NuGet.

This is external evidence for the producer/consumer ownership model documented by PulseStackAI:

```text
PulseStackAI
    produces + verifies immutable development packages
        ↓
publishes package files to a selected feed

MeridianWorks
    admits its NuGet source
        ↓
selects direct packages + exact version
        ↓
restores and verifies the resolved graph
        ↓
builds as an external consumer
```

Package-production and local-publication semantics remain owned by PulseStackAI's development-package guide.

## MeridianWorks-owned package automation

MeridianWorks contains its own:

```text
scripts/Update-PulseStack.ps1
docs/development/pulsestack-packages.md
nuget.config
```

These are reference-consumer implementation and documentation.

The updater demonstrates one consumer-owned adoption workflow: select an exact PulseStackAI development version, restore, inspect the resolved `PulseStack.*` graph, require that graph to use the requested exact version, and perform a Release build.

That automation is not a PulseStackAI API and is not mandatory for other consumers. Other applications can use ordinary NuGet source and `PackageReference` mechanisms appropriate to their repositories.

## Stable declarative identity evidence

MeridianWorks recreates the persisted application with stable identities for:

```text
Model
Prompt
Agent
Workflow
Project
Workflow Run step
```

The Workflow step uses the identity-complete authoring path:

```text
WorkflowStepId
        ↓
DurableWorkflowStep.Run(...)
        ↓
IdentityCompleteWorkflowAssetOptions
```

This verifies externally the persisted-authoring rule documented in the declarative application guide: restarting the application should recreate the same authored identities rather than silently generate different identities for the same logical persisted definitions.

The reference application proves the rule in use; the framework guide owns the procedural guidance.

## Persistent application evidence

MeridianWorks performs two explicit passes over its authored definitions:

```text
Model
Prompt
Agent
Workflow
Project
    ↓
store all
    ↓
publish all
```

It then uses the Project definition key as the persistent aggregate root.

The external proof verifies that the stored and published application can be reconstructed as an `AIAssetGraph` with the expected nodes and required relationships.

This is evidence for the Persistent Asset Platform. Detailed storage, publication, conflict, catalog, resolver, and graph-loading semantics remain owned by that architecture and its contracts.

## Realization evidence

MeridianWorks explicitly passes the loaded `AIAssetGraph` to `IApplicationRealizer` as a conformance step.

The proof checks that the resulting `RealizedApplication` retains the expected:

- Project reference;
- entry Workflow reference;
- realized Workflow name;
- Workflow description;
- Workflow step count.

This validates the external handoff from persistent declarative graph to realized application.

It does not make explicit realization a prerequisite that application code must reproduce before using `IApplicationOperation`.

## Integrated execution evidence

After the independent loading and realization checks, MeridianWorks executes the persisted Project through:

```csharp
IApplicationOperation.ExecuteAsync(
    projectKey,
    new ApplicationInvocationRequest(rfqInput))
```

The reference application requires the operation to reach an `InvocationOutcome`, then verifies:

- invocation success;
- Project provenance;
- entry-Workflow provenance;
- non-empty final output.

The input is a `string`, matching the current `ApplicationInvocationRequest` contract.

The execution reaches the configured OpenRouter-backed model through the framework's Workflow and Agent runtime path. The resulting text is required to be business-readable rather than merely proving that a method returned.

The RFQ domain used by MeridianWorks is reference-application content. Its prompt and business logic are not PulseStackAI contracts and are intentionally not reproduced here.

## Navigate to the owning documentation

This reference page records evidence. Use the owning documents for explanation and guidance.

### Framework distribution

To produce, publish, and consume PulseStackAI development packages:

[Development Package Consumption](../guides/development-packages.md)

### Declarative application development

To author, persist, publish, and execute a current PulseStackAI Project:

[Build and Execute a Declarative Application](../guides/declarative-application.md)

### Architecture

For the framework boundaries demonstrated by MeridianWorks:

- [Architecture Overview](../architecture/architecture-overview.md)
- [Persistent Asset Platform](../architecture/persistent-asset-platform.md)
- [Application Realization](../architecture/runtime-realization-architecture.md)
- [Application Operation and Invocation](../architecture/application-operation.md)
- [Workflow Runtime](../architecture/workflow-runtime.md)

### External implementation evidence

For the application source, consumer-owned package automation, and current external evolution:

[MeridianWorks repository](https://github.com/abilgaiyan/MeridianWorks)

The repository's current `main` branch is the useful exploration surface. The evidence baseline recorded at the top of this page is the exact revision used to establish this document's claims.

## Authority boundary

MeridianWorks demonstrates that the documented public framework path works across an external repository boundary.

It does **not**:

- define new PulseStackAI APIs or contracts;
- replace the R4 declarative-application guide;
- replace the R5 development-package guide;
- make explicit graph loading the recommended application execution path;
- make explicit realization the recommended application execution path;
- make its RFQ prompt or manufacturing domain part of the framework;
- make `Update-PulseStack.ps1` a required consumer mechanism;
- create a permanent source or version dependency between the two repositories;
- establish a framework sample-library architecture.

Its role is deliberately narrower:

```text
PulseStackAI documentation
    defines and teaches the public boundaries

MeridianWorks
    independently exercises those boundaries
        ↓
    provides external evidence
```
