# Workflow Language

> **Describe business process structure as declarative Workflow Assets; realize that structure separately for runtime execution.**

## Purpose

The Workflow Language is the business-oriented vocabulary used to describe orchestration in PulseStackAI.

It answers questions such as:

- What work should run?
- What work can run in parallel?
- What should happen only when a condition is satisfied?
- What should be retried?
- What collection should be traversed?
- Which branch should be selected?

It does not define persistence mechanics, application realization, runtime dispatch, or provider execution.

The current architecture keeps those responsibilities separate:

```text
Workflow concepts
        ↓
declarative Workflow Asset
        ↓
AI Asset persistence
        ↓
application realization
        ↓
runtime Workflow
        ↓
IWorkflowRuntime
```

## Workflow coordinates work

A Workflow coordinates application work. Agent Assets describe AI-capable participants and compose the other Assets they require.

At the application level:

```text
Project
  ↓
Entry Workflow
  ↓
Agent references
  ↓
Prompt / Model / Tools / Knowledge / Memory / Policies
```

A Workflow definition does not itself communicate with a model provider. Provider-backed execution occurs downstream through realization and runtime composition.

## Think in business process structure

Before choosing runtime mechanics, identify the process.

### Intent

Start with the business objective:

- review a contract;
- approve an expense;
- summarize documents;
- classify a support request;
- analyze an RFQ.

### Transitions

Then describe what happens next:

- run work;
- evaluate a condition;
- perform branches in parallel;
- retry work;
- iterate over values;
- select a switch branch.

### State

Runtime decisions operate over execution context, but the declarative Workflow Asset is not runtime state.

That distinction is deliberate:

```text
Workflow Asset
    describes process structure

PipelineContext
    carries invocation state during execution
```

The exact runtime behavior is owned by [Workflow Runtime](workflow-runtime.md).

## Current declarative vocabulary

The current declarative Workflow model is expressed through `WorkflowStepDefinition` types.

The implemented step kinds are:

```text
Run
Conditional
Parallel
Retry
Loop
Switch
```

These definitions form the structure stored inside a `WorkflowAsset`.

A Workflow Asset itself contains:

```text
WorkflowAsset
├── Asset identity
├── Name
├── Description
└── Steps[]
```

The Workflow Asset is part of the AI Asset model. It is not the runtime `Workflow` object executed by `IWorkflowRuntime`.

## Durable authoring and identity

`WorkflowStepDefinition.Id` has a default generated identity, which is useful for ordinary in-memory definition construction.

Persisted declarative applications need stronger identity continuity across application restarts. PulseStackAI therefore provides an explicit identity-complete authoring path:

```text
WorkflowStepId
        ↓
DurableWorkflowStep.*
        ↓
IdentityCompleteWorkflowStep
        ↓
IdentityCompleteWorkflowAssetOptions
        ↓
WorkflowAssetFactory
        ↓
WorkflowAsset
```

`DurableWorkflowStep` currently exposes explicit-identity construction for:

- `Run`;
- `Parallel`;
- `Conditional`;
- `Retry`;
- `Loop`;
- `Switch` and `SwitchCase`.

Each resulting identity-complete subtree guarantees that every Workflow step in that authored subtree received its identity through the explicit authoring contract.

For the complete persisted-application procedure, see [Build and Execute a Declarative Application](../guides/declarative-application.md).

## Language versus representation

Several related concepts coexist in the framework and should not be collapsed.

### Workflow Language

The conceptual vocabulary for expressing process structure.

### Workflow Asset

The declarative AI Asset representation used by the current persisted-application path.

### Runtime Workflow

The realized `Workflow` object consumed by `IWorkflowRuntime`.

### Workflow Runtime

The execution authority that receives the realized `Workflow`, caller-owned `PipelineContext`, and cancellation token.

Therefore:

```text
Workflow Language
        ≠
Workflow Asset
        ≠
runtime Workflow
        ≠
Workflow Runtime
```

## Relationship to the older Workflow builder

PulseStackAI also contains the `Workflow.Create(...)` / `WorkflowBuilder` authoring surface for constructing runtime `Workflow` objects.

That API remains part of the repository. Its continued existence does not make it the canonical persisted declarative application path.

Current persisted application guidance uses stable AI Asset identities, identity-complete Workflow-step authoring, AI Asset persistence/publication, and Project-rooted execution through `IApplicationOperation`.

The builder surface and the durable Workflow Asset authoring surface therefore answer different representation needs and should not be presented as one grammar.

## Responsibility boundary

This document owns the current conceptual Workflow Language and its relationship to declarative Workflow Assets.

It does not own:

- the detailed authoring procedure for a persisted application;
- AI Asset storage, publication, or graph loading;
- Workflow Asset realization;
- runtime execution semantics;
- the historical Workflow-specific persistence generation;
- reconciliation of the older Workflow document specification.

Use the owning documents instead:

- [Workflow Language Grammar](../guides/workflow-language/grammar.md) — current durable declarative authoring vocabulary;
- [Workflow Model](workflow-model.md) — declarative versus runtime representations;
- [Persistent Asset Platform](persistent-asset-platform.md) — current persistence authority;
- [Application Realization](runtime-realization-architecture.md) — Workflow Asset to runtime Workflow;
- [Workflow Runtime](workflow-runtime.md) — current execution semantics.
