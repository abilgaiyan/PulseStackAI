# AI Asset Model

> **AI Assets are the declarative definitions that identify and compose reusable application capabilities in PulseStackAI.**

This document describes the current public AI Asset architecture. It intentionally stays at the definition boundary: persistence, catalog mechanics, graph loading, realization, invocation, runtime execution, provider behavior, and unresolved language specifications are outside its scope.

## What is an AI Asset?

An AI Asset is a declarative definition represented by the public `IAsset` contract and the concrete asset types built on it.

Every asset exposes a common architectural surface:

```text
IAsset
├── Id
├── Urn
├── Version
├── Metadata
├── Type
├── Lifecycle
├── References
└── Dependencies
```

Assets describe definitions. They are not workflow execution state, runtime contexts, realized applications, provider clients, or invocation results.

This definition/runtime separation is fundamental:

```text
AI Asset Definition
        │
        ▼
Persistent Asset Platform
        │
        ▼
AIAssetGraph
        │
        ▼
Application Realization
        │
        ▼
Runtime Representation
```

The later boundaries do not change what an AI Asset is. They store, resolve, compose, realize, or execute definitions according to their own contracts.

## Asset identity

PulseStackAI distinguishes several identity-related values rather than treating an asset name as its persistence identity.

An asset carries:

- `AssetId` — the asset identifier.
- `AssetUrn` — the asset URN.
- `AssetVersion` — the definition version.
- `AssetType` — the asset kind.

For operations that must identify one immutable definition, the public contract uses `AssetDefinitionKey`:

```csharp
public readonly record struct AssetDefinitionKey(
    AssetType Type,
    AssetId Id,
    AssetVersion Version);
```

An `AssetDefinitionKey` can be projected from either an `IAsset` or an `AssetReference`. The key therefore identifies a specific typed, versioned asset definition independently of its display metadata.

`AssetReference` carries the referenced asset's type, ID, URN, and version:

```csharp
public sealed record AssetReference(
    AssetType Type,
    AssetId Id,
    AssetUrn Urn,
    AssetVersion Version);
```

These contracts provide the identity vocabulary used by later persistence, resolution, graph-loading, and realization boundaries.

## Current asset taxonomy

The current public `AssetType` enumeration defines these asset kinds:

```text
Project
Library
Package
Workflow
Agent
Prompt
Tool
Knowledge
Memory
Policy
Provider
Model
```

The presence of an asset kind establishes a declarative definition category. It does **not** imply that every asset kind has equivalent composition rules, realization behavior, or runtime capabilities.

In particular, this architecture document does not assign additional behavior to Knowledge, Memory, Policy, Provider, or other asset types beyond what their current public contracts encode.

## Common references and dependencies

The base `Asset` contract exposes two general relationship collections:

```csharp
IReadOnlyCollection<AssetReference> References
IReadOnlyCollection<AssetDependency> Dependencies
```

An `AssetDependency` contains an `AssetReference` and a `Required` flag.

These common contracts allow an asset definition to identify related definitions. They should not be interpreted as a complete universal dependency grammar for every asset type. More specific relationships are encoded by individual asset contracts, and language-level rules remain the responsibility of reconciled specifications.

## Current composition boundaries

The following relationships are architectural facts encoded by current public asset options. They are examples of the current composition surface, not a claim that every valid application must have one universal shape.

### Project

`ProjectAssetOptions` represents one intelligent application and contains:

```text
Project
├── EntryWorkflow : AssetReference
└── OwnedAssets   : AssetReference[]
```

The entry Workflow identifies the Workflow definition that serves as the application's execution entry point. `OwnedAssets` identifies definitions owned by the Project.

A persisted Project's `AssetDefinitionKey` is also the root accepted by the integrated persisted-application operation described by the wider architecture.

### Library

`LibraryAssetOptions` represents a reusable collection of AI Asset definitions:

```text
Library
└── Members : AssetReference[]
```

A Library is therefore a grouping/composition definition. This document does not infer additional dependency or runtime semantics beyond its current member references.

