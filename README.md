# PulseStackAI

> **A domain-driven AI application engineering platform for .NET**
>
> Build AI-powered business applications around reusable declarative assets, persistent application definitions, and a composable execution runtime.

PulseStackAI is designed for applications that begin with business intent and grow beyond a single prompt. It separates application composition and runtime boundaries from provider integration, while allowing Model Assets to explicitly select the provider and model they require.

Instead of making each application invent its own persistence, orchestration, realization, and execution infrastructure, PulseStackAI provides a framework-owned path from declarative application definitions to provider-backed execution.

## Why PulseStackAI?

AI applications often start with a simple requirement:

> Review this contract. Summarize this meeting. Analyze this request. Approve this expense.

As the application grows, it needs reusable prompts and agents, workflow composition, persistence, provider integration, retries, runtime coordination, and other infrastructure.

PulseStackAI keeps those concerns behind explicit framework boundaries so application code can remain centered on business capabilities.

The guiding idea is:

> **AI applications are business systems.**

Business intent should remain readable as models, providers, and infrastructure evolve.

## What PulseStackAI is

PulseStackAI combines several distinct responsibilities:

- **AI Assets** describe reusable application definitions such as Models, Prompts, Agents, Workflows, Projects, Tools, Knowledge, Memory, Policies, Libraries, and Package Assets.
- **The Persistent Asset Platform** maps, validates, stores, publishes, resolves, and loads declarative definitions.
- **Application Realization** turns an accepted Project-rooted `AIAssetGraph` into a `RealizedApplication`.
- **Application Operation** provides the integrated persisted-Project execution boundary.
- **Workflow and Agent Runtime** perform the realized work.
- **Provider integrations** connect runtime execution to configured model providers.

At architecture level:

```text
Business Intent
        ↓
AI Asset Model
        ↓
Persistent Asset Platform
        ↓
Application Realization
        ↓
Application Operation
        ↓
Workflow Runtime
        ↓
Providers
```

Each boundary has its own responsibility. The detailed architecture is documented separately rather than reproduced in this README.

## Current developer lifecycle

For an external application, the current path is:

```text
Get PulseStackAI
        ↓
Author AI Assets
        ↓
Persist + Publish definitions
        ↓
Project AssetDefinitionKey
        ↓
IApplicationOperation
        ↓
Workflow / Agent Runtime
        ↓
Provider-backed result
```

Package distribution and AI Asset publication are different operations:

- PulseStackAI development packages are .NET/NuGet distribution artifacts.
- Published AI Asset definitions are persisted application definitions made available through the PulseStackAI asset catalog.

See the dedicated guides below for each workflow.

## Integrated application boundary

A persisted Project is executed through `IApplicationOperation`:

```csharp
var result = await applicationOperation.ExecuteAsync(
    projectKey,
    new ApplicationInvocationRequest(input));
```

The operation coordinates the framework's graph-loading, realization, and invocation stages. Application code does not need to reproduce those stages manually merely to execute a persisted Project.

For stable identities, declarative authoring, persistence, publication, composition, and result handling, follow the [declarative application guide](docs/guides/declarative-application.md).

## Current framework capabilities

The current foundation includes:

- reusable declarative AI Asset definitions;
- stable persisted Asset and Workflow-step identities;
- canonical AI Asset mapping, validation, and serialization;
- serialized Asset storage and loading;
- persistent catalog publication and exact resolution;
- Project-, Library-, and Package-rooted aggregate graph loading;
- Project application realization;
- integrated persisted-Project execution through `IApplicationOperation`;
- Workflow and Agent runtime execution;
- provider integrations including OpenAI, Azure OpenAI, Ollama, Gemini, Groq, and OpenRouter;
- deterministic SHA-qualified NuGet development-package production;
- immutable local development-package publication;
- external package-consumer and application proof through MeridianWorks.

Detailed milestone history and architectural decision records live under [Engineering](docs/Engineering/) rather than in the public front door.

## Get PulseStackAI

PulseStackAI currently supports immutable SHA-qualified development packages produced from an exact committed framework source state and published to a developer-selected local NuGet directory feed.

External applications select the packages they directly require, pin an exact development version, and let NuGet resolve package dependencies transitively.

See:

