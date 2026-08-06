# AI Asset Model vs Runtime

One of the fundamental architectural principles of PulseStackAI is the separation between **authoring** and **execution**.

The **AI Asset Model** defines the reusable software assets that make up an AI application. These assets describe *what exists* and *how applications are composed*, but they do not define how those assets execute.

The **Runtime** is responsible for instantiating those assets, orchestrating their execution, enforcing execution policies, and recording execution history.

This separation ensures that AI applications remain portable, versioned, and independent of any specific execution environment.

```text
                PulseStackAI Architecture

        Authoring Platform              Runtime Platform
        ──────────────────              ────────────────

         AI Asset Model                    Runtime

         Defines                           Defines

         • What exists                     • Execution
         • Identity                        • Cost Tracking
         • Metadata                        • Token Usage
         • Relationships                   • Retry Policies
         • Dependencies                    • Timeouts
         • Lifecycle                       • Provider Selection
         • Composition                     • Observability
         • Versioning                      • Auditing
                                            • Execution History
```

## AI Asset Model Responsibilities

The AI Asset Model defines the canonical representation of AI software assets.

It is responsible for:

- Defining asset types
- Establishing asset identity
- Managing metadata
- Modeling relationships
- Expressing dependencies
- Managing asset lifecycle
- Supporting versioning and packaging

The Asset Model is **provider-independent**, **runtime-independent**, and **execution-independent**.

## Runtime Responsibilities

The Runtime is responsible for executing AI applications built from those assets.

It is responsible for:

- Workflow execution
- Agent execution
- Pipeline orchestration
- Provider integration
- Cost tracking
- Token accounting
- Retry and timeout policies
- Provider failover
- Observability
- Auditing
- Execution history

Execution state exists only while an application is running and is recorded through the transactional `PipelineContext` and runtime event stream.

## Architectural Principle

> **Assets define intent.**
>
> **The Runtime realizes that intent through execution.**

An AI Asset is an immutable, reusable definition of a business capability.

A Runtime Execution is a transient, observable transaction that instantiates those assets to accomplish a specific task.

By separating authoring from execution, PulseStackAI enables AI applications to be portable, reusable, versioned, and executable across different runtimes without changing their underlying asset definitions.