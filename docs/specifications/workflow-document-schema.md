# Workflow Document Schema Specification

**Status:** Draft v1.0
**Version:** 1.0
**Applies To:** PulseStackAI Workflow Persistence
**Last Updated:** July 2026

---

# 1. Introduction

The **Workflow Document Schema** defines the canonical persistence format used by PulseStackAI.

A `WorkflowDocument` is the durable, portable representation of a workflow. It is the primary artifact exchanged between tools, persisted by storage providers, version controlled in repositories, and reconstructed into runtime objects during execution.

Unlike the in-memory `Workflow` object, the document is designed for long-term storage and interoperability.

Every persisted workflow, regardless of serialization format or storage provider, **must conform to this specification**.

---

# 2. Goals

The schema has the following design goals.

* Portable across machines and environments
* Independent of runtime implementations
* Human-readable
* Versioned
* Deterministic
* Extensible
* Storage-provider agnostic
* Suitable for source control
* Compatible with future migrations

---

# 3. Design Principles

The schema is based on several architectural principles.

## Runtime Independence

Persistence documents never contain runtime objects.

The following runtime concepts are intentionally excluded:

* Agent instances
* Runtime state
* Execution context
* Dependency injection
* Service providers

Only immutable data required to reconstruct a workflow is persisted.

---

## Stable Identity

Every workflow has a stable identity that remains unchanged across persistence operations.

Workflow identity is independent of:

* Storage provider
* Serialization format
* Runtime execution

---

## Layer Separation

The persistence pipeline is intentionally divided into independent stages.

```text
Workflow
      │
      ▼
WorkflowMapper
      │
      ▼
WorkflowDocument
      │
      ▼
WorkflowValidator
      │
      ▼
WorkflowSerializer
      │
      ▼
IWorkflowStore
```

Each stage has a single responsibility.

---

# 4. Workflow Document Structure

The canonical document structure is shown below.

```text
WorkflowDocument
│
├── Schema
├── SchemaVersion
├── Identity
├── Id
├── Definition
└── Steps
```

Each field has a well-defined purpose.

---

## 4.1 Schema

Identifies the document type.

Example:

```json
{
  "schema": "pulsestack.workflow"
}
```

The schema identifier enables future support for additional artifact types.

Examples:

* pulsestack.workflow
* pulsestack.prompt
* pulsestack.agent
* pulsestack.memory

---

## 4.2 SchemaVersion

Identifies the version of the persistence contract.

Example:

```json
{
    "schemaVersion": "1.0"
}
```

This value is used to determine compatibility and drive future migration pipelines.

---

## 4.3 Workflow Identity

Represents the permanent identity of a workflow.

```text
WorkflowIdentity
│
├── WorkflowId
└── Version
```

### WorkflowId

A globally unique identifier.

The identifier remains constant throughout the lifetime of the workflow.

### Version

Represents the business version of the workflow.

Example:

```text
1.0.0
1.1.0
2.0.0
```

This version is controlled by the workflow author.

---

## 4.4 Workflow Step Identity

The document also contains the workflow's own `WorkflowStepId`.

```text
Id
```

Because a `Workflow` is itself an implementation of `IWorkflowStep`, the workflow participates in the workflow hierarchy like every other step.

This identity enables future support for nested workflows and composite workflow structures.

---

## 4.5 Workflow Definition

Represents the business metadata of the workflow.

```text
WorkflowDefinition
│
├── Name
└── Description
```

The definition intentionally excludes persistence-specific information.

---

## 4.6 Workflow Steps

The root workflow contains a collection of workflow steps.

```text
Steps
│
├── RunStepDocument
├── ConditionalStepDocument
├── ParallelStepDocument
├── LoopStepDocument
└── SwitchStepDocument
```

The step hierarchy forms a recursive tree.

---

# 5. Workflow Step Schema

Every workflow step derives from the common base document.

```text
WorkflowStepDocument
│
├── Id
├── Kind
├── Name
└── Children
```

---

## Id

Stable identifier of the workflow step.

The identifier is generated during workflow construction and preserved across persistence operations.

---

## Kind

Defines the workflow language construct.

Current values include:

* Run
* Conditional
* Parallel
* Loop
* Switch

