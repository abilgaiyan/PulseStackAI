> **Document Type:** Language Specification
> **Audience:** Contributors
> **Status:** Draft
> **Owner:** PulseStackAI Team
> **Last Reviewed:** 2026-08-03

# Prompt Language Specification

> **A Prompt defines what should be communicated, not how it should be executed.**

---

# 1. Vision

The Prompt Language defines the vocabulary used to describe reusable AI instructions within PulseStackAI.

Rather than treating prompts as provider-specific strings or message payloads, the Prompt Language models prompts as reusable engineering assets.

A Prompt represents communication intent.

The Runtime is responsible for translating that intent into the format required by a specific AI provider.

The Prompt Language therefore remains independent of:

- AI providers
- Model implementations
- Runtime execution
- Infrastructure technologies

This enables prompts to remain reusable, portable, composable, and versioned across different AI platforms.

---

# 2. What is a Prompt?

A Prompt is a reusable AI Asset that describes what should be communicated to an AI model in order to achieve a specific outcome.

A Prompt defines intent rather than execution.

It describes:

- what the AI should understand
- what the AI should do
- what constraints apply
- what outcome is expected

A Prompt never describes:

- which provider should execute it
- which model should execute it
- how execution should occur

---

# 3. Purpose

The purpose of a Prompt is to provide reusable instructions that can be composed into AI-powered business applications.

A Prompt enables developers to separate business communication from runtime implementation.

Instead of embedding instructions directly into application code, developers create reusable Prompt Assets that can be shared, versioned, validated, packaged, and composed.

---

# 4. Vocabulary

The Prompt Language defines the following core vocabulary.

| Concept | Description |
|----------|-------------|
| **Role** | The perspective or responsibility the AI should assume. |
| **Context** | Background information required to understand the task. |
| **Instruction** | The primary task to perform. |
| **Template** | Reusable prompt structure containing variables. |
| **Variable** | Placeholder values supplied at execution time. |
| **Constraint** | Rules or limitations that guide the response. |
| **Example** | Reference examples demonstrating expected behavior. |
| **Output** | Description of the expected result or format. |

These concepts define the Prompt Language independently of any AI provider.

---

# 5. Responsibilities

A Prompt is responsible for:

- expressing communication intent
- describing business instructions
- providing reusable templates
- defining reusable variables
- documenting expected output
- remaining reusable across applications

A Prompt is not responsible for execution.

---

# 6. What a Prompt is NOT

A Prompt is intentionally independent of runtime execution.

The following concepts do **not** belong to the Prompt Language:

- AI Provider
- Model Selection
- Temperature
- Top P
- Max Tokens
- Retry Policies
- Streaming
- Memory Management
- Token Usage
- Cost Tracking

These concerns belong to Asset Configuration or the Runtime.

Likewise, prompt engineering techniques such as:

- Zero-shot Prompting
- Few-shot Prompting
- Chain-of-Thought Prompting
- ReAct
- Self-Consistency

are engineering methodologies rather than Prompt Language constructs.

---

# 7. Prompt Composition

A Prompt may be composed from multiple reusable language elements.

```
Prompt

├── Role

├── Context

├── Instruction

├── Variables

├── Constraints

├── Examples

└── Expected Output
```

Each element contributes to the overall communication while remaining independent of execution.

---

# 8. Configuration Boundary

Prompts describe **what** should be communicated.

Configuration describes **how** that communication should be implemented.

Examples of configuration include:

- AI Provider
- Model
- Temperature
- Response Format
- Token Limits
- Execution Policies

Configuration may vary without requiring changes to the Prompt itself.

---

# 9. Runtime Boundary

The Runtime is responsible for realizing the Prompt.

Its responsibilities include:

- rendering prompt templates
- resolving variables
- selecting providers
- selecting models
- translating Prompt Language into provider-specific message formats
- executing requests
- collecting responses
- tracking usage
- recording observability

The Runtime executes the Prompt.

The Prompt never executes itself.

---

# 10. Examples

## Customer Support

```
Role
Customer Support Specialist

Context
Assist customers with subscription questions.

Instruction
Answer clearly and accurately using the supplied knowledge.

Constraint
Never invent information.

Output
Professional response.
```

---

## Code Review

```
Role
Senior .NET Architect

Context
Review C# source code.

Instruction
Identify correctness, maintainability, and performance improvements.

Constraint
Explain recommendations with reasoning.

Output
Markdown review report.
```

---

## Architecture Review

```
Role
Enterprise Solution Architect

Context
Review the supplied architecture proposal.

Instruction
Evaluate design principles, separation of concerns, and extensibility.

Constraint
Remain provider-independent.

Output
Architecture review with recommendations.
```

---

# Summary

The Prompt Language defines a provider-independent vocabulary for expressing reusable AI instructions.

It separates communication intent from execution, allowing prompts to remain portable, reusable, versioned, and composable.

Prompts answer one fundamental question:

> **What should be communicated?**

Configuration determines how that communication is implemented.

The Runtime determines how that communication is executed.