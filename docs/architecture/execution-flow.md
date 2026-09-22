# Execution Flow

> **Execution crosses several framework boundaries. Describing the flow does not transfer ownership of downstream stages to the Workflow Runtime.**

## Purpose

This document provides a high-level map of what happens as a persisted PulseStackAI application reaches execution.

It answers:

> **Which authority owns each stage of the execution path?**

It is not the normative specification of Workflow Runtime traversal, executor selection, result aggregation, Agent execution, tool execution, provider communication, persistence, or realization.

Use the owning architecture document for exact behavior.

## End-to-end authority flow

For the integrated persisted-application path:

```text
persisted Project identity
        ↓
IApplicationOperation
        ↓
graph loading
        ↓
IApplicationRealizer
        ↓
RealizedApplication
        ↓
IApplicationInvoker
        ↓
Workflow + caller-created PipelineContext
        ↓
IWorkflowRuntime
        ↓
IStepExecutor
        ↓
IAgentExecutionRuntime when a RunStep requires Agent work
        ↓
downstream Agent / provider capabilities
        ↓
WorkflowExecutionResult
        ↓
ApplicationInvocationResult
```

The arrows describe handoffs between authorities. They do not imply that `IWorkflowRuntime` owns every stage shown below it.

## Before Workflow Runtime

### Persistence and graph loading

Declarative Assets are mapped, stored, published, resolved, and loaded through the Persistent Asset Platform.

That work occurs before runtime Workflow execution and is not a Workflow Runtime responsibility.

See [Persistent Asset Platform](persistent-asset-platform.md).

### Application realization

Application Realization accepts a Project-rooted `AIAssetGraph` and composes the runtime application representation.

For the entry Workflow, realization is the boundary that turns the accepted declarative Workflow Asset representation into the runtime `Workflow` used for execution.

```text
WorkflowAsset
        ↓
Application Realization
        ↓
Workflow
```

Workflow Runtime does not perform this declarative-to-runtime conversion.

See [Application Realization](runtime-realization-architecture.md).

### Application invocation and execution context

The integrated application boundary coordinates loading, realization, and invocation through `IApplicationOperation`.

Application Invocation creates a fresh `PipelineContext` from the invocation request and hands the realized `Workflow`, that context, and the cancellation token to `IWorkflowRuntime`.

```text
ApplicationInvocationRequest
        ↓
Application Invocation
        ↓
Workflow
+ PipelineContext
+ CancellationToken
        ↓
IWorkflowRuntime
```

Workflow Runtime receives the context; it does not create it.

See [Application Operation & Invocation](application-operation.md).

## Workflow Runtime boundary

At its public boundary, Workflow Runtime receives:

```text
Workflow
+
PipelineContext
+
CancellationToken
        ↓
IWorkflowRuntime
        ↓
WorkflowExecutionResult
```

The concrete runtime performs ordered top-level traversal and selects registered step executors according to its current implementation.

Nested/composite execution has its own resolver path, and runtime result projection has precise semantics.

Those details belong exclusively to [Workflow Runtime](workflow-runtime.md) and are intentionally not duplicated here.

## Step execution

Workflow steps are executed by `IStepExecutor` implementations.

Conceptually:

```text
runtime Workflow step
        ↓
appropriate executor
        ↓
StepExecutionResult
```

Different executors own different step semantics.

This document does not define a generic top-level “Step Dispatcher” abstraction. The exact distinction between top-level executor selection and nested/composite resolution is documented by the Workflow Runtime authority.

## Agent execution handoff

A runtime Run step crosses from Workflow execution into Agent execution:

```text
RunStep
        ↓
RunStepExecutor
        ↓
IAgentExecutionRuntime
        ↓
Agent execution
```

The handoff is part of the end-to-end execution flow.

Agent execution itself is not owned by Workflow Runtime. Prompt construction, tools, model interaction, memory behavior, and provider communication belong to their downstream authorities.

