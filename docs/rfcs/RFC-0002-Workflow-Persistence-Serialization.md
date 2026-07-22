# RFC-0002 — Workflow Persistence & Serialization

| Field | Value |
|-------|-------|
| **RFC** | RFC-0002 |
| **Title** | Workflow Persistence & Serialization |
| **Status** | Complete |
| **Authors** | PulseStackAI Core Team |
| **Related Milestone** | MS-004 |
| **Target Version** | v0.7.0 |
| **Created** | 2026-07-15 |

---

# Summary

This RFC proposes a persistence architecture for PulseStackAI workflows.

The proposal introduces a canonical `WorkflowDocument` model that represents workflows as immutable, portable, versioned documents while preserving the separation between workflow authoring, persistence, and runtime execution.

The objective is to transform workflows from transient in-memory objects into durable artifacts suitable for enterprise lifecycle management.

---

# Architectural Context

PulseStackAI is evolving through a series of bounded contexts:

Workflow Language
        ↓
Workflow Model
        ↓
Workflow Persistence
        ↓
Workflow Runtime
        ↓
Pipeline Runtime

MS-003 established the execution architecture.

RFC-0002 introduces the persistence architecture.

Together they form the foundation for future capabilities including visual designers, workflow repositories, AI-generated workflows, Model Context Protocol (MCP), and Agent-to-Agent (A2A) interoperability.

---

# Motivation

Sprint 1 introduced the Workflow Language and Workflow Runtime.

Developers can now author and execute workflows, but workflows only exist during application execution.

This limits future capabilities such as:

- Workflow import/export
- Versioning
- Visual designers
- AI-generated workflows
- Workflow repositories
- Git integration
- REST APIs
- Distributed execution

Workflow Persistence establishes the foundation for these capabilities without increasing runtime complexity.

---

# Problem Statement

The current architecture tightly couples workflow definition to application memory.

There is no stable representation that can be:

- stored
- transferred
- versioned
- validated
- reviewed
- exchanged between systems

Without a canonical persistence model, future enterprise capabilities would require runtime-specific implementations.

---

# Goals

This RFC proposes an architecture that:

- Defines a canonical persistence contract.
- Separates persistence from execution.
- Preserves runtime independence.
- Enables schema evolution.
- Supports multiple serialization formats.
- Introduces stable workflow identities.
- Enables future tooling.

---

# Non-Goals

This RFC does not propose:

- Database persistence
- Cloud storage
- Workflow repositories
- Distributed execution
- Runtime state persistence
- Workflow scheduling

Those capabilities build upon this architecture.

---

# Persistence Transformation Pipeline

The Persistence Pipeline is intentionally representation-oriented rather than technology-oriented. It defines how PulseStackAI transforms domain aggregates into portable artifacts without coupling the domain model to any serialization format, storage provider, or execution environment. This ensures that the framework remains portable, extensible, and capable of supporting future technologies—including distributed repositories, A2A exchange, MCP resources, and graph-based persistence—without changing the core domain model.

## Representation-Oriented Architecture

Represents the executable domain model.

Optimized for:

Business behavior
Runtime execution
Domain invariants
Business rules

The aggregate is never persisted directly.

## Aggregate Transformation Pipeline

Workflow Aggregate
        │
        │
        ▼
+----------------------+
| IWorkflowMapper      |
+----------------------+
        │
        ▼
WorkflowDocument
        │
        │
        ▼
+----------------------+
| IWorkflowValidator   |
+----------------------+
        │
        ▼
Validated Document
        │
        ▼
+----------------------+
| IWorkflowSerializer  |
+----------------------+
        │
        ▼
JSON / YAML / XML
        │
        ▼
+----------------------+
| IWorkflowStore       |
+----------------------+
        │
        ▼
Persistence Provider

## Responsibilities

Workflow Aggregate

Workflow Mapper

Workflow Document

Serializer

Store

## Aggregate References

A persistence document contains intrinsic business information and references to external aggregates.

Example:

WorkflowDocument
│
├── Identity
├── Definition
├── Steps
│
└── RunStepDocument
        │
        └── AgentReference

The workflow does not embed an executable agent.

Instead, it references another aggregate.

Future aggregate references include:

AgentReference
PromptReference
ToolReference
MemoryReference
WorkflowReference

This keeps each aggregate independently versioned, reusable, and portable.

> **Architectural Principle**
>
> Representation changes.
> Meaning does not.

## Workflow Document

Represents the canonical portable description of a workflow.

Optimized for:

Portability
Versioning
Validation
Interchange
Long-term compatibility

The document contains:

Identity
Definition
Workflow structure
References to external aggregates

The document never contains:

Executable runtime objects
Delegates
Services
Dependency Injection
Runtime state

---
## Workflow Mapper

Transforms the Workflow Aggregate into a WorkflowDocument and reconstructs the aggregate from a WorkflowDocument.

The mapper changes only the representation.

It never changes the meaning of the workflow.

Responsibilities:

Aggregate → Document
Document → Aggregate
Resolve aggregate references
Preserve business semantics

The mapper has no knowledge of:

JSON
YAML
Files
Databases
Cloud storage

---

### Serializer

Transforms a WorkflowDocument into a transport format.

