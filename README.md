# PulseStackAI

> **A Domain-Driven AI Application Engineering Platform for .NET**
>
> Build intelligent business applications by describing **business intent**—not AI infrastructure.

PulseStackAI introduces a provider-independent **AI Application Language** built on reusable **AI Assets** and realized through a composable runtime.

Instead of programming prompts, providers, and orchestration, developers compose business capabilities that remain readable, reusable, and technology independent.

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

PulseStackAI

──────────────────────────

Language

↓

Assets

↓

Runtime

| Pillar | Responsibility |
| --- | --- |
| **AI Application Language** | Expresses business intent. |
| **AI Asset Model** | Defines reusable business capabilities. |
| **Runtime** | Realizes and executes those capabilities. |

---

# The Architecture at a Glance

Every layer in PulseStackAI has exactly one responsibility.

```text
Business Intent
        │
        ▼
Application Language
        │
        ▼
AI Asset Model
        │
        ▼
Runtime Realization
        │
        ▼
Execution Runtime
        │
        ▼
Provider Infrastructure
```

Each layer answers a different question.

| Layer | Question |
| --- | --- |
| **Domain** | What problem are we solving? |
| **AI Asset Model** | What reusable concepts exist? |
| **Application Language** | How do we express business intent? |
| **Asset Configuration** | Which implementation do we choose? |
| **Runtime** | How is business intent executed? |

Keeping these responsibilities separate makes applications easier to understand, test, maintain, and evolve.

---

# Architectural Principles

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

```text
Architecture

Vision

↓

Application Language

↓

AI Asset Model

↓

Runtime

↓

Persistence

↓

Packages

↓

Roadmap
```

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

> The current builder API predates the declarative Workflow Asset model. Migrating this authoring surface to emit declarative definitions is intentionally deferred beyond MS-008; the business-first grammar remains the design target.

---

# Project Status

## Completed

- ✅ MS-001 — Core Foundation
- ✅ MS-002 — Agent Runtime
- ✅ MS-003 — Workflow Runtime
- ✅ MS-004 — Workflow Persistence
- ✅ MS-005 — Workflow Packages
- ✅ MS-006 — AI Asset Model & Application Language
- ✅ MS-007 — Runtime Realization Architecture
- ✅ MS-008 — Runtime Realization Implementation
  - ✅ Phase 1 — Runtime Realization Foundation
  - ✅ Phase 2 — Agent Asset Realization
  - ✅ Phase 3 — Workflow Realization

MS-008 closes the realization path:

```text
WorkflowAsset
    ↓
WorkflowStepDefinition
    ↓
Agent references
    ↓
Agent realization
    ↓
Executable Workflow graph
    ↓
IWorkflowRuntime
```

Declarative Workflow grammar now realizes `Run`, `Parallel`, `If`, `Retry`, `ForEach`, and `Switch` into the existing execution runtime without exposing provider or infrastructure concerns in the Application Language.

## Next Milestone

- ⬜ **MS-009 — AI Asset Platform Implementation**

Expected areas:

- AI Projects
- AI Libraries
- Asset Catalog / Registry
- dependency and reference management
- validation, versioning, discovery, and loading

Later platform capabilities include Planner, Human Approval, Scheduling, Distributed Runtime, Visual Designer, and Marketplace.

PulseStackAI is evolving from a workflow runtime into a complete domain-driven AI Application Engineering Platform.

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

# The PulseStackAI Way

Think in Business Intent.

Compose AI Assets.

Realize Through Runtime.

Hide Technology.

Keep Providers Replaceable.

Build for Change.

---

# Welcome to PulseStackAI

PulseStackAI isn't another AI SDK.

It's a language for building AI-powered business applications.

We believe developers should think in business capabilities—not providers, prompts, or orchestration.

Describe the intent. Compose the capabilities. Let the runtime realize the application.

If you're looking for a better way to build AI-powered business applications, welcome.

Let's build software that speaks the language of the business first—and let the technology follow.