Future language constructs may introduce additional kinds without affecting existing implementations.

---

## Name

Human-readable name of the step.

Used for:

* Diagnostics
* Logging
* Visual Designer
* Documentation

---

## Children

Represents nested workflow steps.

Simple steps contain an empty collection.

Composite steps contain their child workflow.

---

# 6. Polymorphism

Workflow steps are serialized polymorphically.

The serializer uses a type discriminator.

Example:

```json
{
    "$type": "Run"
}
```

Future workflow language constructs will introduce additional discriminators.

Examples:

```text
Run
Conditional
Parallel
Loop
Switch
```

---

# 7. Agent References

Runtime objects are never serialized.

Instead, run steps store an agent reference.

```text
RunStepDocument
│
└── AgentReference
```

During reconstruction, the mapper resolves the reference using an `IAgentResolver`.

```text
AgentReference
        │
        ▼
IAgentResolver
        │
        ▼
IAgent
```

This keeps persistence independent of dependency injection and runtime configuration.

---

# 8. Example Workflow Document

```json
{
  "schema": "pulsestack.workflow",
  "schemaVersion": "1.0",

  "identity": {
    "id": "8b99d53b-ef5f-4f4b-bd84-1fdde4cce2d4",
    "version": "1.0.0"
  },

  "id": "d22c49d8-8469-43af-a818-c11b0fd3b89b",

  "definition": {
    "name": "Customer Onboarding",
    "description": "Creates a new customer profile."
  },

  "steps": [
    {
      "$type": "Run",
      "id": "b7b15aef-8bb7-4d17-b48e-59e64c58d3d0",
      "kind": "Run",
      "name": "Create Customer",
      "agentReference": "CustomerAgent",
      "children": []
    }
  ]
}
```

---

# 9. Versioning Strategy

Two independent version numbers exist within every persisted workflow.

## Workflow Version

Represents the business evolution of the workflow.

Examples:

```text
1.0.0
1.1.0
2.0.0
```

This version is controlled by workflow authors.

---

## Schema Version

Represents the persistence format.

Examples:

```text
1.0
1.1
2.0
```

This version is controlled by the framework.

These two versions are intentionally independent.

---

# 10. Compatibility

A reader should verify the schema before deserializing a document.

Validation should include:

* Schema identifier
* Schema version
* Document integrity
* Structural validation

Unsupported schema versions should produce validation errors rather than undefined behavior.

---

# 11. Future Migration Pipeline

Future versions of PulseStackAI may introduce document migrations.

The intended migration pipeline is:

```text
WorkflowDocument
        │
        ▼
Read Schema Version
        │
        ▼
Migration Engine
        │
        ▼
Latest Schema
        │
        ▼
Deserializer
```

This enables older workflow documents to remain usable as the framework evolves.

---

# 12. Extensibility

The schema is intentionally designed to evolve.

Potential future additions include:

* Workflow variables
* Parameters
* Triggers
* Schedules
* Policies
* Permissions
* Tags
* Categories
* Designer layout metadata
* Execution checkpoints
* Audit metadata

New fields should preserve backward compatibility whenever practical.

---

# 13. Relationship to the Runtime

The workflow document is **not** a runtime object.

It is a persistence artifact.

```text
Workflow
        │
        ▼
WorkflowMapper
        │
        ▼
WorkflowDocument
        │
        ▼
Persistence
```

The runtime always executes reconstructed `Workflow` objects rather than persistence documents.

---

# 14. Architectural Significance

The `WorkflowDocument` is the canonical artifact of PulseStackAI.

It serves as the foundation for:

* Workflow persistence
* Import and export
* Version control
* Visual Designer
* Workflow Registry
* Cloud deployment
* AI-generated workflows
* Schema migrations
* Backup and restore

As the framework evolves, new capabilities should exchange `WorkflowDocument` instances rather than runtime objects.

---

# 15. Summary

The Workflow Document Schema establishes a stable, versioned, and portable contract for representing workflows.

By separating persistence artifacts from runtime execution, PulseStackAI achieves:

* Clean architectural boundaries
* Storage independence
* Long-term compatibility
* Extensibility
* Provider neutrality
* Future-proof workflow evolution

This specification represents the authoritative definition of the PulseStackAI workflow persistence format.