Examples:

JSON
YAML
XML

The serializer has no knowledge of runtime behavior.

Store

Persists the serialized representation.

Examples:

Local files
Azure Blob Storage
SQL Database
MongoDB
Git Repository

The store has no knowledge of workflow semantics.

---

## Conceptual Model

A Workflow Aggregate is comparable to a fully assembled building.

A WorkflowDocument is comparable to its architectural blueprint.

The mapper translates between the assembled structure and its blueprint without changing the meaning.

The serializer writes the blueprint into a transport format.

The storage layer decides where the blueprint is persisted.

Loading performs the reverse transformation.

The blueprint never performs the work.

The building does.

# Architectural Decisions

## Decision 1 — Canonical WorkflowDocument

Introduce `WorkflowDocument` as the canonical persistence model.

The domain model (`Workflow`) remains focused on authoring and execution.

Persistence operates exclusively on `WorkflowDocument`.

This avoids introducing serialization concerns into the domain model.

---

## Decision 2 — Runtime Independence

The Workflow Runtime executes only `Workflow`.

It never executes `WorkflowDocument`.

Conversion occurs through a dedicated mapping layer.

```
Workflow
│
├── Identity
├── Definition
└── Steps

        │

        ▼

Workflow Mapper

        ▼

WorkflowDocument
```

This preserves the separation of concerns established in MS-003.

---

## Decision 3 — Declarative Persistence

Persist only declarative information.

Examples:

- Identity
- Definition
- Workflow structure
- Parameters
- Configuration

Do not persist:

- Delegates
- Runtime services
- Execution state
- Agent instances
- IServiceProvider

This ensures portability and security.

---

## Decision 4 — Immutable Documents

WorkflowDocument represents a snapshot.

Documents are immutable after creation.

Changes produce a new document version rather than modifying an existing document.

---

## Decision 5 — Identity as a Domain Concept

Workflow identity is created during workflow construction, not during persistence.

Every workflow receives:

- WorkflowId
- WorkflowVersion

Every workflow step receives:

- WorkflowStepId

Names remain descriptive Definition.

Identity supports:

- Validation
- Logging
- Diagnostics
- Versioning
- Visual designers
- Future graph workflows

---

## Decision 6 — Serialization and Storage Separation

Serialization converts documents into representations.

Storage persists those representations.

```
WorkflowDocument
        │
        ▼
IWorkflowSerializer
        │
        ▼
Stream
        │
        ▼
Transport Format
(JSON / YAML / XML / BSON / ...)

────────┼────────────────────

        ▼

Workflow Store

↓

File

Database

Blob

Git
```

Storage providers are independent of serialization formats.

---

## Decision 7 — Persistence Mirrors the Domain

WorkflowDocument mirrors the Workflow Aggregate as closely as possible.

The persistence model preserves the aggregate's identity, definition, and structure while excluding runtime behavior.

This minimizes mapping complexity and simplifies schema evolution.

---

# Architecture

```
Workflow Language
        │
        ▼
Workflow Model
        │
        ▼
Workflow Mapper
        │
        ▼
WorkflowDocument
        │
        ├──────────────┐
        ▼              ▼
Serializer        Validator
        │
        ▼
JSON / YAML / XML
        │
        ▼
Workflow Store
        │
        ▼
Load
        │
        ▼
Workflow Runtime
```
---

# Alternatives Considered

## Alternative 1 — Serialize Workflow Directly

```
Workflow

↓

JSON
```

### Rejected

Reasons:

- Couples persistence to the domain model.
- Introduces serialization concerns into business objects.
- Makes schema evolution more difficult.
- Reduces portability.

---

## Alternative 2 — Runtime Executes WorkflowDocument

```
WorkflowDocument

↓

Runtime
```
---

### Rejected

Reasons:

- Couples execution to persistence.
- Prevents independent evolution of runtime and document model.
- Violates separation of concerns.

---

## Alternative 3 — Persistence Generates Workflow Identity

### Rejected

Reasons:

Identity is a domain concept.

Persistence preserves identity.

It does not create it.

---

# Consequences

## Positive

- Clean architectural boundaries
- Stable persistence contract
- Runtime independence
- Easier testing
- Better extensibility
- Multiple serialization formats
- Future tooling support

## Trade-offs

- Additional mapping layer
- Additional document model
- Increased upfront design effort

These trade-offs are intentional to improve long-term maintainability.

---

# Future Evolution

This architecture enables:

- Visual Workflow Designer
- Workflow Marketplace
- AI Workflow Generation
- Git-based workflow management
- REST APIs
- Distributed execution
- External workflow exchange
- Workflow governance

Future milestones such as MCP integration and Agent-to-Agent (A2A) interoperability can build on this foundation without requiring changes to the Workflow Runtime.

---

# Open Questions

The following items remain implementation decisions:

- JSON schema structure
- Version migration strategy
- Validation pipeline composition
- Serializer registration
- Workflow storage abstractions
- Compression and encryption support

These will be addressed during implementation.

---

# Decision

Adopt the proposed persistence architecture based on:

- Canonical `WorkflowDocument`
- Immutable document model
- Strongly typed identities
- Dedicated mapping layer
- Separation of serialization and storage
- Runtime independence