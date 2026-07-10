# Workflow Execution

> **Every workflow is a conversation.**

---

## Before we talk about code...

Imagine explaining a business process to a colleague.

You don't start by talking about classes, APIs, or AI models.

You simply describe what needs to happen.

> Read the document.

> Review the contract.

> Approve the expense.

> Notify the customer.

One step naturally leads to the next until the work is complete.

That's a workflow.

PulseStackAI simply gives developers a way to describe that workflow in code.

---

# Every workflow is a conversation

Every workflow begins with an intent.

Each step produces a result.

That result becomes the starting point for the next decision.

The workflow continues until the business objective has been achieved.

PulseStackAI simply coordinates that conversation.

The developer describes it.

The runtime executes it.

---

# The Mental Model

To understand how PulseStackAI works, you do not need to understand complex algorithms or runtime internals.

You only need to understand five simple ideas.

```text
Intent

↓

Workflow

↓

Node

↓

Current State

↓

Next Step
```

Everything in PulseStackAI is built around these ideas.

---

# 1. Intent

Every workflow begins with a purpose.

Not a prompt.

Not a provider.

Not a model.

A purpose.

Examples include:

* Review this contract.
* Approve this expense.
* Summarize these documents.
* Analyze this report.
* Classify this support request.

The intent answers one simple question:

> **What are we trying to accomplish?**

Everything else exists to support that goal.

---

# 2. Workflow

A workflow is the complete map of your business process.

It connects individual pieces of work together until the objective has been achieved.

Think of it as the blueprint of your application.

A workflow does not perform the work itself.

It simply describes what should happen.

---

# 3. Node

A step is a single piece of work.

Every step has one responsibility.

A step might:

* ask an AI agent to summarize a document,
* execute a tool,
* validate a condition,
* perform work in parallel,
* repeat a task,
* choose between multiple paths.

Nodes do not need to understand the entire workflow.

They simply perform their own job and return the result.

Small, focused nodes are easier to understand, easier to test, and easier to reuse.

---

# 4. Current State

Imagine carrying a notebook while completing a task.

Every time something important happens, you write it down.

The next person who continues the work reads the notebook before making the next decision.

That notebook represents the **current state**.

As a workflow executes, PulseStackAI continuously keeps track of:

* the latest results,
* important decisions,
* shared information,
* execution progress.

Every step can use this information when performing its work.

The runtime manages this state automatically.

The developer simply focuses on the business logic.

---

# 5. Next Step

Business processes are simply a sequence of decisions.

After one step finishes, something else happens.

Sometimes another step executes.

Sometimes work branches into multiple paths.

Sometimes work happens in parallel.

Sometimes the workflow repeats.

Sometimes it finishes.

PulseStackAI automatically moves the workflow from one step to the next based on the current state of the business process.

---

# A Workflow is a Team

A workflow is not one intelligent AI.

It is a team working together.

One step gathers information.

Another analyzes it.

Another validates the result.

Another stores it.

PulseStackAI coordinates the team.

Each step focuses on one responsibility.

Together, they accomplish the business objective.

---

# The Separation of Responsibilities

One of the most important ideas in PulseStackAI is the separation between business logic and infrastructure.

```text
The Workflow describes.

The Runtime orchestrates.

The AI thinks.
```

Each layer has a single responsibility.

The workflow expresses the business process.

The runtime coordinates execution.

The AI provides intelligence.

Keeping these responsibilities separate makes applications easier to understand, easier to test, and easier to evolve.

---

# What We Don't Think About

Notice what we never asked.

Not:

> Which provider should I use?

Not:

> How do I retry this request?

Not:

> How do I manage conversation history?

Not:

> How do I execute work in parallel?

Not:

> How do I orchestrate execution?

Those are infrastructure concerns.

PulseStackAI manages them for you.

Instead, developers stay focused on the business process.

---

# The PulseStackAI Way

When designing a workflow, always begin with three simple questions.

> **What is the intent?**

What business problem are we solving?

---

> **What happens next?**

How does the business process naturally flow?

---

> **What is the current state?**

What information is available to make the next decision?

Everything else is the framework's responsibility.

---

# Looking Ahead

Now that we've learned how workflows execute, we can learn the language used to describe them.

The next chapters introduce each workflow primitive one at a time.

* Run
* If
* Parallel
* Retry
* ForEach
* Switch
* Nested Workflows

Each one represents a simple business concept.

Combined together, they form the Workflow Language of PulseStackAI.

---

> **The developer should think about business workflows—not AI infrastructure.**
