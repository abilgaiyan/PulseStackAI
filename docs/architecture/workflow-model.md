# Workflow Model

> **PulseStackAI has a declarative Workflow Asset representation and a separate realized runtime Workflow representation.**

This document owns the structural relationship between those representations.

It does not define persistence mechanics or runtime execution semantics.

## Representation map

The current application path crosses an explicit representation boundary:

```text
WorkflowAsset
    declarative AI Asset
        ↓
AI Asset persistence / graph loading
        ↓
application realization
        ↓
Workflow
    runtime representation
        ↓
IWorkflowRuntime
```

These objects are related, but they are not interchangeable.

## Declarative Workflow Asset

`WorkflowAsset` belongs to the AI Asset model.

Its authoring options contain:

```text
WorkflowAssetOptions
├── Name
├── Description?
└── Steps[]
    └── WorkflowStepDefinition
```

The Workflow Asset also carries the normal AI Asset identity, URN, version, metadata, references, and dependencies supplied by the shared Asset model.

A Workflow Asset describes process structure. It does not carry invocation state and is not executed directly by `IWorkflowRuntime`.

## Declarative step model

`WorkflowStepDefinition` is the base declarative step representation.

Every step owns a `WorkflowStepId`.

Current concrete definitions are:

```text
WorkflowStepDefinition
├── RunStepDefinition
├── ConditionalStepDefinition
├── ParallelStepDefinition
├── RetryStepDefinition
├── LoopStepDefinition
└── SwitchStepDefinition
```

Composite definitions contain other declarative step definitions, producing a recursive definition tree.

Conceptually:

```text
WorkflowAsset
├── Run
├── Conditional
│   ├── Then
│   └── Else?
├── Parallel
│   └── Steps[]
├── Retry
│   └── Step
├── Loop
│   └── Step
└── Switch
    ├── Cases[]
    └── Default?
```

The exact properties of each definition remain owned by the source contracts.

## Identity-complete durable authoring

A `WorkflowStepDefinition` can receive a generated default ID. Persisted applications that recreate the same logical Workflow definition need explicit identity continuity instead.

The durable authoring representation therefore adds:

```text
IdentityCompleteWorkflowStep
IdentityCompleteWorkflowAssetOptions
DurableWorkflowStep
```

An `IdentityCompleteWorkflowStep` wraps a declarative subtree authored through the explicit identity contract for every step in that subtree.

`IdentityCompleteWorkflowAssetOptions` contains those identity-complete root subtrees.

`WorkflowAssetFactory` converts that authoring representation into the ordinary declarative definitions held by a `WorkflowAsset`.

This distinction is about **how identity completeness is established during authoring**. It does not introduce a second persisted Workflow Asset type.

## Runtime Workflow

The runtime `Workflow` is the representation accepted by `IWorkflowRuntime`.

It belongs to the runtime Workflow object model rather than the AI Asset definition model.

The runtime model includes runtime `IWorkflowStep` objects and supports composition understood by the step executors.

One important runtime relationship is:

```text
Workflow : IWorkflowStep
```

That allows a realized runtime Workflow to participate as a runtime step.

This fact should not be projected backward onto `WorkflowAsset`: the declarative Asset is not itself a runtime `IWorkflowStep`.

## Realization connects the models

Application Realization owns the conversion from the accepted declarative graph to runtime representation.

For the entry Workflow:

```text
Project.EntryWorkflow
        ↓
resolve WorkflowAsset from AIAssetGraph
        ↓
existing Workflow realization chain
        ↓
Workflow
        ↓
RealizedApplication.Workflow
```

The realized `Workflow` is then available to application invocation and ultimately `IWorkflowRuntime`.

The Workflow Model therefore does not require persistence documents or runtime execution to share one concrete representation.

## Persistence relationship

Current persisted declarative applications use the AI Asset persistence architecture.

For a Workflow definition, the conceptual path is:

```text
WorkflowAsset
        ↓
AI Asset document mapping
        ↓
WorkflowAssetDocument / AIAssetDocument
        ↓
canonical serialization
        ↓
serialized storage
        ↓
catalog publication
        ↓
persistent resolution / graph loading
```

The canonical persistence authority is [Persistent Asset Platform](persistent-asset-platform.md).

PulseStackAI also contains an older Workflow-specific `WorkflowDocument` persistence generation. That representation is separate from the current AI Asset persisted-application path and is not the persistence model owned by this document.

## Execution relationship

The runtime representation is executed through:

```csharp
Task<WorkflowExecutionResult> ExecuteAsync(
    Workflow workflow,
    PipelineContext context,
    CancellationToken cancellationToken = default);
```

That contract belongs to `IWorkflowRuntime`.

The Workflow Model does not define traversal, executor selection, parallel-state behavior, retry behavior, runtime events, cancellation precedence, or result aggregation. Those semantics are owned by [Workflow Runtime](workflow-runtime.md).

## Language relationship

The conceptual Workflow Language describes business process structure.

The durable authoring grammar turns that vocabulary into explicit-identity declarative definitions.

The model then separates those definitions from the runtime representation:

```text
Workflow Language
        ↓
durable declarative authoring
        ↓
WorkflowAsset
        ↓
realization
        ↓
Workflow
        ↓
runtime execution
```

See:

- [Workflow Language](workflow-language.md) for conceptual vocabulary;
- [Workflow Language Grammar](../guides/workflow-language/grammar.md) for current durable authoring;
- [Application Realization](runtime-realization-architecture.md) for the representation handoff;
- [Workflow Runtime](workflow-runtime.md) for execution.

## Older runtime builder

The repository also contains `Workflow.Create(...)`, `WorkflowBuilder`, and related builders that construct runtime `Workflow` objects.

Those APIs remain real repository contracts. They are not erased by the declarative Workflow Asset model.

Their representation target is different:

```text
WorkflowBuilder
        ↓
Workflow
        ↓
IWorkflowRuntime
```

The canonical persisted application path instead authors a `WorkflowAsset`, persists it through the AI Asset platform, and realizes it before runtime execution.

## Responsibility summary

| Concept | Representation | Authority |
| --- | --- | --- |
| Workflow business vocabulary | conceptual language | Workflow Language |
| Persistable declarative workflow | `WorkflowAsset` + `WorkflowStepDefinition` | AI Asset / Workflow model |
| Explicit durable step authoring | `DurableWorkflowStep` + identity-complete wrappers | Workflow grammar |
| Persistent application representation | `AIAssetDocument` / `WorkflowAssetDocument` path | Persistent Asset Platform |
| Declarative-to-runtime handoff | `WorkflowAsset` → `Workflow` | Application Realization |
| Runtime workflow | `Workflow : IWorkflowStep` | runtime object model |
| Runtime execution | `IWorkflowRuntime` | Workflow Runtime |

## Scope boundary

This document does not:

- make the older Workflow builder the canonical persisted authoring path;
- make `WorkflowAsset` a runtime `IWorkflowStep`;
- make the runtime `Workflow` the canonical persisted Asset representation;
- redefine AI Asset persistence;
- redefine realization;
- redefine runtime execution;
- reconcile the older `WorkflowDocument` specification.

Those responsibilities remain with their owning documents.
