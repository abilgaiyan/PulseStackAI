> **Document Type:** Language Specification
> **Audience:** Contributors
> **Status:** Draft
> **Owner:** PulseStackAI Team
> **Last Reviewed:** 2026-09-21

# Package Language Specification

> **An AI Asset Package is a versioned distribution boundary for AI Asset definitions.**

---

# 1. Domain Meaning

Package is a reusable AI Asset representing one versioned distribution boundary.

Its current implementation has genuine membership, version, and external-dependency authority. Those concepts must be distinguished from software-package distribution mechanisms and from older workflow-specific packaging concepts.

---

# 2. Concept Boundary

In this specification, **Package** means:

```text
AI Asset Package
    PackageAsset
    PackageAssetOptions
```

It does **not** mean:

```text
PulseStackAI NuGet package
    software/binary distribution mechanism

older WorkflowPackage concepts
    separate workflow-specific packaging subsystem
```

NuGet package production and consumption are development/distribution concerns for PulseStackAI binaries. They are not the Package Language defined here.

---

# 3. Current Public Asset Contract

The current public declarative options are:

```text
PackageAssetOptions
├── Name
├── Description
└── Members[]
```

**Name** identifies the AI Asset Package.

**Description** describes the distribution boundary.

**Members** contains direct AI Asset references distributed as members of the Package.

`PackageAsset` also participates in the common Asset model with an `AssetVersion`, and it may carry external `AssetDependency` entries.

Therefore Package version and dependency semantics are real current Asset authority even though they are not additional fields of `PackageAssetOptions`.

---

# 4. Membership and Dependency Authority

Package membership is a real structural relationship.

The current Package reference projection enforces:

- a Package must declare at least one member
- a Package cannot include itself as a direct member
- member Asset types must be defined
- duplicate member definition keys are rejected
- conflicting URNs for the same member definition key are rejected
- a Package cannot depend on itself
- duplicate dependency definition keys are rejected
- conflicting dependency URNs are rejected
- conflicting `Required` values for the same dependency are rejected
- a member cannot also be declared as an external dependency

The final rule can be summarized as:

```text
Members ∩ ExternalDependencies = ∅
```

Package membership is not restricted to the Library member-type whitelist; Package applies its own membership invariants.

---

# 5. Design Vocabulary

The following concepts may be useful when reasoning about AI Asset distribution:

| Concept | Authority |
|---|---|
| **Contents / Members** | Current structural Package authority. |
| **Version** | Current common Asset authority used by Package. |
| **Dependency** | Current Package/common Asset dependency authority. |
| **Manifest** | Design vocabulary; not a current `PackageAssetOptions` field. |
| **Publisher** | Design vocabulary; not a current `PackageAssetOptions` field. |
| **Signature** | Design vocabulary; not a current `PackageAssetOptions` field. |

Manifest, Publisher, and Signature must not be inferred as required persisted Package structure or current package-management capability.

---

# 6. Distribution vs Distribution Mechanism

The Package Asset defines **which AI Asset definitions form a versioned distribution boundary**.

It does not define the mechanism by which software binaries or Package representations are published, downloaded, installed, restored, or cached.

Examples such as:

- NuGet feeds
- Git repositories
- blob storage
- package registries
- download/install workflows

describe possible external distribution mechanisms. They are not current Package Asset fields.

In particular, PulseStackAI's NuGet package feed and development-package workflow do not provide evidence for AI Asset Package semantics.

---

# 7. Runtime and Lifecycle Considerations

Systems working with AI Asset Packages may require concerns such as:

- dependency resolution
- loading package members
- distribution
- verification
- caching

Those are possible downstream persistence, loading, tooling, or integration concerns. This specification does not establish a Package Runtime, package downloader, installer, restore system, signature verifier, or cache manager.

Concrete capabilities require their own current source and architecture authority.

---

# 8. Example

Conceptually, an AI Asset Package may contain:

```text
Financial Operations Package

Members
├── Invoice Workflow
├── Invoice Approval Agent
├── Financial Policy
└── ERP Integration Tool

External Dependencies
└── Shared Finance Asset
```

The `Members` relationship and external dependencies correspond to current Package structure. The example does not imply a NuGet package, downloadable archive, manifest format, publisher field, signature field, or installation lifecycle.

---

# 9. Responsibilities and Boundaries

At the current contract level, Package is responsible for:

- identifying one AI Asset distribution boundary
- describing that boundary
- carrying direct member references
- participating in common Asset versioning
- carrying external dependencies where supplied
- enforcing current Package membership/dependency invariants

Package is not, merely by being a Package Asset, a guarantee of:

- a package repository
- publishing infrastructure
- download or installation
- restore behavior
- signature verification
- caching
- deployment

---

# Summary

AI Asset Package has genuine implemented structural authority:

```text
PackageAssetOptions
├── Name
├── Description
└── Members[]

PackageAsset
├── AssetVersion
└── Dependencies[]
```

Its membership and dependency invariants are source-backed.

Manifest, Publisher, Signature, and package-manager lifecycle concepts remain design or downstream concerns unless separately established by current authority.

Most importantly:

```text
AI Asset Package
        ≠
NuGet software package
        ≠
older WorkflowPackage subsystem
```
