> **Document Type:** Language Specification
> **Audience:** Contributors
> **Status:** Draft
> **Owner:** PulseStackAI Team
> **Last Reviewed:** 2026-09-21

# Library Language Specification

> **A Library is one flat reusable collection of AI Asset definitions.**

---

# 1. Domain Meaning

Library is a reusable AI Asset that groups related AI Asset definitions.

Its current contract has genuine membership structure. Library membership is not merely conceptual vocabulary: `LibraryAssetOptions.Members` and the Library reference projection define and validate the collection.

Library remains distinct from storage, package distribution, deployment, and runtime execution.

---

# 2. Current Public Asset Contract

The current public declarative contract is:

```text
LibraryAssetOptions
├── Name
├── Description
└── Members[]
```

**Name** identifies the Library.

**Description** describes the reusable collection.

**Members** contains the Library's direct member Asset references.

`LibraryAsset` normalizes the member collection, projects valid members into its common `References` collection, and may also carry external `AssetDependency` entries through the common Asset dependency model.

---

# 3. Membership Authority

A Library is a **flat** reusable collection.

The current direct member types are:

```text
Library
├── Workflow
├── Agent
├── Prompt
├── Tool
├── Knowledge
├── Memory
├── Policy
└── Model
```

This list represents allowed direct member types. It does not require every Library to contain every type.

The Library reference projection enforces:

- only the supported member Asset types are accepted
- duplicate member definition keys are rejected
- the same definition key with conflicting URNs is rejected

Library membership and external dependency classification are also exclusive:

```text
Members ∩ ExternalDependencies = ∅
```

A member cannot simultaneously be declared as an external dependency of the same Library.

---

# 4. Design Vocabulary

Concepts such as:

- Collection
- Category
- Namespace
- Domain
- Catalog
- Discovery

can be useful when reasoning about how reusable Assets are organized.

Only the concepts represented by the current public contract and membership projection are structural Library authority.

In particular, Category, Namespace, Domain, and Catalog are not fields of the current `LibraryAssetOptions` contract, and the existence of a Library Asset does not establish a catalog or discovery subsystem.

---

# 5. Library vs Package

Library and Package are separate AI Asset concepts.

```text
Library
    organizational membership boundary

Package
    versioned distribution boundary
```

A Library is not a package repository, NuGet package, package feed, deployment artifact, or software library/assembly abstraction.

Library membership should therefore not be interpreted as package distribution semantics.

---

# 6. Storage and Runtime Boundaries

A Library Asset does not establish:

- where its definitions are stored
- a repository or registry
- indexing
- catalog search
- discovery infrastructure
- runtime execution
- package installation or restore

Applications or infrastructure may provide discovery, indexing, searching, resolution, or loading capabilities, but those behaviors require their own current source and architecture authority.

The Library specification defines the Library Asset and its membership boundary, not a Library Runtime.

---

# 7. Example

A valid conceptual Library membership might be:

```text
Engineering Library

Members
├── Code Review Prompt
├── Architecture Knowledge
├── Engineering Standards Policy
├── Repository Analysis Tool
├── Architecture Agent
└── Architecture Workflow
```

Unlike design-only composition diagrams, the `Members` relationship itself corresponds to current Library structure. The named examples are illustrative Asset references; they do not establish catalog, indexing, or discovery behavior.

---

# 8. Responsibilities and Boundaries

At the current contract level, Library is responsible for:

- identifying one reusable collection
- describing that collection
- carrying its direct member references
- enforcing current Library membership invariants
- distinguishing members from external dependencies

Library is not, merely by being a Library Asset, a guarantee of discovery, search, indexing, version management, storage, or distribution behavior.

---

# Summary

Library has genuine implemented structural authority:

```text
LibraryAssetOptions
├── Name
├── Description
└── Members[]
```

Its allowed direct membership and membership invariants are source-backed.

Broader organizational vocabulary can remain useful for design, but it must not be confused with current Library fields or with guaranteed catalog/discovery infrastructure.
