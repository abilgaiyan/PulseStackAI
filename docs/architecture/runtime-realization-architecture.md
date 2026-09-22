# Application Realization Architecture

> **Application Realization converts one accepted declarative `AIAssetGraph` into a `RealizedApplication` without executing it.**

This document describes the current application-realization boundary. It begins with the `AIAssetGraph` produced by the Persistent Asset Platform and ends with a `RealizedApplication` suitable for the next authority: Application Invocation.

Graph-loading mechanics, invocation, integrated application-operation coordination, workflow execution, agent execution, provider execution, and language-specification reconciliation are outside this document.

## Boundary at a glance

```text
AIAssetGraph
      │
      ▼
IApplicationRealizer
      │
      ▼
ApplicationRealizationResult
      │
      └── Success
             │
             ▼
      RealizedApplication
             │
             ▼
[next authority: Application Invocation]
```

Realization is a coordination and composition boundary. It does not run the realized Workflow.

## Input authority: AIAssetGraph

`IApplicationRealizer` accepts one `AIAssetGraph`:

```csharp
Task<ApplicationRealizationResult> RealizeAsync(
    AIAssetGraph graph,
    CancellationToken cancellationToken = default);
```

The graph is already the output of persistent aggregate loading. Application Realization does not own storage, publication, catalog lookup policy, or recursive graph loading.

Its responsibility begins only after an aggregate graph has been accepted by the graph-loading boundary.

```text
Persistent Asset Platform
        │
        ▼
AIAssetGraph
        │
════════════════════════════
  Application Realization
════════════════════════════
        │
        ▼
RealizedApplication
```

## Project-root application boundary

The persistent graph loader supports Project, Library, and Package aggregate roots. Application Realization narrows that broader graph capability to an executable application root.

The current realizer accepts a Project-rooted application for successful realization. Library- and Package-rooted graphs produce the structured `UnsupportedRoot` realization outcome.

This establishes an important distinction:

```text
Aggregate graph root
  Project | Library | Package

Application realization root
  Project
```

A Project therefore supplies the application identity and the authored entry-Workflow reference used by realization.

## Realization authority

`IApplicationRealizer` is the public coordination authority for application realization.

The current implementation performs the following high-level work:

```text
AIAssetGraph
      │
      ▼
Locate Project root
      │
      ▼
Read Project EntryWorkflow reference
      │
      ▼
Resolve within accepted graph
      │
      ▼
Compose existing Workflow runtime representation
      │
      ▼
Create RealizedApplication
```

These steps describe realization responsibility, not a new persistence or execution pipeline.

### Graph-scoped resolution

Realization resolves application definitions from the accepted graph snapshot. The current implementation constructs a graph-backed resolver over that `AIAssetGraph`.

The Project's `EntryWorkflow` reference must resolve coherently within that graph before Workflow composition can proceed.

Realization does not return to the persistent catalog to construct a second aggregate. The accepted graph is the declarative input authority for this operation.

### Existing realization chain

The realizer does not introduce a second Workflow composition engine.

`IApplicationRealizationChainFactory` creates the existing Workflow realization chain bound to an explicit asset resolver:

```csharp
IWorkflowComposer Create(IAssetResolver assetResolver);
```

The resulting composer realizes the resolved `WorkflowAsset` into the runtime `Workflow` representation already understood by the execution runtime.

```text
WorkflowAsset
   declarative definition
        │
        ▼
existing Workflow realization chain
        │
        ▼
Workflow
   runtime representation
```

Application Realization coordinates that existing capability at the Project/application boundary.

## ApplicationRealizationResult

Realization returns an explicit result algebra:

```text
ApplicationRealizationResult
├── Success
├── UnsupportedRoot
├── EntryWorkflowUnresolved
└── EntryWorkflowTypeIncoherent
```

### Success

`ApplicationRealizationResult.Success` contains the resulting `RealizedApplication`. Its `Workflow` convenience property projects the same runtime Workflow held by that application.

### UnsupportedRoot

A Library- or Package-rooted graph is valid at the aggregate graph-loading boundary but is not a realizable application root under the current application-realization contract.

The result retains the unsupported root's `AssetDefinitionKey`.

### EntryWorkflowUnresolved