**[Consume PulseStackAI Development Packages](docs/guides/development-packages.md)**

That guide owns package production, provenance, local publication, consumer source configuration, exact-version adoption, and restore/build verification.

It does not establish a stable public NuGet release policy.

## Build an application

The current declarative application guide covers the framework-owned procedural path:

```text
Model → Prompt → Agent → Workflow → Project
                ↓
        Persist definitions
                ↓
        Publish definitions
                ↓
   Project AssetDefinitionKey
                ↓
      IApplicationOperation
```

See:

**[Build and Execute a Declarative Application](docs/guides/declarative-application.md)**

The guide covers stable identities, identity-complete Workflow authoring, current DI composition, canonical document mapping, persistence/publication result handling, and integrated Project execution.

## Understand the architecture

Start with:

**[Architecture Overview](docs/architecture/architecture-overview.md)**

Then follow the boundary you need:

- [AI Asset Model](docs/architecture/ai-asset-model.md) — current declarative Asset concepts, identity, taxonomy, and composition.
- [Persistent Asset Platform](docs/architecture/persistent-asset-platform.md) — canonical documents, storage, publication, catalog resolution, and aggregate graph loading.
- [Application Realization](docs/architecture/runtime-realization-architecture.md) — `AIAssetGraph` to `RealizedApplication`.
- [Application Operation & Invocation](docs/architecture/application-operation.md) — integrated persisted-Project execution and invocation coordination.
- [Workflow Runtime](docs/architecture/workflow-runtime.md) — execution of realized Workflows through the runtime and step executors.

These documents own the current architecture. Older RFCs, milestone closures, and design records remain valuable engineering history but are not substitutes for the current architecture set.

## External reference application

[MeridianWorks](docs/reference/meridianworks.md) is the canonical external reference application for PulseStackAI.

It provides evidence that a separate repository can:

```text
consume exact PulseStackAI NuGet packages
        ↓
compose public framework services
        ↓
author stable declarative Assets
        ↓
persist + publish a Project application
        ↓
load and realize the persisted graph
        ↓
execute through IApplicationOperation
        ↓
reach provider-backed runtime execution
        ↓
return business-readable output
```

MeridianWorks is external conformance/reference evidence. It is not a second PulseStackAI tutorial and does not define new framework contracts.

## Documentation map

| Goal | Canonical documentation |
| --- | --- |
| Understand PulseStackAI at a high level | [Architecture Overview](docs/architecture/architecture-overview.md) |
| Understand AI Assets | [AI Asset Model](docs/architecture/ai-asset-model.md) |
| Understand persistence and graph loading | [Persistent Asset Platform](docs/architecture/persistent-asset-platform.md) |
| Understand realization | [Application Realization](docs/architecture/runtime-realization-architecture.md) |
| Understand integrated execution | [Application Operation & Invocation](docs/architecture/application-operation.md) |
| Understand Workflow execution | [Workflow Runtime](docs/architecture/workflow-runtime.md) |
| Build a persisted declarative application | [Declarative Application Guide](docs/guides/declarative-application.md) |
| Consume development packages | [Development Package Guide](docs/guides/development-packages.md) |
| Inspect external reference evidence | [MeridianWorks Reference Application](docs/reference/meridianworks.md) |
| Review historical decisions and milestone records | [Engineering](docs/Engineering/) |

## Documentation authority

PulseStackAI separates current documentation from engineering history:

```text
Architecture
    what boundaries exist and how they relate

Guides
    how developers use those boundaries

Specifications
    language and contract definitions

Engineering records
    why the architecture and contracts evolved as they did

Reference applications
    external evidence that documented public paths work
```

The current architecture, guides, and reconciled specifications linked above are the appropriate authorities for implemented application behavior. Older Workflow and engineering material retained as historical documentation records earlier design states and should be read in that context. Other documentation may remain scheduled for separate future reconciliation without overriding these current authorities.

## Vision

PulseStackAI aims to let developers spend less time rebuilding AI orchestration infrastructure and more time solving business problems.

Models will evolve. Providers will evolve. Infrastructure will evolve.

The framework's goal is to keep application intent and reusable capabilities understandable while those implementation choices change.

> **Describe the intent. Compose the capabilities. Persist the application. Execute through a stable framework boundary.**
