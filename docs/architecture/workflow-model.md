# The Workflow Model

The Workflow Model is the internal representation of a workflow.

It does not execute workflows.

It does not make decisions.

It simply describes a workflow in a structured form that the runtime can understand.

Think of it as a blueprint.

The Workflow Language is written for people.

The Workflow Model is built for the framework.

---

# Design Philosophy

Every workflow passes through three independent layers.

```
Workflow Language

↓

Workflow Model

↓

Workflow Runtime
```

Each layer has a single responsibility.

| Layer | Responsibility |
|--------|----------------|
| Workflow Language | Express business intent |
| Workflow Model | Represent the workflow structure |
| Workflow Runtime | Execute the workflow |

This separation keeps the language expressive, the model simple, and the runtime focused on execution.

---

# The Structure of a Workflow

When developers write workflows, they naturally think from top to bottom.

The model represents the same workflow as a hierarchy of workflow steps.

Think of it as boxes inside boxes.

```
Workflow

│

├── Action

├── Conditional

│      ├── Workflow (Then)

│      └── Workflow (Else)

└── Action
```

Every workflow is simply a collection of steps.

Some steps perform work.

Some steps decide where the workflow should go next.

---

# The Building Blocks

The Workflow Model is intentionally small.

It is built from a few simple building blocks.

## Workflow

A workflow is a container.

It holds an ordered collection of workflow steps.

Every workflow has a clear beginning and a clear end.

---

## Action

An action represents one step in the workflow.

For example:

- Load a document
- Execute an AI agent
- Run a tool
- Call another workflow

An action represents work that should happen.

---

## Conditional

A conditional represents a business decision.

Unlike the Workflow Language, the model does not contain words such as **If**, **Then**, or **Else**.

Instead, a conditional simply stores:

- A name
- A condition
- A workflow for the true branch
- An optional workflow for the false branch

The model represents the business decision.

The language describes how developers write that decision.

---

# From Language to Model

Builders act as the bridge between the Workflow Language and the Workflow Model.

Their responsibility is simple.

> **Builders compile the Workflow Language into the Workflow Model.**

For example:

```
.If(condition)

    .Then()

        .Run(...)

    .Else()

        .Run(...)

.End()
```

becomes

```
Conditional

├── Condition

├── Then Workflow

└── Else Workflow
```

Notice that the language contains **Then** and **Else**.

The model does not.

The model only represents the completed business decision.

---

# Design Principles

## Immutable by Design

Once the Workflow Model has been created, it should not change.

The runtime executes the model exactly as it was authored.

Keeping the model immutable makes workflows predictable, thread-safe, and easier to reason about.

---

## No Execution Logic

The Workflow Model never executes anything.

It contains no orchestration logic, retry policies, provider selection, or runtime behavior.

Its only responsibility is to describe the workflow.

Execution belongs entirely to the Workflow Runtime.

---

## The Runtime Does Not Understand the Language

The Workflow Runtime should never need to understand the Workflow Language.

Whether a workflow is authored using:

- the C# Workflow Language
- a JSON document
- YAML
- a visual designer
- another future language

they should all produce exactly the same Workflow Model.

The runtime always executes the model.

It never executes the language.

---

# A Guiding Principle

The Workflow Language exists for developers.

The Workflow Model exists for the framework.

The Workflow Runtime exists for execution.

Keeping these responsibilities separate allows each layer to evolve independently while preserving a simple mental model.

The language expresses business intent.

The model represents that intent.

The runtime brings that intent to life.