A Project may identify an entry Workflow reference that cannot be resolved from the accepted graph. The result retains both the Project root key and the authored entry-Workflow reference.

### EntryWorkflowTypeIncoherent

If the entry-Workflow reference does not resolve to the expected concrete `WorkflowAsset`, realization returns a type-incoherence outcome retaining the same Project and entry-Workflow provenance.

These are realization outcomes. They are not graph-load, invocation, or workflow-execution outcomes.

## RealizedApplication

A successful realization produces `RealizedApplication`.

Its public state is deliberately small:

```text
RealizedApplication
├── Project       : AssetReference
├── EntryWorkflow : AssetReference
└── Workflow      : Workflow
```

### Project provenance

`Project` identifies the Project Asset from which the application was realized.

It preserves the Project's asset type, ID, URN, and version through an `AssetReference`.

### Entry-Workflow provenance

`EntryWorkflow` preserves the Project's authored Workflow reference.

The contract requires this reference to identify a Workflow Asset.

### Realized Workflow

`Workflow` is the runtime Workflow representation composed from the declarative entry `WorkflowAsset`.

It is the representation expected by the downstream execution runtime. It is not another persisted asset definition.

## Realized does not mean executing

A `RealizedApplication` is **the output of realization and the input to invocation**.

It is not an independently executable application, execution engine, host, queue, background worker, or runtime session.

```text
AIAssetGraph
      │
      ▼
Realize
      │
      ▼
RealizedApplication
      │
      │  no execution has occurred
      ▼
Application Invocation
      │
      ▼
Workflow Runtime
```

The presence of a runtime `Workflow` inside `RealizedApplication` means that the declarative entry Workflow has been composed into the representation needed for execution. It does not mean that execution has started.

Execution authority remains downstream.

## Identity and provenance continuity

Realization preserves the identities required by later invocation:

```text
Project graph root
      │
      └── Project AssetReference
               │
               ▼
        RealizedApplication.Project

Project.EntryWorkflow
      │
      └── Workflow AssetReference
               │
               ▼
        RealizedApplication.EntryWorkflow

Resolved WorkflowAsset
      │
      ▼
Workflow composition
      │
      ▼
RealizedApplication.Workflow
```

This allows invocation results to retain Project and entry-Workflow provenance without reconstructing those identities from runtime state.

## Cancellation and failures

`RealizeAsync` accepts a `CancellationToken` and observes cancellation before realization work proceeds. Cancellation is not converted into one of the non-exceptional `ApplicationRealizationResult` cases.

The explicit result cases describe expected realization outcomes represented by the public result algebra. Contract violations or other exceptional failures remain exceptions rather than being silently converted into a generic realization result.

This document does not redefine the detailed failure semantics of the underlying Workflow composition chain.

## Responsibility summary

| Boundary | Responsibility | Does not own |
| --- | --- | --- |
| `AIAssetGraph` | Accepted declarative aggregate input | realization or execution |
| `IApplicationRealizer` | Coordinate Project-root application realization | graph loading, invocation, execution |
| graph-backed resolver | Resolve definitions from the accepted graph | persistent aggregate loading |
| `IApplicationRealizationChainFactory` | Bind the existing Workflow realization chain to the operation resolver | application invocation |
| Workflow composer | Compose declarative `WorkflowAsset` into runtime `Workflow` | execute that Workflow |
| `ApplicationRealizationResult` | Preserve realization-stage outcome | invocation/runtime outcome |
| `RealizedApplication` | Carry Project/entry-Workflow provenance and realized Workflow to invocation | independently execute the application |

## Scope boundary

Application Realization owns:

```text
AIAssetGraph
      ↓
Project application selection
      ↓
Entry Workflow resolution
      ↓
Workflow composition
      ↓
RealizedApplication
```

It does **not** own:

- persistent graph-loading mechanics;
- `IApplicationInvoker` behavior;
- `IApplicationOperation` coordination;
- `ApplicationInvocationRequest` projection;
- `PipelineContext`;
- `IWorkflowRuntime`;
- workflow or step execution;
- agent execution;
- provider execution;
- Application Language specification semantics.

The next authority receives the `RealizedApplication` and decides how to invoke it. That invocation boundary is intentionally separate from realization.
