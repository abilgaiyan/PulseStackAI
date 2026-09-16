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

```text
PulseStackAI

Language
   ↓
Assets
   ↓
Persistence
   ↓
Realization
   ↓
Invocation
   ↓
Runtime
```

| Pillar | Responsibility |
| --- | --- |
| **AI Application Language** | Expresses business intent. |
| **AI Asset Model** | Defines reusable business capabilities. |
| **Asset Platform** | Persists, resolves, and loads declarative applications. |
| **Realization** | Transforms accepted declarative graphs into executable runtime objects. |
| **Invocation & Runtime** | Invokes realized applications through the existing execution runtime. |

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
Persistence / Aggregate Loading
        │
        ▼
Application Realization
        │
        ▼
Portable Invocation
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
| **Asset Platform** | How are definitions stored, resolved, and loaded? |
| **Realization** | How does a declarative application become executable? |
| **Invocation** | How is an already-realized application invoked portably? |
| **Runtime** | How is the realized workflow executed? |

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

Persistence

↓

Aggregate Loading

↓

Application Realization

↓

Portable Invocation

↓

Runtime

↓

Roadmap
```

Together these documents describe the architecture of PulseStackAI from business intent through portable execution.

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

> The current builder API predates the declarative Workflow Asset model. Migrating this authoring surface to emit declarative definitions remains future developer-experience work; the business-first grammar remains the design target.

---

# Project Status

## Completed Foundation

- ✅ **MS-001 — Core Foundation**
- ✅ **MS-002 — Agent Runtime**
- ✅ **MS-003 — Workflow Runtime**
- ✅ **MS-004 — Workflow Persistence**
- ✅ **MS-005 — Workflow Packages**
- ✅ **MS-006 — AI Asset Model & Application Language**
- ✅ **MS-007 — Runtime Realization Architecture**
- ✅ **MS-008 — Runtime Realization Implementation**
- ✅ **MS-009 — AI Asset Platform Implementation through Aggregate Graph Loading**
- ✅ **MS-010 — Application Realization & Invocation**

The current framework foundation now supports the following architectural path:

```text
AI Asset Definitions
        ↓
Canonical Serialization
        ↓
Persistent Storage
        ↓
Catalog / Exact Resolution
        ↓
Aggregate Graph Loading
        ↓
AIAssetGraph
        ↓
Application Realization
        ↓
RealizedApplication
        ↓
Portable Invocation
        ↓
IWorkflowRuntime
        ↓
ApplicationInvocationResult
```

### ✅ MS-009 — AI Asset Platform

MS-009 established the schema-v1 AI Asset persistence platform needed for portable definition storage and declarative loading.

Completed capabilities include:

- persistence contracts and mappings for supported AI Assets;
- canonical serialization;
- serialized storage and loading;
- persistent catalog publication and exact resolution;
- aggregate declarative graph loading for Project, Library, and Package roots.

Its aggregate-loading boundary is:

```text
persistent exact resolution
        ↓
multi-definition declarative graph loading
        ↓
AIAssetGraph
```

Storage and catalog providers remain graph-unaware. Required declarative relationships form the materialized closure while optional requirements are preserved without initiating expansion.

### ✅ MS-010 — Application Realization & Invocation

MS-010 consumes the accepted `AIAssetGraph` boundary and connects it to the existing realization and execution infrastructure.

```text
AIAssetGraph
    ↓
Project application realization
    ↓
RealizedApplication
    ↓
portable application invocation
    ↓
IWorkflowRuntime
    ↓
ApplicationInvocationResult
```

MS-010.1–MS-010.3 established and implemented Project-root application realization over the existing Agent and Workflow realization authorities.

MS-010.4 established portable invocation over an already-realized application, including:

- invocation request and result contracts;
- fresh invocation-context projection;
- Project and entry-Workflow provenance;
- failure and cancellation preservation;
- sequential reuse;
- exact-object exclusivity for overlapping invocation;
- provider-owned invocation coordination;
- structured release and recovery;
- provider composition and whole-boundary conformance.

MS-010.4 intentionally begins at `RealizedApplication`. Persisted Project → graph load → realize → invoke coordination is a separate future application-operation boundary rather than unfinished invocation work.

---

# What Comes Next

The framework has reached the point where the next phase should increasingly be driven by real application usage.

The leading usability boundary is a portable application operation that coordinates the already-existing capabilities:

```text
persisted Project identity
        ↓
aggregate graph loading
        ↓
application realization
        ↓
portable invocation
        ↓
application result
```

After that boundary is established, the goal is to exercise the framework through a canonical end-to-end application using persisted declarative assets and let real application requirements drive subsequent capabilities.

Focused future capability tracks include:

- Knowledge retrieval and RAG
- Policy evaluation and governance enforcement
- persistent and shared Memory
- Human Approval
- Scheduling
- provider integrations
- observability and diagnostics expansion

Longer-term platform capabilities include Planner, Distributed Runtime, Asset Registry / Distribution, Visual Designer, and Marketplace.

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

Persist and Load Declaratively.

Realize Through Runtime.

Invoke Portably.

Hide Technology.

Keep Providers Replaceable.

Build for Change.

---

# Welcome to PulseStackAI

PulseStackAI isn't another AI SDK.

It's a language and runtime architecture for building AI-powered business applications around reusable, provider-independent capabilities.

Developers should think in business capabilities—not providers, prompts, or orchestration plumbing.

> **Describe the intent. Compose the capabilities. Let the runtime realize and invoke the application.**

If you're looking for a better way to build AI-powered business applications, welcome.
