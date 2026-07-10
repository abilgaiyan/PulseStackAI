# AGENTS.md

# The PulseStackAI Constitution

> **The developer should think about business workflows—not AI infrastructure.**

Everything in PulseStackAI exists to support this single idea.

This document defines the architectural principles that guide the evolution of the framework.

Every contributor—human or AI—should understand these principles before contributing code.

---

# Why PulseStackAI Exists

Software development has always evolved by raising the level of abstraction.

Assembly became C.

C became C++.

C++ became C#.

Developers stopped thinking about registers and memory addresses.

They started thinking about business problems.

Artificial Intelligence is the next evolution.

Yet today's AI development has moved backwards.

Developers are expected to think about providers, prompts, retries, tool execution, memory, streaming, orchestration, and dozens of rapidly evolving AI concepts before they can solve a single business problem.

We believe this is the wrong level of abstraction.

AI infrastructure should support business software—not define it.

At PulseStackAI, we believe the future of AI development is workflow-oriented.

Business applications should be expressed as workflows.

Infrastructure should disappear behind the framework.

Providers should become interchangeable.

Execution should become observable.

Business logic should remain visible.

Our mission is simple:

> **The developer should think about business workflows—not AI infrastructure.**

Everything else is the framework's responsibility.

---

# The Architecture Laws

These are the non-negotiable design principles of PulseStackAI.

Every architectural decision should reinforce them.

---

## Law 1

### Workflows express business intent.

Developers describe **what** should happen.

The runtime decides **how** it happens.

---

## Law 2

### Builders compose.

Builders never execute.

Their responsibility ends once the workflow has been defined.

---

## Law 3

### Runtimes execute.

WorkflowRuntime, AgentRuntime, ToolRuntime, and future runtime services own execution.

Execution logic never belongs inside builders.

---

## Law 4

### Providers remain isolated.

Providers communicate with language models.

Providers never contain workflow logic.

Workflow logic never depends on provider implementations.

---

## Law 5

### Composition over inheritance.

Complex behavior should emerge from composing small workflow nodes.

Prefer:

* ConditionalStep
* RetryStep
* ParallelStep
* LoopStep
* SwitchStep
* WorkflowNode

Avoid deep inheritance hierarchies.

---

## Law 6

### Infrastructure belongs inside the framework.

Retries.

Tool execution.

Provider communication.

Diagnostics.

Observability.

Execution strategies.

Memory.

State management.

These are infrastructure concerns.

They should disappear behind the runtime.

---

## Law 7

### Business logic belongs inside workflows.

When reading a workflow, developers should immediately understand the business process.

Infrastructure should never distract from intent.

---

## Law 8

### Public APIs describe business intent.

Developers should write code that resembles business workflows.

Good:

```csharp
Workflow.Create()

    .Run()

    .If()

    .Parallel()

    .Retry()

    .Build();
```

Avoid exposing runtime mechanics in the public API.

---

## Law 9

### Every feature should reduce cognitive load.

Before adding a new feature, ask:

> Does this make AI applications easier to understand?

If not, rethink the design.

PulseStackAI exists to simplify AI development—not expose more infrastructure.

---

## Law 10

### Great frameworks absorb complexity.

Complexity is unavoidable.

Developers should not be required to understand it.

The framework exists to hide complexity while preserving flexibility.

---

# Architectural Mental Model

PulseStackAI is intentionally layered.

```
Developer

↓

Workflow Language

↓

Workflow Definition

↓

Workflow Runtime

↓

Agent Runtime

↓

Providers
```

Every layer has exactly one responsibility.

---

# Design Principles

When introducing new features:

* Prefer composition over configuration.
* Prefer explicit workflows over hidden behavior.
* Prefer strongly typed APIs over string-based configuration.
* Prefer simple mental models over clever abstractions.
* Prefer readability over terseness.
* Prefer deterministic behavior over magic.

If a feature increases complexity without improving expressiveness, it probably does not belong.

---

# Contributor Guidelines

Before implementing a feature, ask yourself:

## Does this belong in the Builder?

Builders compose workflows.

They never execute them.

---

## Does this belong in the Runtime?

Execution.

Retries.

Diagnostics.

Events.

Observability.

Usage tracking.

Execution strategies.

These belong in the runtime.

---

## Does this belong in the Provider?

Only provider-specific communication belongs there.

Nothing else.

---

## Does this belong in the Workflow?

If it represents business intent, it probably belongs in the Workflow Language.

---

# Writing Code

When contributing to PulseStackAI:

Keep methods small.

Prefer immutable objects.

Avoid hidden side effects.

Keep responsibilities focused.

Avoid unnecessary abstractions.

Write unit tests before expanding functionality.

Favor readability over cleverness.

Every public API should feel natural to a .NET developer.

---

# Writing Documentation

Documentation should teach ideas—not APIs.

Every document should answer one question.

README

> Why does PulseStackAI exist?

Architecture

> How does PulseStackAI work?

Workflow Language

> How should developers think?

Examples

> How do I solve business problems?

Roadmap

> Where are we going?

If documentation becomes difficult to understand, simplify it.

> *"If you can't explain it simply, you don't understand it well enough."*

---

# Decision Checklist

Before merging any change, ask:

* Does this reduce infrastructure exposed to developers?
* Does this improve the workflow mental model?
* Does this belong in the correct architectural layer?
* Does it preserve provider independence?
* Does it improve readability?
* Does it reduce cognitive load?

If the answer is "no" to several of these questions, reconsider the design.

---

# The Future

PulseStackAI is not building another AI SDK.

It is building a Workflow Language for AI applications.

Our goal is to make AI development feel like designing business systems rather than assembling infrastructure.

Everything we build should move us closer to that vision.

---

# One Final Principle

Every contribution should leave PulseStackAI simpler than it was before.
