# Workflow Execution

> **A business-readable mental model for thinking about Workflow execution.**

This document explains execution conceptually. It does not define the exact `IWorkflowRuntime` contract, traversal algorithm, executor-selection rules, state-isolation guarantees, retry semantics, event model, or result projection.

For those implementation semantics, use [Workflow Runtime](workflow-runtime.md).

## Start with intent

Imagine explaining a business process to a colleague.

You usually begin with what needs to happen:

> Read the document.

> Review the contract.

> Approve the expense.

> Notify the customer.

That business objective is the starting point for Workflow design.

PulseStackAI keeps that intent separate from the framework mechanics that persist, realize, invoke, and execute the application.

## A useful mental model

A simple way to reason about Workflow execution is:

```text
Intent
  ↓
Workflow
  ↓
Step
  ↓
Execution State
  ↓
Next Decision
```

This is a conceptual model, not a runtime algorithm.

Each term has a more precise representation elsewhere in the architecture.

## 1. Intent

Intent answers:

> **What are we trying to accomplish?**

Examples include:

- review a contract;
- approve an expense;
- summarize documents;
- analyze an RFQ;
- classify a support request.

Intent should remain understandable independently of runtime infrastructure.

## 2. Workflow

Conceptually, a Workflow describes the orchestration structure of the business process.

PulseStackAI has two important Workflow representations:

```text
WorkflowAsset
    declarative application definition
        ↓ realization
Workflow
    runtime representation
```

The declarative `WorkflowAsset` is part of the AI Asset model. The realized runtime `Workflow` is what `IWorkflowRuntime` executes.

See [Workflow Model](workflow-model.md) for this representation boundary.

## 3. Step

A step describes one unit of Workflow structure or runtime work.

At the declarative level, current Workflow-step definitions include concepts such as:

- Run;
- Conditional;
- Parallel;
- Retry;
- Loop;
- Switch.

During realization, declarative definitions are composed into runtime Workflow steps understood by the existing executors.

A step does not need to own the entire application lifecycle. Its execution authority is bounded to the semantics of that step.

## 4. Execution state

During invocation, PulseStackAI projects the application request into a fresh `PipelineContext`.

That context is then supplied to `IWorkflowRuntime` with the realized runtime `Workflow`.

Conceptually, execution state lets later work observe information produced or supplied earlier.

The important ownership distinction is:

```text
Application Invocation
    creates the fresh PipelineContext

Workflow Runtime / executors
    receive and operate on that context
```

The context is shared mutable state in the current runtime. This mental model does not imply transactionality, parallel isolation, deterministic concurrent mutation ordering, or any other concurrency guarantee.

Exact state behavior belongs to [Workflow Runtime](workflow-runtime.md).

## 5. Next decision

Business processes move through different kinds of decisions:

- execute another unit of work;
- choose a conditional branch;
- run work in parallel;
- retry a child operation according to Retry semantics;
- iterate over values;
- select a switch branch;
- complete the Workflow.

The Workflow Language provides the vocabulary for expressing those structures.

The runtime and its executors determine the implemented execution mechanics.

Therefore “what happens next” is useful as a business mental model, but it should not be read as a substitute for the exact traversal and executor semantics documented by the runtime authority.

## A Workflow coordinates capabilities

A Workflow is often easier to understand as coordination among focused capabilities rather than as one intelligent component.

For example:

```text
receive business input
        ↓
run an Agent capability
        ↓
evaluate a business condition
        ↓
run additional work
        ↓
produce a result
```

When a Run step requires Agent execution, the runtime path crosses an explicit boundary:

```text
RunStep
        ↓
RunStepExecutor
        ↓
IAgentExecutionRuntime
```

Agent execution is downstream work. Describing it here does not transfer ownership of Agent internals, tools, models, memory, prompts, or providers to Workflow Runtime.

## Separation of responsibilities

A useful shorthand is:

```text
The Workflow describes orchestration.

The Workflow Runtime coordinates runtime Workflow execution.

Step executors own the semantics of their accepted steps.

Agent execution provides downstream AI capability when requested.

Provider integrations communicate with configured model providers.
```

Those responsibilities cooperate without becoming one authority.

The same principle applies before execution:

```text
Persistent Asset Platform
    owns persistence / publication / graph loading

Application Realization
    owns declarative graph → runtime application composition

Application Invocation
    owns invocation context projection and runtime handoff

Workflow Runtime
    owns runtime Workflow execution
```

## Business thinking versus infrastructure

Application authors should be able to begin with the business process instead of rebuilding orchestration infrastructure.

That does not mean application definitions contain no infrastructure-relevant choices.

For example:

- a Model Asset can explicitly select a provider and model;
- a Workflow can explicitly include Retry or Parallel structure;
- an application chooses which Assets compose its Project.

The framework owns the mechanics behind its boundaries; the application still declares the choices that are part of its definition.

## From description to execution

The complete conceptual progression is:

```text
Business Intent
        ↓
declarative AI Assets
        ↓
WorkflowAsset
        ↓
persistence / publication
        ↓
Project-rooted graph
        ↓
application realization
        ↓
runtime Workflow
        ↓
application invocation
        ↓
IWorkflowRuntime
        ↓
step execution
        ↓
downstream Agent/provider work when required
        ↓
result
```

This diagram describes cross-boundary flow. It does not make Workflow Runtime the owner of persistence, realization, invocation, Agent execution, tools, or providers.

## Where to go next

Use the document that owns the question you are asking:

- [Workflow Language](workflow-language.md) — conceptual business-process vocabulary;
- [Workflow Language Grammar](../guides/workflow-language/grammar.md) — current durable declarative authoring;
- [Workflow Model](workflow-model.md) — `WorkflowAsset` versus runtime `Workflow`;
- [Persistent Asset Platform](persistent-asset-platform.md) — persistence, publication, resolution, and graph loading;
- [Application Realization](runtime-realization-architecture.md) — declarative graph to runtime application;
- [Application Operation & Invocation](application-operation.md) — integrated operation and invocation context;
- [Workflow Runtime](workflow-runtime.md) — exact current runtime execution semantics;
- [Execution Flow](execution-flow.md) — cross-boundary execution authority map.

## Summary

The most useful execution questions remain simple:

> **What is the intent?**

> **What orchestration structure describes the process?**

> **What execution state is available?**

> **What decision or work comes next?**

PulseStackAI then carries that application through distinct persistence, realization, invocation, runtime, Agent, and provider boundaries.

The boundaries cooperate, but describing the complete flow never transfers ownership of a downstream stage to Workflow Runtime.
