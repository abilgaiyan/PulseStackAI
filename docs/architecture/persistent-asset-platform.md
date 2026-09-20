# Persistent Asset Platform

> **The Persistent Asset Platform makes declarative AI Asset definitions durable, published, resolvable, and loadable as an aggregate `AIAssetGraph`.**

This document describes the current architecture from an AI Asset definition through aggregate graph loading. Its output boundary is `AIAssetGraph`.

Application realization, invocation, `IApplicationOperation`, workflow execution, provider behavior, legacy workflow-specific persistence, framework NuGet distribution, and language-specification reconciliation are outside this document.

## Boundary at a glance

```text
AI Asset Definition
        │
        ▼
Canonical AI Asset Document
        │
        ▼
Validation / Canonical Serialization
        │
        ▼
Serialized Storage
        │
        ▼
Publication
        │
        ▼
Catalog / Persistent Resolution
        │
        ▼
Aggregate Graph Loading
        │
        ▼
AIAssetGraph
```

The stages have distinct responsibilities. Storage does not by itself make a definition published. Resolution does not recursively load an application graph. Graph loading does not realize or execute the graph.

## From asset definition to canonical document

The public `IAIAssetDocumentMapper` maps between an immutable `IAsset` definition and its canonical persistence document:

```csharp
AIAssetDocument ToDocument(IAsset asset);

IAsset FromDocument(AIAssetDocument document);
```

`AIAssetDocument` is the common persistence-document boundary, with concrete document forms for supported AI Asset definitions, including Project, Library, Package, Workflow, Agent, Prompt, Tool, Knowledge, Memory, Policy, and Model.

Mapping and validation are separate responsibilities. The mapper's contract explicitly requires externally sourced documents to be structurally validated before `FromDocument` is called. Defensive reconstruction checks do not replace aggregate document validation.

The persistence document is not a runtime object and is not an `AIAssetGraph`. It is the canonical document representation of one asset definition.

## Validation and canonical serialization

`IAIAssetDocumentValidator` owns structural validation of AI Asset documents.

`IAIAssetDocumentCodec` owns serialization and deserialization of the canonical document representation. Its public surface supports UTF-8 byte representations, strings, and stream-based operations.

```text
AIAssetDocument
      │
      ├── validate
      │
      ▼
Structurally valid document
      │
      ▼
IAIAssetDocumentCodec
      │
      ▼
Canonical serialized representation
```

The current codec implementation uses the canonical AI Asset JSON profile. Serialization is therefore part of the canonical persistence contract rather than an arbitrary storage-provider format.

Deserialization reconstructs a document representation. Reconstructing an `IAsset` from externally sourced content still respects the validation and mapping boundaries described above.

## Serialized storage

Serialized storage is keyed by `AssetDefinitionKey`, which identifies one typed and versioned asset definition.

At the lowest public storage boundary, `ISerializedAIAssetStore` reads and writes one exact serialized representation:

```csharp
ValueTask<SerializedAIAssetReadResult> ReadAsync(
    AssetDefinitionKey key,
    CancellationToken cancellationToken = default);

ValueTask<AIAssetWriteResult> WriteAsync(
    AssetDefinitionKey key,
    ReadOnlyMemory<byte> representation,
    CancellationToken cancellationToken = default);
```

The higher-level storage authorities are:

- `IAIAssetWriter` — writes the canonical representation of exactly one definition.
- `IAIAssetLoader` — loads and reconstructs exactly one definition by immutable definition key.

`IAIAssetWriter` accepts either an `AIAssetDocument` or an already serialized representation. `IAIAssetLoader` returns the storage/load result for one exact definition.

The framework currently provides in-memory and file-backed serialized-store implementations. Those providers implement the same storage authority; provider choice does not change the identity of the stored definition.

### Storage is not publication

A successful write makes an exact definition durable in the configured store. It does **not** by itself make that definition observable through the persistent catalog.

That distinction is intentional:

```text
Store definition
      │
      │ definition exists durably
      ▼
Publish definition
      │
      │ definition becomes catalog-observable
      ▼
Resolve definition
```

This separation permits persistence and publication to retain independent contracts and outcomes.

## Publication

`IAIAssetPublisher` publishes one already-stored exact definition:

```csharp
ValueTask<AIAssetPublicationResult> PublishAsync(
    AssetDefinitionKey key,
    CancellationToken cancellationToken = default);
```

Publication bridges stored definitions into the persistent catalog. The publisher does not author the asset, serialize a replacement definition, realize runtime objects, or recursively load a graph.

The underlying portable catalog provider exposes exact lookup, lineage lookup, and publication of catalog records through `IAIAssetCatalogProvider`.

The architectural sequence is therefore:

```text
AssetDefinitionKey
      │
      ├── exact serialized definition in storage
      │
      ▼
IAIAssetPublisher
      │
      ▼
Persistent Catalog
```

Publication concerns catalog observability of an already-stored definition, not runtime execution.

## Catalog and persistent resolution

The persistent catalog provides two important lookup perspectives:

- exact definition lookup by `AssetDefinitionKey`;
- lineage discovery by `AssetUrn`.

`IPersistentAIAssetResolver` is the public resolution boundary over the persistent catalog and the single-definition loader. It can resolve a published definition using:

```text
AssetDefinitionKey
AssetReference
AssetUrn + AssetVersion
```

and can discover lineage by `AssetUrn`.

Its contract is intentionally narrower than aggregate loading:

> persistent resolution resolves published declarative definitions; it performs no runtime realization or recursive graph loading.