### Package Asset

`PackageAssetOptions` represents a distribution boundary:

```text
Package Asset
└── Members : AssetReference[]
```

A Package Asset groups referenced AI Asset definitions at the application-definition level.

A **Package Asset is not a NuGet package**. NuGet packages distribute PulseStackAI framework binaries to .NET consumers. The two concepts share a word but belong to different architectural concerns.

### Workflow

`WorkflowAssetOptions` represents a reusable declarative Workflow Asset:

```text
Workflow Asset
├── Name
├── Description
└── WorkflowStepDefinition[]
```

A Workflow Asset contains declarative workflow-step definitions. It is not the runtime `Workflow` object accepted by `IWorkflowRuntime`.

The distinction is intentional:

```text
WorkflowAsset
    declarative definition
          │
          │ later realization
          ▼
Workflow
    runtime representation
```

How workflow definitions are realized and executed belongs to the realization and workflow-runtime architecture, not to the AI Asset Model.

### Agent

`AgentDefinitionOptions` currently encodes explicit references to other definitions:

```text
Agent
├── Model?      : AssetReference
├── Prompt?     : AssetReference
├── Knowledge[] : AssetReference
├── Tools[]     : AssetReference
├── Memory?     : AssetReference
└── Policies[]  : AssetReference
```

It also carries the agent's name, goal, role, and responsibilities.

This is a concrete example of asset composition encoded by the current public contract. It does not establish a general grammar for unrelated asset types.

### Model

The current `ModelAssetOptions` contract contains:

```csharp
public sealed record ModelAssetOptions(
    string Provider,
    string Model);
```

Accordingly, the current AI Asset architecture does not claim that every persisted asset definition is provider-independent. Broader language-design goals concerning provider independence remain outside this document until specification reconciliation establishes their authority.

## Definition roles versus runtime behavior

AI Asset taxonomy and runtime capability are deliberately not symmetrical.

For example:

- Project, Library, and Package encode aggregate or grouping relationships through explicit references.
- Workflow encodes declarative workflow-step definitions.
- Agent encodes references to Model, Prompt, Knowledge, Tool, Memory, and Policy definitions.
- Model currently records provider/model selection.
- Other foundation asset types expose the definition data present in their own public contracts.

Nothing in this taxonomy alone guarantees that two asset kinds are realized, executed, persisted, or consumed in the same way.

That behavior belongs to the subsystem that owns the corresponding boundary.

## Handoff to the persistent asset platform

The AI Asset Model ends at declarative definitions and their encoded relationships.

The next architectural boundary is responsible for making those definitions durable, publishing them for resolution, and loading an aggregate graph rooted at a requested definition:

```text
AI Asset Definition
        │
        ▼
Persistent Asset Platform
        │
        ▼
AIAssetGraph
```

This document intentionally does not describe the mechanics of that platform. The AI Asset Model supplies the definitions and relationship information that the persistence and graph-loading architecture consumes.

Likewise, `AIAssetGraph` is not an execution result. It is the handoff from the persisted-definition platform to application realization.

## Scope boundaries

The AI Asset Model answers four questions:

1. What is an AI Asset?
2. How is an asset definition identified?
3. Which asset categories exist in the current public contract?
4. Which reference and composition relationships are explicitly encoded by current asset contracts?

It does not define:

- canonical serialization or storage mechanics;
- catalog publication or resolution algorithms;
- aggregate graph-loading rules;
- application realization;
- invocation or application-operation behavior;
- workflow runtime execution;
- provider execution behavior;
- future semantics for Knowledge, Memory, Policy, or other asset types;
- the normative grammar of the Application Language.

Those subjects belong to their own architectural or specification authorities.

## Related architecture

The canonical architecture overview places the AI Asset Model between the Application Language and the Persistent Asset Platform. This document supplies the current asset-definition model required by that lifecycle while deliberately leaving specification reconciliation and downstream implementation details to their owning documentation boundaries.
