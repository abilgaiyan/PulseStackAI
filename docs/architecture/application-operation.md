# Application Operation & Invocation Architecture

> **Application Operation is the integrated consumer boundary for executing one persisted Project; Application Invocation is the downstream boundary that hands one `RealizedApplication` to the existing Workflow Runtime.**

This document describes two nested current authorities:

1. `IApplicationOperation` coordinates graph loading, realization, and invocation for a persisted Project.
2. `IApplicationInvoker` invokes an already realized application by projecting an invocation request into a fresh `PipelineContext` and handing the realized `Workflow` to `IWorkflowRuntime`.

The document stops at the Workflow Runtime handoff. Workflow-step execution, agent execution, provider execution, and language-specification semantics are outside its scope.

## Boundaries at a glance

The integrated consumer path is:

```text
Project AssetDefinitionKey
        │
        ▼
IApplicationOperation
        │
        ├── Graph Load
        │
        ├── Realize
        │
        └── Invoke
                 │
                 ▼
        ApplicationOperationResult
```

Inside its invocation stage:

```text
RealizedApplication
        +
ApplicationInvocationRequest
        │
        ▼
IApplicationInvoker
        │
        ▼
fresh PipelineContext
        │
        ▼
IWorkflowRuntime
        │
        ▼
WorkflowExecutionResult
        │
        ▼
ApplicationInvocationResult
```

These are related but distinct responsibilities. Application Operation coordinates existing stage authorities; Application Invocation owns the handoff from a realized application into the existing Workflow Runtime.

## Integrated consumer boundary: IApplicationOperation

`IApplicationOperation` is the public coordination boundary for one persisted Project application operation:

```csharp
Task<ApplicationOperationResult> ExecuteAsync(
    AssetDefinitionKey projectKey,
    ApplicationInvocationRequest request,
    CancellationToken cancellationToken = default);
```

The caller supplies:

- the exact `AssetDefinitionKey` of the persisted Project;
- an `ApplicationInvocationRequest`;
- an optional cancellation token.

The operation then delegates to the existing authorities in sequence:

```text
Project AssetDefinitionKey
        │
        ▼
IAIAssetGraphLoader
        │
        ▼
AIAssetGraph
        │
        ▼
IApplicationRealizer
        │
        ▼
RealizedApplication
        │
        ▼
IApplicationInvoker
        │
        ▼
ApplicationInvocationResult
```

`IApplicationOperation` does not replace any of those authorities and does not introduce another execution engine.

## Stage-preserving operation result

`ApplicationOperationResult` preserves the authoritative stage that produced the terminal non-exceptional outcome:

```text
ApplicationOperationResult
├── LoadOutcome
├── RealizationOutcome
└── InvocationOutcome
```

### LoadOutcome

If graph loading does not succeed, the operation stops and preserves that `AIAssetGraphLoadResult`.

A successful graph-load result cannot be represented as a terminal `LoadOutcome`.

### RealizationOutcome

If graph loading succeeds but realization does not, the operation stops and preserves that `ApplicationRealizationResult`.

A successful realization result cannot be represented as a terminal `RealizationOutcome`.

### InvocationOutcome

Once graph loading and realization succeed, the operation invokes the resulting `RealizedApplication`. The returned `ApplicationInvocationResult` is preserved in `InvocationOutcome`.

The operation therefore coordinates stage progression without flattening each stage's result algebra into a generic success/failure value.

## Application Invocation

`IApplicationInvoker` owns invocation of one already realized application:

```csharp
Task<ApplicationInvocationResult> InvokeAsync(
    RealizedApplication application,
    ApplicationInvocationRequest request,
    CancellationToken cancellationToken = default);
```

Invocation begins after realization has already produced:

```text
RealizedApplication
├── Project
├── EntryWorkflow
└── Workflow
```

The invoker does not load assets or realize the application again. It consumes the exact `RealizedApplication` supplied by its caller.

## ApplicationInvocationRequest

The current request contract contains a string input and an optional item dictionary:

```csharp
public sealed class ApplicationInvocationRequest
{
    public string Input { get; }

    public IReadOnlyDictionary<string, object?> Items { get; }
}
```

`Input` is currently **`string`**. This architecture does not generalize it to `object` or another hypothetical request model.

The request snapshots supplied items using ordinal key semantics. Item keys must be non-empty and non-whitespace.

The request is application-level input. It is not itself the mutable runtime context.

## Fresh PipelineContext projection

For each admitted invocation, the invoker projects the request into a new `PipelineContext`.

The current projection is exact:

```text
ApplicationInvocationRequest.Input
        ├──→ PipelineContext.Input
        └──→ PipelineContext.CurrentOutput

ApplicationInvocationRequest.Items
        └──→ PipelineContext.Items
```

In code-equivalent terms:

```csharp
var context = new PipelineContext
{
    Input = request.Input,
    CurrentOutput = request.Input
};

foreach (var item in request.Items)
{
    context.Items.Add(item.Key, item.Value);
}
```

The invocation boundary does not reuse a previous invocation's `PipelineContext`. The request is projected into fresh mutable runtime state for the admitted invocation.

Other runtime-owned collections on `PipelineContext`, such as step and tool results, begin with the new context's normal empty state.

