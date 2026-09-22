# Workflow Runtime Architecture

> **The Workflow Runtime executes a realized runtime `Workflow` against a caller-supplied `PipelineContext` and returns a `WorkflowExecutionResult`.**

This document describes the current Workflow Runtime execution boundary. It begins at the `IWorkflowRuntime` handoff established by Application Invocation and stops at the execution authorities delegated to individual step executors.

Persistence, graph loading, application realization, application operation/invocation, Agent Runtime internals, tool execution, and provider/model communication are outside this document.

## Boundary at a glance

```text
Workflow
   +
caller-owned PipelineContext
   +
CancellationToken
        │
        ▼
IWorkflowRuntime
        │
        ▼
ordered top-level Workflow.Steps traversal
        │
        ▼
IStepExecutor
        │
        ▼
StepExecutionResult
        │
        ▼
WorkflowExecutionResult
```

The Workflow Runtime receives execution state. It does not create the `PipelineContext`.

## IWorkflowRuntime contract

The public execution contract is:

```csharp
Task<WorkflowExecutionResult> ExecuteAsync(
    Workflow workflow,
    PipelineContext context,
    CancellationToken cancellationToken = default);
```

The caller supplies the realized runtime `Workflow`, a mutable `PipelineContext`, and the cancellation token. For the integrated application path, Application Invocation creates a fresh `PipelineContext` from the `ApplicationInvocationRequest` and passes it here. Context creation remains an invocation responsibility.

## PipelineContext is shared mutable execution state

The current context surface contains `Input`, `CurrentOutput`, `Items`, `Steps`, and `ToolResults`. `WorkflowRuntime` receives this context and passes the same object to the selected executor for each top-level step.

Executors may read or mutate that shared state according to their own semantics. For example, `ParallelStepExecutor` assigns its combined output to `context.CurrentOutput`, while `LoopStepExecutor` assigns the current iteration value to `context.Items["CurrentItem"]`.

The runtime does not define a general transactional or isolation model for context mutation.

### Parallel shared-context behavior

Current parallel child execution passes the same mutable `PipelineContext` instance to child executors. That is an implementation fact, not a broader concurrency guarantee. It does **not** establish thread safety, isolation between parallel children, deterministic ordering of concurrent mutations, or transactional behavior.

## Ordered top-level execution

The concrete `WorkflowRuntime` traverses `Workflow.Steps` in their existing order. For each top-level step it selects the first registered `IStepExecutor` whose `CanExecute` method accepts that step:

```text
Workflow.Steps
      ↓ ordered foreach
registered IStepExecutor collection
      ↓ FirstOrDefault(CanExecute)
selected executor
      ↓
ExecuteAsync(step, context, token)
```

If no registered executor can execute a top-level step, execution fails with an `InvalidOperationException`. There is no separate generic “Step Dispatcher” abstraction in this top-level path.

## IStepExecutor boundary

Each executor implements:

```csharp
bool CanExecute(IWorkflowStep step);

Task<StepExecutionResult> ExecuteAsync(
    IWorkflowStep step,
    PipelineContext context,
    CancellationToken cancellationToken = default);
```

The executor owns the semantics of the step type it accepts. The Workflow Runtime owns top-level orchestration around those executor calls.

The current executor family includes `RunStepExecutor`, `PipelineStepExecutor`, `WorkflowStepExecutor`, `ConditionalStepExecutor`, `ParallelStepExecutor`, `LoopStepExecutor`, `RetryStepExecutor`, and `SwitchStepExecutor`.

## Top-level selection versus nested resolution

Top-level and nested execution use related but distinct selection mechanisms.

For top-level Workflow steps, `WorkflowRuntime` directly searches its registered `IStepExecutor` collection using `CanExecute`.

For nested and composite execution, composite executors derive from `CompositeStepExecutor` and delegate child resolution through `IStepExecutorResolver`:

```text
Composite executor
      ↓
ExecuteStepAsync(child)
      ↓
IStepExecutorResolver
      ↓
matching IStepExecutor
      ↓
child ExecuteAsync(...)
```

The current composite family includes Conditional, Parallel, Loop, Retry, and Switch execution. The architecture must not normalize top-level selection and nested resolution into a fictional common dispatcher.

## Nested Workflow recursion

A `Workflow` can itself appear as a workflow step. `WorkflowStepExecutor` handles that case by handing the nested Workflow back to `IWorkflowRuntime` with the same `PipelineContext` and cancellation token:

```text
nested Workflow
      ↓
WorkflowStepExecutor
      ↓
IWorkflowRuntime.ExecuteAsync(workflow, context, cancellationToken)
```

The nested `WorkflowExecutionResult` is summarized into the enclosing `StepExecutionResult`. This preserves recursive Workflow execution without flattening nested workflow structure into the outer runtime's top-level traversal.

## RunStep and Agent Runtime handoff

`RunStepExecutor` is the Workflow Runtime boundary to agent execution:

```text
RunStep
   ↓
RunStepExecutor
   ↓
IAgentExecutionRuntime
   ↓
agent execution result
   ↓
StepExecutionResult
```

