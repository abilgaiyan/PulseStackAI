> **Document Type:** Language Specification
> **Audience:** Contributors
> **Status:** Draft
> **Owner:** PulseStackAI Team
> **Last Reviewed:** 2026-09-21

# Agent Language Specification

> **An Agent is a reusable business worker that composes referenced AI Assets around a business goal and role.**

---

# 1. Domain Meaning

The Agent Language defines the declarative contract for reusable Agent Assets within PulseStackAI.

An Agent represents a business worker. Unlike language concepts whose current Asset contracts are primarily descriptive, Agent has a substantial implemented composition contract: its goal, role, responsibilities, and referenced Model, Prompt, Knowledge, Tools, Memory, and Policies are current public structure.

Agent definition remains distinct from Agent execution.

---

# 2. Current Public Asset Contract

The current public declarative contract is:

```text
AgentDefinitionOptions
├── Name
├── Goal
├── Role
├── Responsibilities[]
├── Model?
├── Prompt?
├── Knowledge[]
├── Tools[]
├── Memory?
└── Policies[]
```

**Name** identifies the Agent business capability.

**Goal** states the business objective the Agent is responsible for accomplishing.

**Role** states the business role performed by the Agent.

**Responsibilities** records business responsibilities owned by the Agent.

The remaining fields are explicit references to other AI Assets.

`AgentDefinition` normalizes collection values and projects its Model, Prompt, Knowledge, Tools, Memory, and Policy references into the Agent's common `References` collection.

---

# 3. Composition Authority

The following composition is current structural authority:

```text
Agent
├── Model?
├── Prompt?
├── Knowledge[]
├── Tools[]
├── Memory?
└── Policies[]
```

These relationships are not merely conceptual design vocabulary. They are represented directly by `AgentDefinitionOptions`.

An Agent may therefore compose:

- one optional Model Asset
- one optional Prompt Asset
- zero or more Knowledge Assets
- zero or more Tool Assets
- one optional Memory Asset
- zero or more Policy Assets

The collected references are de-duplicated in the Agent definition.

---

# 4. Vocabulary

The following terms are source-backed Agent language:

| Concept | Current meaning |
|---|---|
| **Goal** | Business objective represented by the `Goal` field. |
| **Role** | Business role represented by the `Role` field. |
| **Responsibility** | Business responsibility represented in `Responsibilities`. |
| **Composition** | Explicit Asset references represented by the Agent contract. |

Broader ideas such as planning, reflection, adaptation, autonomy, scheduling, and multi-Agent coordination are not fields or guaranteed semantics of the current Agent Asset contract.

---

# 5. Provider Boundary

Agent does not contain a provider-selection field.

However, an Agent may explicitly reference a Model Asset, and the current Model Asset contract contains its own `Provider` and `Model` selection.

Therefore:

```text
Agent
    references Model Asset
        ↓
Model Asset
    Provider + Model
```

Provider selection is not duplicated as Agent structure, but neither is it independent of the composed application definition.

Provider clients, credentials, endpoints, and provider execution remain outside the Agent Asset contract.

---

# 6. What an Agent is NOT

The existence of an Agent Asset does not itself establish:

- an Agent loop
- planning
- observation or reflection
- autonomous adaptation
- scheduling
- retry or timeout policy
- multi-Agent coordination
- provider client behavior
- runtime telemetry or cost accounting

Those capabilities require their own current source and architecture authority.

---

# 7. Runtime Boundary

Agent definition and Agent execution are separate concerns.

An Agent-oriented runtime may use the Agent's goal, role, responsibilities, and referenced Assets when executing work. This specification does not, by itself, guarantee planning, tool invocation, knowledge retrieval, memory management, policy enforcement, model-selection algorithms, telemetry, or cost tracking.

Any concrete Agent execution semantics must be established by the current runtime contracts and architecture rather than inferred from Agent composition.

---

# 8. Example

Conceptually, an Agent definition may contain:

```text
Name              Architecture Advisor
Goal              Review software architecture
Role              Architecture reviewer
Responsibilities  Review design boundaries

Prompt            Architecture Review Prompt
Knowledge         Engineering Standards
Memory            Project Context
Policies          Architecture Governance
Tools             Repository Analysis
Model             Technical Model
```

Unlike purely conceptual vocabulary examples, the categories above correspond to fields in the current `AgentDefinitionOptions` contract. The example does not imply runtime behavior for the referenced Assets.

---

# 9. Responsibilities and Boundaries

At the current contract level, Agent is responsible for:

- identifying a reusable business worker
- expressing its goal and role
- recording business responsibilities
- composing the supported referenced AI Assets
- participating in the common AI Asset identity and lifecycle model

Agent is not, merely by being an Agent Asset, a guarantee of a particular orchestration or execution strategy.

---

# Summary

Agent has genuine implemented structural authority.

Its current contract includes business semantics and explicit Asset composition:

```text
Name
Goal
Role
Responsibilities[]
Model?
Prompt?
Knowledge[]
Tools[]
Memory?
Policies[]
```

Runtime execution remains a separate authority. The Agent specification therefore preserves its real composition contract without deriving additional orchestration or provider behavior from it.
