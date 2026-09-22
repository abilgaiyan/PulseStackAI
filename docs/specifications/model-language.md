> **Document Type:** Language Specification
> **Audience:** Contributors
> **Status:** Draft
> **Owner:** PulseStackAI Team
> **Last Reviewed:** 2026-09-21

# Model Language Specification

> **A Model identifies the provider and model implementation that supplies intelligence to an AI application.**

---

# 1. Vision

The Model Language defines the declarative Model Asset contract used by PulseStackAI applications.

A Model Asset identifies the AI provider and concrete model that an application intends to use. It is an application definition, not an inference client or runtime execution object.

The Model Language therefore separates:

- declarative provider/model selection
- provider integration and credentials
- runtime inference and execution

This keeps model selection explicit in the persisted application definition while leaving provider implementation and execution concerns outside the Model Asset.

---

# 2. What is a Model?

A Model is a reusable AI Asset whose public declarative options are:

```text
ModelAssetOptions
├── Provider
└── Model
```

**Provider** identifies the provider integration selected by the application.

**Model** identifies the provider model selected by the application.

Both values are part of the Model Asset definition.

A Model does not itself:

- create provider clients
- hold provider credentials
- invoke inference
- stream responses
- collect usage
- execute application workflows

Those responsibilities belong to provider integrations and runtime execution boundaries.

---

# 3. Purpose

The purpose of a Model Asset is to make provider/model selection an explicit, reusable part of an AI application definition.

For example, an application may declare a Model Asset that identifies:

```text
Provider
OpenRouter

Model
deepseek/deepseek-chat-v3-0324
```

The Model Asset records that selection. Provider registration, credentials, client construction, request execution, and response handling remain separate concerns.

---

# 4. Vocabulary

The current Model Language has two normative configuration values.

| Concept | Description |
|----------|-------------|
| **Provider** | Provider integration selected for the Model Asset. |
| **Model** | Provider model identifier selected for the Model Asset. |

Other concepts such as reasoning capability, vision, speech, planning, generation, classification, temperature, token limits, and context windows are not currently fields of the public `ModelAssetOptions` contract.

They may be useful architectural or application concepts, but this specification does not define them as current Model Asset language elements.

---

# 5. Responsibilities

A Model Asset is responsible for:

- declaring the selected provider
- declaring the selected provider model
- participating as a reusable AI Asset in application composition
- carrying stable AI Asset identity, version, metadata, and lifecycle through the common Asset contract

A Model Asset is not responsible for:

- provider credentials
- provider client construction
- inference execution
- streaming
- sampling
- response generation
- usage or cost collection

---

# 6. Provider Boundary

Provider selection is part of the Model Asset contract.

Provider implementation is not.

```text
ModelAsset
    Provider + Model
        ↓
application realization / runtime use
        ↓
configured provider integration
        ↓
provider execution
```

The Model Asset identifies what provider/model pair the application selects. The configured provider integration owns communication with that provider.

Changing `Provider` or `Model` changes the Model Asset definition; changing credentials, endpoints, client lifetime, or provider-service registration does not redefine the Model Language.

---

# 7. Runtime Boundary

The Model Asset is declarative and does not execute itself.

Runtime and provider-specific responsibilities may include:

- resolving the configured provider integration
- constructing or obtaining provider clients
- invoking the selected model
- handling provider requests and responses
- applying execution-specific options
- collecting execution results

Those responsibilities must not be inferred as fields of the Model Asset merely because they participate in model execution.

---

# 8. Composition

Other AI Assets may reference a Model Asset as part of application composition.

For example, an Agent can reference a Model Asset through its declarative Agent contract. That relationship does not transfer provider execution responsibilities into the Agent or Model Asset.

The Model remains the declarative provider/model selection used by the composed application.

---

# 9. Example

```csharp
var options = new ModelAssetOptions(
    Provider: "OpenRouter",
    Model: "deepseek/deepseek-chat-v3-0324");
```

Conceptually:

```text
Model Asset
├── Provider: OpenRouter
└── Model: deepseek/deepseek-chat-v3-0324
```

This example describes the public Model Asset options. It does not configure credentials or execute inference.

---

# Summary

The Model Language defines the declarative provider/model selection used by a PulseStackAI application.

Its current public contract is:

```text
ModelAssetOptions(Provider, Model)
```

Model therefore answers:

> **Which provider and model implementation does this application definition select?**

Provider integrations determine how that selection is connected to an external AI provider.

Runtime execution determines when and how the selected model is invoked.
