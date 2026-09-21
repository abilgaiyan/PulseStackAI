> **Document Type:** Language Specification
> **Audience:** Contributors
> **Status:** Draft
> **Owner:** PulseStackAI Team
> **Last Reviewed:** 2026-09-21

# Project Language Specification

> **Project defines the root and semantic membership of one intelligent application.**

---

# 1. Vision

The Project Language defines the declarative application-root contract used by PulseStackAI.

A Project Asset represents one intelligent application. It identifies the Workflow that serves as the application's entry point and the AI Assets owned by that application.

The Project Language remains independent of:

- source-control repositories
- workspace or folder layout
- build and deployment pipelines
- hosting infrastructure
- runtime execution mechanics

A Project is therefore an application definition and ownership boundary, not a repository, deployment unit, or runtime host.

---

# 2. What is a Project?

A Project is a reusable AI Asset whose public declarative options are:

```text
ProjectAssetOptions
├── Name
├── Description?
├── EntryWorkflow
└── OwnedAssets[]
```

**Name** identifies the application for developers and diagnostics.

**Description** optionally describes the application.

**EntryWorkflow** is the required Workflow Asset reference that identifies the application's entry workflow.

**OwnedAssets** is the collection of AI Asset references semantically owned by the Project.

A Project defines application composition and ownership. It does not execute the application.

---

# 3. Purpose

The purpose of a Project is to provide the root definition for one intelligent application.

Instead of requiring consumers to independently identify the application's entry workflow and all application-owned definitions, the Project provides one Project Asset identity from which the persisted application graph can be loaded and realized.

Conceptually:

```text
Project
├── EntryWorkflow
└── OwnedAssets[]
```

The Project is the supported application root for application realization and invocation boundaries.

---

# 4. Vocabulary

The current Project Language defines the following normative concepts.

| Concept | Description |
|----------|-------------|
| **Project** | AI Asset representing one intelligent application. |
| **Name** | Display name of the application. |
| **Description** | Optional application description. |
| **Entry Workflow** | Required Workflow Asset reference that defines the application's execution entry point. |
| **Owned Assets** | AI Asset references semantically owned by the Project. |
| **External Dependency** | Referenced dependency required by the Project but not owned by it. |

Libraries and Packages are separate AI Asset domain concepts. A Project may participate in graphs containing those Assets, but `ProjectAssetOptions` does not define Project composition as a list of Libraries or Packages.

---

# 5. Ownership and Dependency Invariants

Project membership and external dependency ownership are distinct.

A Project-owned Asset cannot also be declared as an external dependency of the same Project.

```text
OwnedAssets ∩ ExternalDependencies = ∅
```

A Project also cannot depend on another Project Asset.

These rules preserve a single Project root for the application and prevent the same Asset definition from being simultaneously classified as owned and external.

The Project's references are projected from its required `EntryWorkflow` and `OwnedAssets` according to the Project reference contract.

---

# 6. What a Project is NOT

A Project is not:

- a Git repository
- an Azure DevOps or GitHub project
- a source-code folder
- a build pipeline
- a deployment manifest
- a container or Kubernetes workload
- a runtime host
- a list of Libraries
- a NuGet package

Those concepts may participate in development, distribution, or deployment, but they are not fields of the current Project Asset contract.

---

# 7. Application Composition

The Project is the application root, while other AI Asset types retain their own contracts.

A Project may own references to application Assets such as:

- Workflow
- Agent
- Prompt
- Tool
- Knowledge
- Memory
- Policy
- Model
- Library
- Package

The exact validity of the resulting persisted aggregate is governed by the applicable AI Asset graph and persistence contracts.

Project ownership must not be confused with Library membership or Package distribution membership.

---

# 8. Persistence and Resolution Boundary

A Project Asset can be persisted and published through the common AI Asset persistence platform.

Its persisted `AssetDefinitionKey` can then serve as the root key for loading the application's aggregate graph.

The Project specification does not own:

- serialization format
- storage-provider behavior
- catalog publication mechanics
- aggregate graph-loading algorithms

Those responsibilities belong to the persistent AI Asset platform.

---

# 9. Realization and Runtime Boundary

Project defines the application; it does not execute it.

The current application path is conceptually:

```text
Project AssetDefinitionKey
        ↓
aggregate graph loading
        ↓
application realization
        ↓
application invocation
        ↓
Workflow Runtime
```

Application realization accepts a Project-rooted graph and selects the Project's entry Workflow for the realized application.

Runtime execution remains downstream of Project definition.

---

# 10. Example

Conceptually:

```text
Project
Name
    Meridian Works

EntryWorkflow
    RFQ Analysis Workflow

OwnedAssets
    RFQ Analysis Workflow
    RFQ Analysis Agent
    RFQ Analysis Prompt
    RFQ Analysis Model
```

The Project records application ownership and its required entry Workflow. It does not describe provider credentials, runtime hosting, deployment, or execution mechanics.

---

# Summary

The Project Language defines the declarative root of one intelligent PulseStackAI application.

Its current public contract is:

```text
ProjectAssetOptions
├── Name
├── Description?
├── EntryWorkflow
└── OwnedAssets[]
```

Project therefore answers:

> **What is this application, which Workflow is its entry point, and which AI Assets does it own?**

Persistence determines how the Project definition is stored and published.

Application realization determines how a Project-rooted graph becomes a realized application.

Runtime execution determines how the realized entry Workflow executes.
