# The Grammar of the Workflow Language

Software developers naturally think in terms of business processes.

They ask questions like:

- What should happen first?
- What happens next?
- What should happen only if a condition is true?
- Which activities can happen together?

Traditional programming languages express these ideas through syntax.

PulseStackAI does the same.

Instead of exposing AI infrastructure, the Workflow Language allows developers to describe business intent using a small, consistent vocabulary.

Everything in this document exists to answer one simple question:

> **How should a workflow read?**

---

# Design Philosophy

The Workflow Language is built around a simple principle.

> **Business workflows should be easy to read.**

Developers should describe *what* the business process does.

The framework is responsible for *how* it executes.

To achieve this, PulseStackAI maintains a strict separation between three independent layers.

## Workflow Language (DSL)

The language developers write.

This layer contains only business-oriented concepts.

It intentionally avoids implementation details.

## Workflow Model

The internal representation of the workflow.

The model transforms the language into a structured object graph that the runtime can understand.

## Workflow Runtime

The execution engine.

The runtime coordinates workflow execution, evaluates conditions, executes agents and tools, manages state, and handles diagnostics.

---

Implementation concepts such as **steps**, **graphs**, **execution trees**, or **ASTs** belong to the model and runtime.

They do **not** belong to the Workflow Language.

The language should describe business intent—not technical implementation.

---

# The Two Builder Roles

The Workflow Language is intentionally built from only two kinds of builders.

Keeping the language limited to these two roles makes it easy to learn, easy to extend, and easy to reason about.

## 1. Grammar Builders

Grammar builders guide the developer through a valid workflow sentence.

They do not author workflow steps.

Instead, they control which language construct is allowed next.

A grammar builder exists only to enforce the rules of the language.

Example:

```
If(...)
    ↓
Then()
```

After `If(...)`, the only valid next keyword is `Then()`.

The compiler guides the developer toward a grammatically correct workflow.

---

## 2. Workflow Scope Builders

Workflow scope builders represent places where business work is written.

Whether the developer is writing:

- the root workflow,
- a parallel block,
- a conditional branch,
- or a loop,

the vocabulary remains exactly the same.

Every workflow scope exposes the same core language.

```csharp
.Run(...)

.If(...)

.Parallel()

.ForEach()

.Switch()

.Retry()
```

The only difference between workflow scopes is how they package their work when `End()` is called.

This consistency allows developers to learn the language once and use it everywhere.

---

# The Universal Verb

Every executable piece of business work is expressed using one simple verb.

```csharp
.Run(...)
```

`Run` intentionally describes **intent**, not implementation.

The work may eventually be performed by:

- an AI agent
- a tool
- another workflow
- a future execution component

The Workflow Language never needs to know.

It simply says:

> **Run this work.**

The runtime decides how that work is executed.

---

# Core Grammar

Every language construct follows a consistent pattern.

A keyword introduces a new workflow scope.

That scope is completed by calling `End()`.

```
Keyword

↓

Workflow Scope

↓

End()

↓

Previous Workflow Scope
```

This makes every language construct predictable and easy to understand.

---

# Conditional Grammar

Conditional execution introduces the first true sentence in the Workflow Language.

```
Workflow Scope

↓

If(condition)

↓

Then()

↓

Workflow Scope

↓

Else()   (optional)

↓

Workflow Scope

↓

End()

↓

Previous Workflow Scope
```

---

## Grammar Rules

### Then is mandatory

Every `If(...)` must immediately transition to `Then()`.

The compiler prevents any other operation until the condition has been completed.

---

### Else is optional

Not every business rule requires an alternative path.

If no alternative exists, the workflow may simply continue after `End()`.

---

### End closes the conditional

`End()` completes the conditional block and restores the previous workflow scope.

The developer simply continues writing the workflow.

---

### Workflow scopes support nesting

Every workflow scope shares the same language.

This means any workflow construct can be nested naturally.

For example:

- Parallel inside Then
- If inside Parallel
- Loop inside Else
- Switch inside Loop

There are no special rules.

The grammar remains consistent regardless of depth.

---

# Example

```csharp
var workflow =
    Workflow.Create("Expense Approval")

        .Run(loadExpense)

        .If(requiresManagerApproval)

            .Then()

                .Parallel()

                    .Run(notifyFinance)

                    .Run(managerApproval)

                .End()

                .If(isHighValueExpense)

                    .Then()

                        .Run(vpApproval)

                .End()

            .Else()

                .Run(autoApprove)

        .End()

        .Run(logCompletion)

    .Build();
```

Notice how the workflow reads like a business conversation.

There are no execution loops.

No infrastructure code.

No provider-specific concepts.

Only business intent.

---

# A Guiding Principle

Every new language feature should answer one question:

> **Does this make the workflow easier to read?**

If the answer is yes, it belongs in the Workflow Language.

If it exposes infrastructure, implementation details, or runtime concerns, it belongs somewhere else.

The language exists for one purpose:

> **To help developers think about business workflows—not AI infrastructure.**