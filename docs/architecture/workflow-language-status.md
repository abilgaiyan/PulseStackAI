# Workflow Language Status

**Project:** PulseStackAI  
**Status:** Phase 1 Complete – Workflow Foundation Established

---

# Purpose

This document records the current architectural state of the PulseStackAI Workflow Language.

It is not an API reference.

It is a design checkpoint that captures the language philosophy, the core abstractions, and the implementation roadmap before moving to the next phase.

---

# Vision

PulseStackAI is building a **Workflow Language**, not simply a pipeline execution framework.

The goal is to allow developers to describe business workflows using expressive, readable, domain-focused syntax while hiding execution complexity behind the runtime.

Example:

```csharp
Workflow.Create("Expense Approval")

    .Run(loadExpense)

    .If(requiresApproval)
        .Then()
            .Run(managerApproval)
        .Else()
            .Run(autoApprove)
    .End()

    .Run(notifyUser)

    .Build();
```

The language should read like business logic rather than infrastructure code.

---

# Architecture

The workflow architecture is intentionally divided into three independent layers.

```
Workflow Language
        │
        ▼
Workflow Model
        │
        ▼
Workflow Runtime
```

Each layer has a single responsibility.

---

## 1. Workflow Language

The Workflow Language is the fluent API developers write.

Responsibilities:

- Human-friendly syntax
- Grammar enforcement
- Builder transitions
- No execution logic

Example:

```csharp
.Run(...)
.If(...)
.Then()
.Else()
.Parallel()
.Loop()
.Switch()
```

---

## 2. Workflow Model

The Workflow Model is an immutable description of work.

It contains no execution logic.

It simply describes **what** should happen.

Responsibilities:

- Workflow
- Workflow Steps
- Conditions
- Routing
- Structure

---

## 3. Workflow Runtime

The runtime executes the Workflow Model.

Responsibilities:

- Step execution
- Runtime context
- Execution state
- Diagnostics
- Retry
- Parallel execution
- Observability

The runtime never understands the fluent language directly.

---

# Core Design Principles

## Workflow Steps describe work.

A Workflow Step never performs work.

Execution belongs exclusively to the runtime.

---

## Workflow is recursive.

A Workflow is composed of Workflow Steps.

A Workflow is itself a Workflow Step.

This allows unlimited composition.

```
Workflow

    Workflow Step

    Workflow Step

    Workflow Step
```

---

## Immutable by Design

Workflow definitions never change during execution.

Execution state belongs to the runtime.

---

## Separation of Concerns

Language

↓

Model

↓

Runtime

Each layer only knows its own responsibility.

---

# Universal Language

One of the primary goals of the project is to establish a consistent ubiquitous language.

## Universal Verb

```
Run
```

"Run" represents progress through work.

It was intentionally selected over:

- Execute
- Invoke
- Perform

because it naturally fits every workflow construct.

---

## Universal Noun

```
Workflow Step
```

Everything inside a workflow is ultimately a Workflow Step.

Examples:

- Run Step
- Conditional Step
- Parallel Step
- Repeat Step
- Route Step

---

# Current Workflow Model

```
Workflow

│

├── RunStep

├── ConditionalStep

├── ParallelStep

├── RepeatStep

└── RouteStep
```

The runtime may internally use different execution structures, but the public model is expressed entirely using Workflow Steps.

---

# Builder Philosophy

The Workflow Language is built using two categories of builders.

## Decision Builders

Decision builders enforce grammar.

Example:

```
.If(...)
```

returns

```
IfConditionBuilder
```

which only exposes

```
.Then()
```

---

## Workflow Scope Builders

Workflow scope builders author workflow content.

Every workflow scope exposes the same vocabulary.

```
Run()

If()

Parallel()

Loop()

Switch()

Retry()
```

Nested builders inherit exactly the same vocabulary.

---

# Current Grammar

Current supported grammar:

```csharp
Workflow.Create()

    .Run(...)

    .Parallel()
    .End()

    .If(...)
        .Then()
        .Else()
    .End()

    .Build();
```

Grammar enforcement is performed by builder transitions rather than runtime validation.

---

# Current Project Structure

```
Workflow

├── Builders

├── Steps

├── Conditions

├── Routing
```

Runtime concerns remain outside the Workflow Model.

---

# Runtime Direction

The runtime remains responsible for:

- execution
- retries
- diagnostics
- usage tracking
- cancellation
- parallel scheduling

Workflow Steps never execute themselves.

---

# Current Milestone

Completed

- ✓ Workflow Builder
- ✓ Composite Workflow Builder
- ✓ Parallel Builder
- ✓ Grammar Builder foundation
- ✓ IWorkflowStep
- ✓ Workflow
- ✓ Recursive workflow model
- ✓ Separation of Language / Model / Runtime

---

# Next Milestone

Phase 2

Implement the first concrete Workflow Step.

```
RunStep
```

Followed by:

- ConditionalStep
- ParallelStep
- RepeatStep
- RouteStep

After the model is complete, the runtime will gradually migrate from Pipeline Steps to Workflow Steps.

---

# Guiding Philosophy

> A workflow is the ordered flow of work toward a business goal.

It is composed of Workflow Steps.

Each Workflow Step has a single responsibility and contributes to the progress of the workflow.

Some steps perform work.

Some steps make decisions.

Some steps organize other steps.

Everything in a workflow is ultimately a Workflow Step.