## Tools and external capabilities

An Agent may use tools or other capabilities during its own execution.

That possibility can be shown in an end-to-end application flow:

```text
Workflow Runtime
        ↓
RunStepExecutor
        ↓
Agent Runtime
        ↓
tool / external capability when required
```

This diagram does not establish a Tool Registry, tool lifecycle, or tool-execution contract for Workflow Runtime.

Workflow Runtime's ownership stops at its existing executor boundaries.

## Provider-backed execution

Provider integration is also downstream from Workflow Runtime.

A provider-backed path can conceptually reach:

```text
RunStep
        ↓
Agent execution
        ↓
configured model/provider integration
        ↓
Agent result
        ↓
StepExecutionResult
```

The Workflow Runtime remains separated from provider-specific communication, but that separation does not mean provider/model selection is absent from application definitions. Model Assets can explicitly identify their provider and model.

## Execution state

`PipelineContext` is mutable execution state supplied to `IWorkflowRuntime`.

The same context participates in Workflow-step execution according to the semantics of the current runtime and executors.

This document does not define isolation, transactional behavior, parallel mutation ordering, context ownership, or context disposal. Those details must not be inferred from this high-level flow.

See [Workflow Runtime](workflow-runtime.md) for the current state and concurrency boundaries.

## Runtime lifecycle events

Workflow Runtime emits its current Workflow/Step lifecycle events through the runtime event-dispatch boundary.

This document intentionally does not present a combined Workflow/Agent/Tool event stream. Describing downstream Agent or Tool work does not make their lifecycle events Workflow Runtime events.

Exact Workflow Runtime event behavior belongs to [Workflow Runtime](workflow-runtime.md).

## Failures, retry, and cancellation

Failure behavior is stage-specific.

Workflow Runtime propagates executor failures according to its current contract. Individual Workflow constructs can own explicit semantics—for example, Retry can repeat child execution based on the child result.

This does not establish:

- generic exception retry;
- parallel branch isolation;
- compensation semantics;
- human-approval semantics.

Application Operation, Application Invocation, Workflow Runtime, and individual executors each retain their own documented failure and cancellation responsibilities.

## Completion

Workflow Runtime returns a `WorkflowExecutionResult` to its caller.

For the integrated application path, Application Invocation then projects its own `ApplicationInvocationResult`, and `IApplicationOperation` exposes the stage-preserving operation outcome.

```text
WorkflowExecutionResult
        ↓
Application Invocation
        ↓
ApplicationInvocationResult
        ↓
ApplicationOperationResult
```

The exact Workflow result fields and top-level result aggregation remain defined by [Workflow Runtime](workflow-runtime.md).

## Authority map

| Stage | Current authority |
| --- | --- |
| AI Asset persistence / publication / graph loading | [Persistent Asset Platform](persistent-asset-platform.md) |
| Declarative graph → runtime application | [Application Realization](runtime-realization-architecture.md) |
| Integrated load → realize → invoke coordination | [Application Operation & Invocation](application-operation.md) |
| Invocation request → fresh execution context | [Application Operation & Invocation](application-operation.md) |
| Runtime Workflow execution | [Workflow Runtime](workflow-runtime.md) |
| Workflow-step semantics | current `IStepExecutor` implementations |
| Run-step handoff to Agent execution | `RunStepExecutor` → `IAgentExecutionRuntime` |
| Agent/tool/provider internals | downstream authorities, not Workflow Runtime |

## Summary

Execution in PulseStackAI is a chain of explicit handoffs rather than one runtime owning the entire application lifecycle.

```text
Persist / Publish / Load
        ↓
Realize
        ↓
Invoke
        ↓
Workflow Runtime
        ↓
Step Executor
        ↓
Agent / provider work when required
        ↓
Results return through the owning boundaries
```

Use this document to understand the cross-boundary flow. Use the linked architecture documents for the exact contract and semantics of each stage.
