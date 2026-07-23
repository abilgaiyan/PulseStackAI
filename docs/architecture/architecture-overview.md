# Architecture Overview

PulseStackAI separates business workflow modeling from execution, allowing developers to express intent while the runtime manages orchestration, resilience, persistence, and AI provider integration.

## Overview

PulseStackAI is a modular, enterprise-grade AI orchestration framework for .NET that enables developers to build intelligent applications using composable workflows, agents, tools, and providers.

Rather than exposing provider-specific APIs, PulseStackAI provides a unified programming model for designing, executing, and evolving AI-powered systems.

The framework separates **workflow definition** from **workflow execution**, allowing business workflows to remain portable while the runtime manages orchestration, resilience, observability, persistence, and provider integration.

This separation enables developers to focus on business logic while PulseStackAI manages the complexity of AI orchestration.

---

# Vision

PulseStackAI is built around a simple architectural philosophy:

> Developers express **business intent**. The runtime manages **execution**.

```text
Developer

↓

Workflow Model

↓

Workflow Runtime

↓

AI Providers
```

Business workflows remain independent of the underlying AI providers, enabling applications to evolve without rewriting orchestration logic.

---

# Design Goals

The architecture is guided by the following principles:

- Clean Architecture
- Provider Independence
- Workflow-First Design
- Extensibility
- Enterprise Readiness
- Observability
- Testability
- Incremental Evolution

These goals influence every subsystem within the framework.

---

# Architecture at a Glance

```text
                PulseStackAI

                 Developer
                     │
                     ▼
             Workflow Language
                     │
                     ▼
             Workflow Model
                     │
        ┌────────────┴────────────┐
        ▼                         ▼
 Persistence                Workflow Runtime
        │                         │
        ▼                         ▼
 Workflow Store          Step Executors
        │                         │
        └────────────┬────────────┘
                     ▼
               Agent Runtime
                     │
                     ▼
                Tool Runtime
                     │
                     ▼
                AI Providers
                     │
                     ▼
        OpenAI • Azure • Ollama • Others
```

This layered architecture separates concerns while keeping each subsystem independently extensible.

---

# Core Building Blocks

PulseStackAI is composed of several core architectural concepts.

## Workflow

A workflow represents a business process expressed using composable workflow steps.

Workflows describe **what** should happen rather than **how** it is executed.

---

## Workflow Step

Workflow Steps are the fundamental building blocks of a workflow.

Examples include:

- RunStep
- ConditionalStep
- ParallelStep
- LoopStep *(planned)*

Each step represents a discrete unit of orchestration.

---

## Agent

Agents encapsulate AI behavior.

They coordinate prompts, tools, memory, and model interactions to accomplish a specific task.

---

## Tool

Tools provide structured access to external capabilities such as APIs, databases, ERP systems, or custom business logic.

---

## Provider

Providers integrate external AI models while remaining isolated behind a common abstraction.

This enables applications to switch providers without changing orchestration code.

---

# Solution Architecture

The solution is organized into modular projects with clearly defined responsibilities.

```text
PulseStack.Abstractions
        │
        ▼
PulseStack.Core
        │
        ▼
PulseStack.Agents
        │
        ▼
PulseStack.Providers.*
        │
        ▼
Samples
```

Each layer depends only on lower-level abstractions, preserving clean architectural boundaries.

---

# Layer Responsibilities

## PulseStack.Abstractions

Defines the public contracts shared across the framework.

Examples include:

- Workflows
- Agents
- Tools
- Providers
- Runtime abstractions
- Persistence contracts

---

## PulseStack.Core

Provides foundational infrastructure used throughout the framework.

Responsibilities include:

- Dependency Injection
- Runtime services
- Persistence
- Validation
- Serialization
- Shared infrastructure

---

## PulseStack.Agents

Implements the workflow execution engine.

Responsibilities include:

- Workflow Runtime
- Step Executors
- Agent Runtime
- Pipeline orchestration
- Execution context
- Runtime events

---

## PulseStack.Providers

Implements integrations with external AI providers.

Each provider package remains isolated from the orchestration layer.

---

# Runtime Architecture

The runtime is responsible for executing workflows.

Rather than embedding execution logic within workflow definitions, PulseStackAI delegates execution to specialized runtime components.

```text
Workflow

↓

Workflow Runtime

↓

Step Executors

↓

Agent Runtime

↓

Tool Runtime

↓

AI Provider
```

This separation keeps workflows declarative while allowing the runtime to evolve independently.

---

# Persistence Architecture

Workflow persistence is implemented as an independent subsystem.

```text
Workflow

↓

Mapper

↓

Document

↓

Validator

↓

Serializer

↓

Store
```

This architecture enables workflows to be validated, serialized, versioned, stored, and reconstructed without affecting runtime behavior.

---

# Extension Points

PulseStackAI is designed for extensibility.

Developers can extend the framework by implementing custom:

- Providers
- Workflow Steps
- Agents
- Tools
- Serializers
- Validators
- Stores

Each extension point is exposed through well-defined abstractions.

---

# Design Principles

The architecture follows a consistent engineering philosophy.

> Think in Workflows.

> Design the Runtime.

> Hide the Complexity.

> Compose, Don't Couple.

> Model Before Implementation.

> Document the Why.

> Test the Behavior.

> Leave the Framework Better.

These principles guide every architectural decision within the project.

---

# Architecture Roadmap

The architecture continues to evolve incrementally.

## Current Capabilities

- Workflow Runtime
- Workflow Persistence
- AI Providers
- Tool Framework

## Future Capabilities

- Planner
- Scheduling
- Human Approval
- MCP Integration
- Distributed Runtime
- Visual Designer

Each milestone expands the framework while preserving architectural consistency.

---

# What's Next

This document provides the architectural overview of PulseStackAI.

The following documents explore each subsystem in greater depth:

- Workflow Architecture
- Runtime Architecture
- Persistence Architecture
- Execution Flow
- Extension Points
- Solution Structure
- Domain Model

Together, these documents describe the complete architecture of PulseStackAI, from high-level concepts to implementation details.