> **Document Type:** Language Specification
> **Audience:** Contributors
> **Status:** Draft
> **Owner:** PulseStackAI Team
> **Last Reviewed:** 2026-09-21

# Prompt Language Specification

> **A Prompt represents reusable system-instruction intent.**

---

# 1. Domain Meaning

Prompt is a reusable AI Asset for describing instructions supplied to an Agent.

The current public contract is intentionally smaller than the richer vocabulary commonly used when designing prompts. This specification distinguishes the implemented Prompt Asset structure from useful prompt-design concepts.

---

# 2. Current Public Asset Contract

The current public declarative contract is:

```text
PromptAssetOptions
├── Name
└── SystemInstructions
```

**Name** identifies the Prompt Asset.

**SystemInstructions** contains the reusable system instructions represented by the Prompt.

These are the current Prompt-specific public fields. `PromptAsset` projects Name into common Asset metadata and retains the Prompt options.

---

# 3. Design Vocabulary

The following concepts remain useful when reasoning about prompt design:

| Concept | Design meaning |
|---|---|
| **Role** | Perspective or responsibility described by instructions. |
| **Context** | Background information useful to the task. |
| **Instruction** | Task or behavior requested from the model. |
| **Template** | Conceptual reusable prompt structure. |
| **Variable** | Conceptual value substituted into prompt material. |
| **Constraint** | Limitation expressed by prompt design. |
| **Example** | Reference material demonstrating expected behavior. |
| **Output** | Intended result or format. |

These terms are **design vocabulary**. Unless represented by the current public Asset contract, they do not define Prompt Asset fields, required persisted structure, executable semantics, or guaranteed runtime capabilities.

In particular, the current `PromptAssetOptions` contract does not expose structured Role, Context, Template, Variables, Constraints, Examples, or Output fields.

Such concepts may be expressed within `SystemInstructions`, but doing so does not create additional structural Prompt properties.

---

# 4. Conceptual Model

A prompt may be designed conceptually using:

```text
Prompt design

├── Role
├── Context
├── Instruction
├── Variables
├── Constraints
├── Examples
└── Expected Output
```

This is a **conceptual model, not an object/property schema**.

The implemented structural contract remains:

```text
Name
SystemInstructions
```

---

# 5. Provider and Configuration Boundary

Prompt does not contain Provider, Model, Temperature, Top P, Max Tokens, streaming, retry, usage, or cost fields.

Provider/model selection belongs to other current application contracts, including the Model Asset where applicable.

Prompt-engineering techniques such as zero-shot, few-shot, ReAct, or other methodologies are also not structural Prompt Asset fields.

---

# 6. Runtime Considerations

Prompt-oriented execution may involve concerns such as:

- composing or rendering instruction material
- supplying execution-time context
- translating instructions into provider-specific messages
- executing model requests
- collecting responses

These are possible downstream runtime or integration concerns. This specification does not guarantee a Prompt Runtime, structured template renderer, variable-resolution system, provider-selection behavior, or usage-tracking subsystem.

Concrete execution semantics require their own current source and architecture authority.

---

# 7. Conceptual Example

The following is prompt-design vocabulary, **not constructible `PromptAssetOptions` syntax**:

```text
Role
Senior .NET Architect

Context
Review supplied C# source code.

Instruction
Identify correctness and maintainability improvements.

Constraint
Explain recommendations.

Output
Markdown review.
```

A current Prompt Asset representing that intent would encode the relevant instruction material through `SystemInstructions` and identify the Asset through `Name`.

---

# 8. Responsibilities and Boundaries

At the current contract level, Prompt is responsible for providing a reusable AI Asset identity and reusable system instructions.

Prompt is not, merely by being a Prompt Asset, a guarantee of:

- structured template rendering
- variable resolution
- provider or model selection
- model execution
- response collection
- usage or cost tracking

Those concerns require separate implementation authority.

---

# Summary

Prompt retains useful design vocabulary while its implemented structural authority remains:

```text
PromptAssetOptions
├── Name
└── SystemInstructions
```

Role, Context, Template, Variable, Constraint, Example, Output, and related concepts may guide prompt design, but they are not current Prompt Asset fields unless the public contract evolves to represent them.
