# Workflow Document Schema Specification

**Status:** Draft v1.0
**Version:** 1.0
**Applies To:** PulseStackAI Workflow-Specific Persistence
**Last Updated:** September 2026

---

# 1. Introduction

The **Workflow Document Schema** defines the document contract used by PulseStackAI's surviving **Workflow-specific persistence subsystem**.

A `WorkflowDocument` is the durable, portable representation used when a runtime `Workflow` is mapped into that subsystem, serialized, validated, stored, and reconstructed.

Its authority is intentionally narrow:

```text
runtime Workflow
        ↕
IWorkflowMapper
        ↕
WorkflowDocument
        ↕
workflow serializer / deserializer
        ↕
IWorkflowStore
```

This specification does **not** define the persistence representation of a declarative `WorkflowAsset`. The current AI Asset persistence path is separate:

```text
declarative WorkflowAsset
        ↕
WorkflowAssetDocument
        ↕
AI Asset serialization / storage
        ↓
catalog / resolution
        ↓
aggregate graph loading
        ↓
application realization
        ↓
runtime Workflow
```

`WorkflowDocument` and `WorkflowAssetDocument` therefore coexist at different architectural layers. Neither should be inferred to be the persistence representation of the other.

This reconciliation is a documentation authority boundary, **not a deprecation decision**. The Workflow-specific mapper, serializer/deserializer, validator, stores, composition surfaces, and tests remain current where their source contracts establish them.

---

# 2. Authority Boundary

This specification owns the current contract for:

- `WorkflowDocument`
- `WorkflowDocumentSchema`
- Workflow-specific document mapping
- Workflow-specific serialization and deserialization
- Workflow-specific validation
- `IWorkflowStore` persistence semantics where defined by those contracts
- reconstruction of runtime `Workflow` objects through the Workflow-specific mapper

This specification does **not** own:

- `WorkflowAsset`
- `WorkflowAssetDocument`
- general AI Asset serialization
- AI Asset storage or publication
- persistent catalog or resolution
- aggregate graph loading
- application realization
- persisted application architecture

The term **canonical** in this document, where used for a `WorkflowDocument` detail, means canonical **within the Workflow-specific persistence subsystem**. It does not mean universal PulseStackAI persistence authority.

---

# 3. Goals

Within the Workflow-specific persistence subsystem, the document contract is designed to be:

- portable across machines and environments
- independent of runtime object instances
- human-readable
- versioned
- deterministic
- extensible
- storage-provider agnostic
- suitable for source control

These goals describe this subsystem and must not be projected onto the separate AI Asset persistence contract.

---

# 4. Design Principles

## Runtime Independence

A `WorkflowDocument` is a persistence document, not the runtime `Workflow` object itself.

Runtime instances such as Agent objects, execution context, dependency-injection containers, and service providers are not embedded directly in the document.

Only data represented by the Workflow-specific document contract is persisted.

## Stable Identity

The Workflow-specific document carries the workflow identity and workflow-step identity represented by the current source contract.

Those identities are independent of the selected `IWorkflowStore` implementation.

## Layer Separation

The Workflow-specific persistence path separates mapping, document validation, serialization, and storage concerns:

```text
Workflow
      │
      ▼
IWorkflowMapper
      │
      ▼
WorkflowDocument
      │
      ├── validation
      │
      ▼
workflow serialization
      │
      ▼
IWorkflowStore
```

This pipeline describes only direct Workflow persistence. It is not the `WorkflowAssetDocument` / AI Asset persistence pipeline.

---

# 5. Workflow Document Structure

The current `WorkflowDocument` structure is:

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

## 5.1 Schema

`Schema` identifies this Workflow-specific document family.

The current schema identifier is:

```text
pulsestack.workflow
```

as defined by `WorkflowDocumentSchema.Name`.

## 5.2 SchemaVersion

The current Workflow-specific schema version is:

```text
1.0
```

as defined by `WorkflowDocumentSchema.Version`.

Schema version belongs to this document contract and must not be confused with the AI Asset persistence schema.

## 5.3 Workflow Identity

`Identity` is a `WorkflowIdentity` containing the workflow's `WorkflowId` and business `Version`.

This is distinct from the document's `SchemaVersion`.

## 5.4 Workflow Step Identity

`Id` is the workflow's `WorkflowStepId`.

A runtime `Workflow` participates in the workflow hierarchy as an `IWorkflowStep`, so its step identity is represented separately from its `WorkflowIdentity`.

## 5.5 Workflow Definition

`Definition` is the `WorkflowDefinition` associated with the runtime Workflow and contains its business definition data.

## 5.6 Workflow Steps

`Steps` contains the ordered Workflow-specific step documents used to reconstruct the runtime Workflow.

The exact supported step-document shapes and discriminators are defined by the current Workflow-specific document and serialization source contracts.

---

# 6. Workflow Step Documents

Workflow-specific step documents derive from the subsystem's `WorkflowStepDocument` representation and preserve the source-backed data needed by the mapper and serializer.

Current step-document behavior must be read from the implemented document hierarchy rather than inferred from future workflow-language ideas.

Stable step identity represented by the current document contract remains part of this subsystem.

---

# 7. Polymorphic Serialization

