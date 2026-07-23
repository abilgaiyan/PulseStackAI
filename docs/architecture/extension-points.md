# Extension Points

## Introduction

PulseStackAI is designed as an extensible framework rather than a closed platform.

Every major subsystem exposes well-defined extension points that allow applications to introduce new capabilities without modifying the core framework.

The Extension Points document answers one question:

> **How can developers extend PulseStackAI?**

The framework follows the Open/Closed Principle by encouraging extension through abstractions while keeping the core implementation stable.

---

# Design Philosophy

Extension is a fundamental architectural principle of PulseStackAI.

Every extension point follows these goals:

- Extend rather than modify
- Program against abstractions
- Keep implementations isolated
- Preserve architectural boundaries
- Support dependency injection
- Encourage reusable components

Most customizations are introduced by implementing an interface and registering the implementation with the dependency injection container.

---

# Extension Overview

The framework provides extension points across multiple architectural layers.

```text
                    PulseStackAI

                           │

        ┌───────────┬────────────┬────────────┬────────────┐
        ▼           ▼            ▼            ▼

     Agents       Tools      Providers     Workflows

        ▼           ▼            ▼            ▼

    Runtime     Services     AI Models   Step Executors

        │
        ▼

 Persistence
        │
        ▼

Validators → Serializers → Stores
```

Each extension point has a clearly defined responsibility.

---

# Agent Extensions

Purpose

Create reusable AI behavior.

Extension

Implement:

- IAgent

Registered Through

- Dependency Injection

Used By

- Workflow Runtime

Typical Scenarios

- Chat agents
- Research agents
- Planning agents
- Domain-specific assistants

---

# Tool Extensions

Purpose

Expose external capabilities to agents.

Implement

- ITool

Registered Through

- Tool Registry

Used By

- Agent Runtime

Examples

- REST APIs
- Databases
- Browser Automation
- ERP Systems
- File Systems

---

# Provider Extensions

Purpose

Integrate external AI platforms.

Implement

- IChatClient
- Provider services

Examples

- OpenAI
- Azure OpenAI
- Ollama
- Gemini
- Groq

Providers isolate vendor-specific SDKs from the rest of the framework.

---

# Workflow Step Extensions

Purpose

Introduce new orchestration behaviors.

Examples

- Human Approval Step
- Delay Step
- Event Step
- Planner Step
- Package Step

Each workflow step represents a new business capability while remaining part of the Workflow Model.

---

# Step Executor Extensions

Every workflow step is executed by a corresponding Step Executor.

Examples

- RunStepExecutor
- ConditionalStepExecutor
- ParallelStepExecutor
- LoopStepExecutor
- RetryStepExecutor
- SwitchStepExecutor

New workflow steps typically introduce new executors rather than modifying the runtime.

---

# Persistence Extensions

The persistence pipeline supports extension at every stage.

## Workflow Mapper

Transforms between Workflow and WorkflowDocument.

## Validation Rules

Add organization-specific validation.

## Serializers

Support additional transport formats.

Examples

- YAML
- XML
- Binary

## Stores

Persist workflows using any storage technology.

Examples

- Azure Blob Storage
- SQL Server
- PostgreSQL
- Git
- MongoDB

---

# Runtime Events

Applications may subscribe to runtime events without modifying workflow execution.

Examples

- Logging
- Metrics
- Telemetry
- Audit
- Notifications

Observability remains independent of business execution.

---

# Dependency Injection

Every extension integrates through dependency injection.

```text
Application

        │

        ▼

Service Collection

        │

        ▼

PulseStack Runtime

        │

        ▼

Extension Implementation
```

This enables applications to replace or augment framework behavior using standard .NET dependency injection patterns.

---

# Best Practices

When creating extensions:

- Prefer composition over inheritance.
- Keep responsibilities focused.
- Depend on abstractions.
- Avoid coupling extensions together.
- Register services through dependency injection.
- Preserve provider independence.
- Keep implementations stateless where practical.

---

# Summary

PulseStackAI is designed to grow through extension rather than modification.

Every major architectural subsystem exposes well-defined extension points that allow applications to introduce new capabilities while preserving the framework's modular architecture.

By extending abstractions instead of changing core components, applications remain maintainable, testable, and compatible with future versions of the framework.