# MS-004 — Workflow Persistence & Serialization

| Field | Value |
|-------|-------|
| **Milestone** | MS-004 |
| **Title** | Workflow Persistence & Serialization |
| **Status** | Draft |
| **Owner** | PulseStackAI Core Team |
| **Target Version** | v0.7.0 |
| **Sprint** | Sprint 2 |
| **Created** | 2026-07-15 |

---

# Vision

Transform the Workflow Model from an in-memory programming construct into a portable, immutable, versioned artifact that can be authored, stored, shared, validated, and executed across environments while preserving the separation between authoring, persistence, and execution.

Workflow Persistence establishes the foundation for enterprise workflow lifecycle management, interoperability, tooling, governance, and future distributed execution.

---

# Background

Sprint 1 established:

- Workflow Language
- Workflow Builder
- Workflow Runtime
- Step Executor Architecture
- Runtime Orchestration

Workflows currently exist only in memory.

MS-004 introduces the ability to persist workflows as portable documents without introducing persistence concerns into the runtime.

---

# Objectives

This milestone aims to:

- Introduce a canonical Workflow Document model.
- Define a stable persistence contract.
- Support workflow serialization and deserialization.
- Enable schema versioning.
- Establish workflow validation.
- Preserve runtime independence.
- Enable future workflow tooling.

---

# Success Criteria

A workflow should complete the following lifecycle:

Author

↓

Build

↓

Serialize

↓

Store

↓

Load

↓

Validate

↓

Execute

Example:

```csharp
var workflow = Workflow
    .Create("Expense Approval")
    .Run(researchAgent)
    .If(...)
    .Parallel(...)
    .Build();

await serializer.SaveAsync(workflow, "expense.json");

var loaded = await serializer.LoadAsync("expense.json");

await workflowRuntime.ExecuteAsync(loaded);

```

---

# Scope

## In Scope
WorkflowDocument
WorkflowDefinition
WorkflowIdentity
WorkflowStep identity
Serialization contracts
Deserialization contracts
JSON serializer
JSON deserializer
Schema versioning
Workflow validation
Round-trip serialization tests
Documentation
Samples

## Out of Scope
Database providers
Blob storage
Workflow repository
Visual workflow designer
Workflow marketplace
Workflow scheduling
Distributed execution
Runtime state persistence

These capabilities will build upon the persistence architecture established in this milestone.

---

# Architecture
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

---
## Architectural Principles
Canonical Document

WorkflowDocument is the canonical persistence model.

It is independent of:

JSON
YAML
XML
Database
Storage provider

Serialization formats are representations of the WorkflowDocument, not the workflow itself.

# Declarative Persistence

Persist only declarative information.

## Persist:

Structure
Definition
Identity
Parameters
Configuration

## Never persist:

Delegates
Runtime state
IServiceProvider
Agent instances
Execution context

---

## Runtime Independence

Workflow Runtime executes Workflow objects.

Persistence converts between:

Workflow

⇄

WorkflowDocument

The runtime has no dependency on serialization or storage.

---
## Immutability

WorkflowDocument represents a snapshot.

Documents are immutable after creation.

Changes produce a new document version.

## Versioning

Every persisted workflow includes:

Schema
Schema Version
Workflow Version

to support future compatibility and migrations.

---

# Domain Impact

This milestone introduces several foundational domain concepts.

## Workflow Identity

Introduce a stable WorkflowId generated during workflow creation.

Workflow names remain Definition.

## Workflow Definition

Introduce a dedicated `WorkflowDefinition` to describe the intrinsic characteristics of a workflow.

The definition represents the business description of the workflow and remains independent of persistence and repository concerns.

Examples include:

- Name
- Description

Repository-specific information such as authorship, timestamps, storage location, and audit history are intentionally excluded.

---

## Step Identity

Every workflow step receives a WorkflowStepId.

Benefits include:

Validation
Logging
Diagnostics
Future visual designers
Workflow diffing
Graph workflows

---

## Mapping Layer

Introduce a dedicated mapping layer:

Workflow

⇄

WorkflowDocument

This separates the domain model from persistence concerns.

---

# Internal Architectural Barriers

The following architectural limitations must be addressed before persistence implementation.

Workflow lacks a stable identity.
Workflow steps are not uniquely identifiable.
Identity and Definition are currently coupled.
Persistence model does not exist.
Validation cannot uniquely identify workflow steps.
Logging lacks stable workflow correlation identifiers.

Removing these barriers improves long-term maintainability and enables future capabilities without increasing runtime complexity.

---

# Risks
Public Persistence Contract

WorkflowDocument becomes a public contract.

Schema evolution and backward compatibility must be carefully managed.

## Domain Changes

Identity and Definition refactoring may require updates to builders, tests, and runtime components.

## Future Workflow Evolution

The persistence model should support future graph-based workflows without requiring breaking changes.

---

## Dependencies

Requires:

Workflow Language
Workflow Builder
Workflow Runtime
Step Executor Architecture

Introduces:

Workflow Identity
Workflow Definition
WorkflowDocument
Workflow Mapper
Validation Pipeline

---

# Deliverables

## Phase 0 — Remove Architectural Barriers
WorkflowId
WorkflowStepId
WorkflowDefinition
Identity separation
Runtime review
Builder updates

---

## Phase 1 — Persistence Contracts

WorkflowDocument
Serializer interfaces
Store interfaces
Mapper interfaces

---

## Phase 2 — JSON Serialization

JSON serializer
JSON deserializer
Schema Definition

---

## Phase 3 — Validation

Structural validation
Identity validation
Schema validation
Version validation

---

## Phase 4 — Samples & Documentation

Save workflow sample
Load workflow sample
Execute persisted workflow sample
Persistence guide
Architecture documentation

---

# Future Capabilities Enabled

MS-004 establishes the foundation for:

Visual Workflow Designer
Workflow Templates
Workflow Repository
Workflow Marketplace
AI-generated Workflows
Git-based Workflow Management
Workflow Versioning
REST Workflow APIs
Distributed Workflow Execution
Workflow Governance

---

# Definition of Done

The milestone is complete when:

WorkflowDocument is implemented.
Persistence architecture is documented.
Workflow identity is established.
Round-trip serialization succeeds.
Validation is implemented.
Runtime executes persisted workflows.
Samples and documentation are complete.
Existing runtime behavior remains unchanged.

---

## Workflow Mapping Layer Completed

Introduced a canonical WorkflowMapper that translates between the runtime workflow model and the persistence document model. Runtime components are represented as references (for example, AgentReference) rather than serialized object instances, enabling format-agnostic persistence and future storage providers.
---