The executor supplies the shared `PipelineContext`, an `AgentExecutionContext`, a `PipelineExecutionPolicy`, and the cancellation token to `IAgentExecutionRuntime`. This document stops at that handoff and does not assign Workflow Runtime ownership to Agent Runtime internals, tools, models, providers, prompts, or memory.

## Three distinct state/result surfaces

The runtime architecture has three different surfaces that must not be collapsed:

```text
PipelineContext
    mutable execution state shared during execution

StepExecutionResult
    result returned by an individual executor

WorkflowExecutionResult
    terminal workflow-level projection
```

Each `IStepExecutor` returns one `StepExecutionResult` for the step it was asked to execute. Composite and nested executors may summarize child execution into their enclosing step result; nested results are not automatically flattened into the outer Workflow Runtime's result collection.

At completion, the concrete runtime projects:

```text
WorkflowExecutionResult.Success
    = all collected top-level StepExecutionResults are successful

WorkflowExecutionResult.FinalOutput
    = context.CurrentOutput ?? string.Empty

WorkflowExecutionResult.Steps
    = collected top-level StepExecutionResults
```

In particular, `WorkflowExecutionResult.Steps` is **not** `PipelineContext.Steps`. The former is the runtime's collected top-level executor results; the latter is a separate mutable collection on `PipelineContext`.

## Runtime lifecycle events

The concrete `WorkflowRuntime` emits lifecycle events through `IRuntimeEventDispatcher`. For each Workflow execution it currently dispatches:

```text
WorkflowStartedEvent

for each top-level step:
    StepstartedEvent
    StepCompletedEvent

WorkflowCompletedEvent
```

This is more precise than saying that Workflow Runtime owns observability. The ownership chain is:

```text
WorkflowRuntime
      ↓ Dispatch(...)
IRuntimeEventDispatcher
      ↓
RuntimeEventDispatcher
      ├── retains dispatched runtime events
      └── forwards to optional IRuntimeObserver
```

The Workflow Runtime emits its lifecycle events; the dispatcher and observer subsystem own collection and observation behavior. This runtime path does not establish Workflow Runtime ownership of Agent or Tool lifecycle events.

## Observer failure isolation

`RuntimeEventDispatcher` forwards events to an optional `IRuntimeObserver`. Observer failures are caught by the dispatcher and do not break Workflow execution. This failure isolation belongs to the event-dispatch/observation boundary rather than to step execution semantics.

## Cancellation and executor failures

The Workflow Runtime passes its supplied cancellation token to the selected executor. Composite and nested paths likewise propagate the token to delegated execution calls.

The core runtime does not convert executor exceptions into a generic workflow result. If executor selection or execution throws, that failure propagates rather than being silently represented as an unsuccessful `WorkflowExecutionResult`.

Individual step types may implement their own explicit execution semantics. For example, `RetryStepExecutor` repeats child execution based on returned `StepExecutionResult.Success`. This does not establish a generic exception-retry policy for the Workflow Runtime.

## Responsibility summary

| Boundary | Responsibility | Does not own |
| --- | --- | --- |
| caller | Supply `Workflow`, `PipelineContext`, and cancellation token | Workflow traversal |
| `IWorkflowRuntime` | Execute one runtime Workflow against supplied context | context creation, application invocation |
| `WorkflowRuntime` | Ordered top-level traversal, executor selection, result collection, lifecycle-event dispatch | nested resolver abstraction, Agent Runtime internals |
| `IStepExecutor` | Execute an accepted workflow step | application loading/realization |
| `IStepExecutorResolver` | Resolve executors for delegated nested/composite execution | top-level `WorkflowRuntime` selection |
| `StepExecutionResult` | Represent one executor's returned result | workflow-level result accumulation |
| `WorkflowExecutionResult` | Project terminal top-level workflow outcome | `PipelineContext.Steps` |
| `IRuntimeEventDispatcher` | Receive runtime lifecycle events for dispatch | Workflow traversal |
| `RuntimeEventDispatcher` | Retain events and notify optional observer | Workflow execution semantics |
| `IAgentExecutionRuntime` | Downstream agent-execution authority reached by `RunStepExecutor` | Workflow Runtime orchestration |

## Scope boundary

The current Workflow Runtime architecture is:

```text
Workflow + caller-owned PipelineContext
        ↓
IWorkflowRuntime
        ↓
ordered top-level traversal
        ↓
CanExecute-based IStepExecutor selection
        ↓
StepExecutionResult accumulation
        ↓
WorkflowExecutionResult
```

It delegates nested/composite steps, nested Workflows, and RunStep agent execution to their existing authorities.

It does **not** own or imply `PipelineContext` creation, a generic Step Dispatcher abstraction, generic runtime-service initialization, generic execution-metadata ownership, flattened nested-result semantics, observability ownership, Agent or Tool lifecycle events without separate authority, Tool Registry ownership, tool execution, provider/model communication, Agent Runtime internals, persistence/catalog behavior, graph loading, application realization, application operation/invocation, compensation, human approval, or concurrency safety/isolation for shared mutable context.