## Exact Workflow Runtime handoff

After context projection, invocation calls the existing Workflow Runtime:

```csharp
Task<WorkflowExecutionResult> ExecuteAsync(
    Workflow workflow,
    PipelineContext context,
    CancellationToken cancellationToken = default);
```

The handoff uses:

- `application.Workflow`;
- the freshly projected `PipelineContext`;
- the invocation cancellation token.

```text
RealizedApplication.Workflow
        +
fresh PipelineContext
        +
CancellationToken
        │
        ▼
IWorkflowRuntime.ExecuteAsync(...)
```

This is the terminal execution handoff owned by this document.

`IApplicationInvoker` does not define how the Workflow Runtime traverses steps, executes agents, invokes tools, or communicates with providers. Those are downstream runtime responsibilities.

## ApplicationInvocationResult

After `IWorkflowRuntime` returns a `WorkflowExecutionResult`, invocation projects it into an `ApplicationInvocationResult`.

The public result preserves:

```text
ApplicationInvocationResult
├── Project       : AssetReference
├── EntryWorkflow : AssetReference
├── Success       : bool
├── FinalOutput   : string
└── Steps         : StepExecutionResult[]
```

Project and entry-Workflow provenance come from the supplied `RealizedApplication`; invocation does not reconstruct those identities from runtime state.

The result therefore combines application provenance with the projected terminal Workflow execution outcome.

## Sequential reuse and overlapping invocation

A `RealizedApplication` may be invoked sequentially, but overlapping invocation of the same concrete `RealizedApplication` instance is rejected.

The coordination authority is provider-owned and associates occupancy with concrete object identity using weak association.

Conceptually:

```text
RealizedApplication instance

Idle
 │
 │ acquire
 ▼
Active
 │
 │ release
 ▼
Idle
```

If the same instance is already active, another invocation is rejected immediately with an exception rather than queued.

This is an invocation-coordination rule. It does not turn `RealizedApplication` into a stateful execution engine; the coordination state is owned externally by the invocation authority.

## Cancellation precedence

Invocation checks cancellation before attempting admission and again after admission before runtime handoff.

Consequently, an already-cancelled request is not reported as same-instance contention merely because another invocation is active. Pre-cancellation takes precedence over the contention check.

The same cancellation token is then handed to `IWorkflowRuntime`.

Application Operation also passes its supplied cancellation token unchanged through graph loading, realization, and invocation.

## Release invariant and failure preservation

An admitted invocation owns a structured acquire/release lifecycle.

Release must observe the expected transition:

```text
Active → Idle
```

An impossible release transition is an internal coordination invariant violation.

The invocation boundary preserves the primary outcome ordering:

- if invocation/runtime work already failed or was cancelled, that primary exception remains authoritative;
- a release-invariant failure discovered afterward is reported separately and does not replace that existing primary failure;
- if no primary invocation failure exists, a release-invariant failure may surface.

This keeps coordination corruption detectable without changing the exception or cancellation already produced by admitted invocation work.

## Invocation is coordination, not a runtime

Application Invocation performs three application-level responsibilities:

```text
admit realized application invocation
        ↓
project request into fresh runtime context
        ↓
hand exact Workflow/context/token to IWorkflowRuntime
        ↓
project runtime result with application provenance
```

It does not implement Workflow execution semantics.

Likewise, Application Operation performs:

```text
load
  ↓
realize
  ↓
invoke
```

without taking ownership of the implementation semantics inside any of those stages.

The architecture therefore remains layered:

```text
Persistent Project
        ↓
Application Operation
        ↓
Application Realization
        ↓
Application Invocation
        ↓
IWorkflowRuntime
        ↓
Workflow Execution
```

## Responsibility summary

| Boundary | Responsibility | Does not own |
| --- | --- | --- |
| `IApplicationOperation` | Coordinate load → realize → invoke for one persisted Project | graph-loading, realization, or runtime implementation |
| `ApplicationOperationResult` | Preserve terminal non-exceptional stage outcome | flatten stage-specific results |
| `ApplicationInvocationRequest` | Carry string input and invocation items | mutable runtime execution state |
| `IApplicationInvoker` | Coordinate invocation of a `RealizedApplication` | persistence, realization, Workflow execution semantics |
| invocation coordination authority | Enforce non-overlap for the same realized-application instance | application hosting or queueing |
| context projection | Create fresh `PipelineContext` from request | Workflow traversal |
| `IWorkflowRuntime` | Receive exact Workflow/context/token for execution | application loading or realization |
| `ApplicationInvocationResult` | Return application provenance and projected Workflow result | persistence or realization outcome |

## Scope boundary

This document owns the integrated application-operation and application-invocation boundaries.

It intentionally does not define:

- AI Asset persistence or graph-loading mechanics;
- application-realization internals beyond consuming its result;
- Workflow Runtime traversal or step dispatch;
- Workflow step semantics;
- agent execution;
- tool execution;
- provider/model execution;
- hosting, queues, or background execution;
- NuGet package distribution;
- Application Language specification semantics.

The terminal handoff for this architecture is `IWorkflowRuntime`. The implementation and documentation authority for Workflow execution begins downstream of that interface.