This separation is important because resolving one definition and materializing the complete aggregate required by an application are different operations.

## Aggregate graph loading

`IAIAssetGraphLoader` is the aggregate boundary:

```csharp
ValueTask<AIAssetGraphLoadResult> LoadAsync(
    AssetDefinitionKey rootKey,
    CancellationToken cancellationToken = default);
```

The graph root must identify a Project, Library, or Package definition.

Graph loading starts from that root and expands the relationships recognized by the current graph-loading contract through persistent resolution. It produces either a successful `AIAssetGraph` or a structured graph-load failure.

The graph model contains:

```text
AIAssetGraph
├── RootKey
├── Nodes[]
│   ├── DefinitionKey
│   └── IAsset
└── Relationships[]
    ├── SourceKey
    ├── TargetReference
    ├── RelationshipClass
    ├── MaterializationAuthority
    ├── BoundaryRole
    ├── DependencyRequired?
    ├── LocalOrdinal
    └── AuthoredPath
```

Each materialized node pairs an exact `AssetDefinitionKey` with the corresponding `IAsset`. The graph also preserves authored relationship information and the graph-loading semantics associated with those relationships.

This document does not redefine those relationship rules as a general Application Language grammar. They are graph-loading contract semantics used to construct the persisted aggregate.

## Required and excluded materialization

The graph contract distinguishes relationships whose targets must be materialized from relationships excluded from materialization.

For explicit `AssetDependency` relationships, the authored `Required` value determines that materialization authority. Other graph relationship classes recognized by the current contract carry their own defined boundary and materialization semantics.

A successful graph therefore represents the definitions materialized under the graph-loading contract; it is not merely a bag of every reference visible anywhere in the root definition.

The graph contract also preserves canonical relationship occurrence information such as authored path and local ordinal so failures and graph structure can identify the relevant relationship precisely.

## Graph-load outcomes

`AIAssetGraphLoadResult` is an explicit result algebra rather than a nullable graph.

Current terminal result categories are:

```text
Success
RootDefinitionUnavailable
RequiredDefinitionUnavailable
ReferenceIdentityConflict
LineageIdentityConflict
RequiredMaterializationCycle
```

A successful result contains the `AIAssetGraph`.

Failure results retain structured context, including the requested root and, for relationship-originated failures, a canonical graph path and the relationship occurrence responsible for the failure.

These are persistent aggregate-loading outcomes. They are not realization or invocation outcomes.

## AIAssetGraph: the platform output boundary

A successful `AIAssetGraph` is an immutable aggregate snapshot containing a root definition, materialized nodes, and ordered relationship information accepted by the graph contract.

```text
Persistent Asset Platform
        │
        ▼
AIAssetGraph
        │
        ▼
Application Realization
```

This is where the Persistent Asset Platform stops.

The graph contains declarative AI Asset definitions. It does not contain an executed workflow, an invocation context, an application result, or provider response.

Application realization is the next architectural authority and consumes an accepted graph according to its own contract.

## Relationship to earlier workflow persistence

PulseStackAI also contains earlier workflow-specific persistence contracts such as `WorkflowDocument` and workflow stores.

Those contracts are not assimilated into this architecture and are not presented as the canonical persisted-application path.

The Persistent Asset Platform described here is the AI Asset path:

```text
IAsset
  ↓
AIAssetDocument
  ↓
canonical serialized storage
  ↓
catalog publication / resolution
  ↓
AIAssetGraph
```

Historical or specialized workflow-persistence documentation can therefore be preserved without creating a second canonical application-persistence architecture.

## Package Asset versus NuGet package

A Package Asset belongs to the AI Asset model and may participate as a supported aggregate graph root.

A NuGet package distributes compiled PulseStackAI framework assemblies to .NET consumers.

NuGet package production, local feeds, package-version provenance, and consumer dependency adoption are framework distribution concerns and are outside the Persistent Asset Platform.

## Responsibility summary

The subsystem boundaries can be summarized as follows:

| Boundary | Responsibility | Does not own |
| --- | --- | --- |
| `IAIAssetDocumentMapper` | Asset definition ↔ canonical persistence document | aggregate validation, storage, realization |
| `IAIAssetDocumentValidator` | Structural document validation | storage or runtime execution |
| `IAIAssetDocumentCodec` | Canonical document serialization/deserialization | catalog publication |
| `ISerializedAIAssetStore` | Exact serialized representation by definition key | asset graph expansion |
| `IAIAssetWriter` / `IAIAssetLoader` | Write/load one exact definition | publication or recursive graph loading |
| `IAIAssetPublisher` | Make an already-stored definition catalog-observable | storage authoring or realization |
| `IPersistentAIAssetResolver` | Resolve published definitions / discover lineage | recursive graph loading or realization |
| `IAIAssetGraphLoader` | Load the aggregate rooted at Project, Library, or Package | application realization or execution |
| `AIAssetGraph` | Declarative aggregate handoff | runtime execution |

## Scope boundary

This document ends at `AIAssetGraph`.

It intentionally does not define:

- `IApplicationRealizer` or `RealizedApplication`;
- `IApplicationInvoker`;
- `IApplicationOperation`;
- workflow runtime execution;
- agent or provider execution;
- the legacy workflow-specific persistence model;
- Package Asset language semantics beyond the current graph/persistence contracts;
- NuGet package production or publishing;
- unresolved Application Language specifications.

Those subjects remain owned by their respective architecture, guide, specification, or engineering-record boundaries.