The Workflow-specific serializer/deserializer supports polymorphic step documents using the discriminators configured by its current serialization implementation.

Those discriminator rules are normative only to the extent established by the current Workflow-specific source and tests.

They are not the discriminator contract of `WorkflowAssetDocument` or the general AI Asset codec.

---

# 8. Agent References and Reconstruction

Runtime Agent instances are not serialized into `WorkflowDocument`.

Workflow-specific Run step documents carry an Agent reference. Reconstruction occurs through:

```text
WorkflowDocument
        │
        ▼
IWorkflowMapper.FromDocument(...)
        │
        ├── IAgentResolver
        ▼
runtime Workflow
```

`IWorkflowMapper.FromDocument(WorkflowDocument, IAgentResolver)` therefore establishes the source-backed Agent-resolution boundary for this subsystem.

This does not define how declarative Agent Assets or Workflow Assets are persisted, resolved, graph-loaded, or realized through the AI Asset application path.

---

# 9. Example Workflow Document

The following illustrates the Workflow-specific document family:

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

The exact accepted JSON representation remains governed by the current Workflow-specific serializer/deserializer implementation. This example is not an AI Asset `WorkflowAssetDocument`.

---

# 10. Versioning

Two different version concepts are represented by the Workflow-specific persistence model.

## Workflow Version

The `WorkflowIdentity` carries the business version of the runtime Workflow.

## Schema Version

`WorkflowDocument.SchemaVersion` identifies the Workflow-specific persistence schema version.

The current schema contract is `1.0`.

These versions are independent.

Neither should be confused with the identity/versioning model of a declarative AI Asset merely because both persistence generations coexist.

---

# 11. Validation and Compatibility

The surviving Workflow-specific subsystem includes validation contracts and implementation for `WorkflowDocument`.

Validation and compatibility requirements are normative only where established by the current Workflow-specific validator, serializer/deserializer, mapper, stores, and their tests.

Unsupported or malformed Workflow-specific documents should be handled according to those current contracts rather than according to the AI Asset document validator.

Conversely, `IAIAssetDocumentValidator` is the authority for the separate AI Asset document model and is outside this specification.

---

# 12. Storage

`IWorkflowStore` remains the storage abstraction for the direct Workflow persistence subsystem.

Its current contract provides operations to save, load, delete, and test existence using `WorkflowId` and serialized streams.

Built-in in-memory and file Workflow storage composition remains separate from AI Asset serialized storage.

Therefore:

```text
WorkflowDocument serialization
        ↓
IWorkflowStore

is separate from

WorkflowAssetDocument / AIAssetDocument
        ↓
ISerializedAIAssetStore
```

This specification does not imply that `WorkflowAssetDocument` is stored through `IWorkflowStore`.

---

# 13. Relationship to Runtime and Declarative Assets

The direct Workflow persistence subsystem begins with and reconstructs a runtime `Workflow`:

```text
runtime Workflow
        ↕
WorkflowDocument
        ↕
Workflow-specific persistence
```

The declarative application path begins with a `WorkflowAsset` and persists it as a `WorkflowAssetDocument` through the AI Asset persistence system before graph loading and realization produce a runtime `Workflow`.

```text
WorkflowAsset
        ↕
WorkflowAssetDocument
        ↕
AI Asset persistence
        ↓
graph loading
        ↓
realization
        ↓
runtime Workflow
```

The shared endpoint of a runtime `Workflow` does not merge these persistence paths.

---

# 14. Future and Design Context

Potential future concerns may include migrations, designer metadata, import/export tooling, registries, deployment workflows, backup/restore, or other document-oriented capabilities.

Those are **design possibilities**, not guaranteed current capabilities of `WorkflowDocument`.

In particular, this specification does not establish:

- a migration engine
- a Visual Designer
- a Workflow Registry
- cloud deployment behavior
- an import/export subsystem
- backup/restore infrastructure
- a rule that future PulseStackAI capabilities must exchange `WorkflowDocument`

Any such capability requires its own current source and architecture authority.

---

# 15. Architectural Significance

`WorkflowDocument` remains a real persistence contract for the surviving direct Workflow persistence subsystem.

Its significance is therefore specific:

```text
Workflow
    ↔
WorkflowDocument
    ↔
Workflow-specific persistence
```

It is **not** the canonical artifact of PulseStackAI as a whole, and it does not supersede or redefine the declarative AI Asset persistence architecture.

The coexistence of the two persistence generations does not, by itself, deprecate either API or require migration between them.

---

# 16. Summary

The Workflow Document Schema remains current authority for the Workflow-specific persistence subsystem.

It defines the document boundary used by:

```text
Workflow
    ↔
IWorkflowMapper
    ↔
WorkflowDocument
    ↔
workflow serializer / deserializer
    ↔
IWorkflowStore
```

The current declarative application path is separate:

```text
WorkflowAsset
    ↔
WorkflowAssetDocument
    ↔
AI Asset persistence
```

Accordingly:

```text
WorkflowDocument
    = current Workflow-specific persistence contract

WorkflowAssetDocument
    = current declarative Workflow Asset persistence representation

WorkflowDocument
    ≠ universal PulseStackAI workflow persistence format
```

This specification narrows authority to match the surviving implementation. It does not redesign, deprecate, or remove either persistence subsystem.
