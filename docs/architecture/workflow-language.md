# Workflow Language

> **Think in business workflows—not AI infrastructure.**

---

## Before writing code, think differently.

Traditional software development teaches us to think in terms of classes, methods, APIs, and data structures.

Modern AI development often teaches something even more complicated.

Developers are expected to think about prompts, providers, memory, streaming, tool calls, retries, orchestration, and execution loops.

These are implementation details.

They are not the problem you're trying to solve.

PulseStackAI asks you to think differently.

Before writing a single line of code, ask yourself three simple questions.

---

## 1. What is the intent?

Every workflow begins with an objective.

Not a prompt.

Not a provider.

Not a model.

An objective.

Examples:

* Review this contract.
* Approve this expense.
* Summarize these documents.
* Classify this support ticket.
* Analyze this financial report.

The intent defines **what the workflow is trying to accomplish**.

Everything else exists to support that goal.

---

## 2. What happens next?

Business processes are simply a sequence of decisions.

After one step completes, something else happens.

Sometimes another agent runs.

Sometimes a manager approves.

Sometimes work happens in parallel.

Sometimes the workflow retries.

Sometimes it loops.

Sometimes it ends.

The Workflow Language exists to describe these transitions naturally.

Think about the business process—not the execution engine.

---

## 3. What is the current state?

Every decision depends on context.

What information do we have?

What has already happened?

What is the latest result?

PulseStackAI represents this through the Pipeline Context.

The context carries the current state of the workflow from one node to the next.

Nodes read from it.

Nodes contribute to it.

The runtime manages it.

The developer simply uses it.

---

## Everything else is infrastructure.

Notice what we didn't ask.

Not:

> Which provider should I use?

Not:

> How do I retry this HTTP request?

Not:

> Where do I store the conversation history?

Not:

> How do I execute nodes in parallel?

Those are runtime responsibilities.

The workflow should describe business intent.

The runtime should implement it.

---

## The PulseStackAI Mental Model

Every workflow can be understood by answering three questions.

```text
Intent

↓

What happens next?

↓

Current State
```

That's it.

Everything else belongs inside the framework.

---

## The Workflow Language

Once you start thinking in workflows, the code becomes almost self-explanatory.

```csharp
var workflow =
    Workflow.Create("Expense Approval")

        .Run(loadExpense)

        .If(
            requiresManagerApproval,
            managerApproval)

        .Parallel(
            fraudCheck,
            policyValidation)

        .Retry(finalSubmission)

        .Build();
```

You're no longer programming an AI system.

You're describing a business process.

That's the purpose of the Workflow Language.
