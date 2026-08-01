# PulseStackAI

> **A Domain-Driven AI Application Platform for .NET**
>
> Build AI applications by describing **business intent**—not AI infrastructure.

PulseStackAI helps developers build AI-powered business applications by separating **what the business needs** from **how AI technologies implement it**.

Instead of building applications around providers, prompts, or orchestration code, PulseStackAI allows you to compose reusable AI capabilities into applications that remain readable, portable, and technology independent.

---

# Every AI project starts the same way...

You have a simple idea.

> "Review this contract."

Or

> "Summarize this meeting."

Or

> "Approve this expense."

Or

> "Research this customer."

At first, it feels like a single prompt.

Then reality arrives.

You need another model.

Then tool calling.

Then memory.

Then retries.

Then logging.

Then observability.

Then streaming.

Then provider abstractions.

Then execution strategies.

Before long...

You're no longer building your AI application.

You're building an AI framework.

**We've all done it.**

---

# Think Like the Business

Businesses don't think in terms of providers.

They think in terms of work.

> Review the contract.

> Validate the policy.

> Research the customer.

> Approve the invoice.

These are business stories.

The business doesn't care whether the work is performed by OpenAI, Azure OpenAI, MCP, Neo4j, or SQL Server.

It cares that the work happens.

PulseStackAI allows developers to express those business stories directly.

Everything else becomes implementation.

---

# The PulseStackAI Philosophy

PulseStackAI is built on one simple belief.

> **AI applications are business systems.**

Business intent should remain independent of the technologies that execute it.

This separation allows applications to evolve without being rewritten every time the AI ecosystem changes.

---

# The Architecture at a Glance

Every layer in PulseStackAI has exactly one responsibility.

```text
Business Story
        │
        ▼
AI Asset Model
        │
        ▼
Application Language
        │
        ▼
Asset Configuration
        │
        ▼
Runtime
        │
        ▼
Execution
```

Each layer answers a different question.

| Layer | Question |
|--------|----------|
| **Domain** | What problem are we solving? |
| **AI Asset Model** | What reusable concepts exist? |
| **Application Language** | How do we express business intent? |
| **Asset Configuration** | Which implementation do we choose? |
| **Runtime** | How is business intent executed? |

Keeping these responsibilities separate makes applications easier to understand, test, maintain, and evolve.

---

# Core Principles

PulseStackAI is guided by a small set of architectural principles.

### Business Before Technology

Business intent should not depend on AI providers or infrastructure.

---

### Everything Reusable is an Asset

Agents, Workflows, Prompts, Tools, Knowledge, Policies, and Packages are reusable building blocks.

Applications are composed from Assets.

---

### Business Intent is Expressed Through Composition

Applications are created by composing reusable Assets rather than writing orchestration code.

---

### Providers Are Implementation Details

Providers bring intelligence.

They do not define the application.

---

### The Runtime Performs the Work

The Runtime executes applications.

It does not define them.

---

### Stable Language, Evolving Technology

Models change.

Providers change.

Databases change.

Business intent changes much more slowly.

PulseStackAI keeps these concerns separate.

---

# Learn the Architecture

The README is only the beginning.

Each architectural concept is explained in detail in its own document.

| Document | Purpose |
|----------|---------|
| **AI Asset Model** | Defines the reusable concepts that make up PulseStackAI. |
| **Application Language** | Explains how business intent is expressed through composition. |
| **Workflow Runtime** | Describes how applications are executed. |
| **Workflow Persistence** | Explains how applications are stored, exchanged, and versioned. |
| **Workflow Packages** | Describes packaging, distribution, and reuse. |
| **Roadmap** | Shows the long-term vision of the platform. |

Together these documents describe the complete architecture of PulseStackAI—from business intent to execution.

---

# A Simple Example

Imagine describing an expense approval process.

```csharp
var application =
    Workflow.Create("Expense Approval")

        .Run(loadExpense)

        .If(
            requiresManagerApproval,
            managerApproval)

        .Parallel(
            fraudCheck,
            policyValidation)

        .Run(finalSubmission)

        .Build();
```

Notice what isn't here.

- No provider-specific code.
- No execution loops.
- No retry plumbing.
- No infrastructure concerns.

Just the business process.

The Runtime takes care of everything else.

---

# Project Status

## Available Today

- ✅ Workflow Runtime
- ✅ Workflow Persistence
- ✅ Workflow Packages
- ✅ AI Asset Model

## Coming Next

- 🚧 Application Language
- 🚧 AI Projects
- 🚧 AI Libraries
- 🚧 Asset Registry
- 🚧 Visual Designer
- 🚧 Marketplace

PulseStackAI is evolving from a workflow runtime into a complete domain-driven AI application platform.

---

# Our Vision

Software has traditionally been written around technology.

We believe AI applications should be written around business intent.

Providers will evolve.

Models will improve.

Infrastructure will change.

Business goals remain.

PulseStackAI exists to keep those worlds separate.

Our goal is simple:

> **Allow developers to spend less time orchestrating AI and more time solving real business problems.**

---

# Welcome to PulseStackAI

If you're looking for another AI SDK, you're in the wrong place.

If you're looking for a better way to build AI-powered business applications, welcome.

Let's build software that speaks the language of the business first—and let the technology follow.