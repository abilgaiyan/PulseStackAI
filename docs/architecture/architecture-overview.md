# Architecture Overview

> **PulseStackAI separates declarative AI application definition from persistence, realization, and runtime execution.**

This document is the canonical high-level architecture map for the current framework. It describes implemented boundaries and how they relate. Detailed language contracts, API usage, and historical milestone decisions belong in specifications, guides, and engineering records respectively.

## Architectural principle

Developers describe an application using AI Assets. PulseStackAI can persist and publish those definitions, resolve a persisted application graph, realize that graph into existing runtime objects, and execute the application's entry workflow through the runtime.

Definition and execution remain separate concerns:

- **Definition** describes application identity, composition, references, and workflow intent.
- **Persistence and resolution** make those definitions durable and discoverable.
- **Realization** converts an accepted declarative graph into the runtime representation required for execution.
- **Execution** runs the realized workflow through the workflow and agent runtimes.
- **Provider integrations** supply concrete AI-provider behavior behind framework composition boundaries.

The framework has provider boundaries, but this overview does not claim that every persisted asset is provider-neutral. For example, the current Model Asset contract identifies both a provider and a model.

## Architecture at a glance

```text
Business Intent
        │
        ▼
AI Application Language
        │
        ▼
AI Asset Model
        │
        ▼
Persistence & Publication
        │
        ▼
Catalog & Resolution
        │
        ▼
Aggregate Graph Loading
        │
        ▼
Application Realization
        │
        ▼
Application Operation
        │
        ▼
Workflow Runtime
        │
        ▼
Provider Infrastructure
```

These boundaries form an architectural lifecycle. They do not imply that an application developer must call every stage manually.

## AI Application Language and AI Asset Model

The application language expresses business intent by composing reusable AI Assets. The current asset vocabulary includes Project, Library, Package, Workflow, Agent, Prompt, Tool, Knowledge, Memory, Policy, Provider, and Model asset types.

A **Project Asset** is the persisted application root used by the integrated application-operation boundary. Its current public definition identifies an entry Workflow and its owned assets.

A **Workflow Asset** contains declarative workflow-step definitions. Realization later converts accepted declarative definitions into the runtime `Workflow` representation consumed by `IWorkflowRuntime`.

Individual asset types do not necessarily have equivalent runtime behavior. The existence of a declarative asset type should not be read as a claim that every possible language behavior for that asset is implemented.

## Persistence and publication

Canonical AI Asset persistence is separate from runtime execution.

The current persistence path maps declarative assets to canonical AI Asset documents, validates and serializes them, and writes their serialized representation through storage authorities. Publication records definitions in the AI Asset catalog so they can subsequently be resolved.

At a high level:

```text
AI Asset
   │
   ▼
AIAssetDocument
   │
   ▼
Validation / Canonical Serialization
   │
   ▼
Serialized AI Asset Storage
   │
   ▼
Catalog Publication
```

PulseStackAI also contains earlier workflow-specific persistence contracts such as `WorkflowDocument` and workflow stores. Those contracts are not the canonical persisted-application path described here. Current persisted application composition uses the AI Asset persistence, catalog, and graph-loading boundaries.

### Two meanings of "package"

PulseStackAI uses the word **package** in two distinct contexts:

- A **Package Asset** is an AI Asset representing a distribution boundary for related AI Asset definitions.
- A **NuGet package** is a binary distribution artifact used to consume PulseStackAI framework assemblies.

They are separate architectural concepts. Framework NuGet production and distribution do not replace or redefine the Package Asset model.

## Catalog, resolution, and aggregate graph loading

Persistence stores definitions; the catalog makes published definitions resolvable.

For a persisted application, `IAIAssetGraphLoader` accepts a root `AssetDefinitionKey` and loads the aggregate AI Asset graph required by that root. This is the boundary between individually persisted definitions and an accepted application graph suitable for realization.

```text
Project AssetDefinitionKey
        │
        ▼
Catalog / Persistent Resolution
        │
        ▼
IAIAssetGraphLoader
        │
        ▼
AIAssetGraph
```

Graph loading is not workflow execution. It reconstructs the declarative application aggregate that realization consumes.

## Application realization

`IApplicationRealizer` coordinates realization of an accepted `AIAssetGraph` without executing it.

```text
AIAssetGraph
      │
      ▼
IApplicationRealizer
      │
      ▼
RealizedApplication
```

Realization resolves and composes the application into the existing runtime representation. A realized application identifies the Project and entry Workflow required by portable invocation.

Realization is deliberately distinct from both persistence and execution.

## Application operation: the normal persisted-application boundary

For normal execution of a persisted Project application, consumers use `IApplicationOperation` rather than manually reproducing the graph-loading, realization, and invocation sequence.

```csharp
var result = await applicationOperation.ExecuteAsync(
    projectKey,
    request,
    cancellationToken);
```

The public contract accepts the persisted Project's `AssetDefinitionKey`, an `ApplicationInvocationRequest`, and an optional cancellation token, and returns an `ApplicationOperationResult`.

Internally, the current operation coordinates the existing authorities:

```text
Project AssetDefinitionKey
        │
        ▼
IAIAssetGraphLoader
        │
        ▼
IApplicationRealizer
        │
        ▼
IApplicationInvoker
        │
        ▼
ApplicationOperationResult
```

`ApplicationOperationResult` preserves the stage that produced the terminal non-exceptional outcome: graph loading, realization, or invocation.

The operation is coordination, not a second execution engine. Actual workflow execution remains owned by the invocation/runtime path.

## Invocation and workflow runtime

`IApplicationInvoker` accepts a `RealizedApplication` and an `ApplicationInvocationRequest`. The invocation boundary projects that request into runtime execution and hands the realized entry Workflow to the existing workflow runtime.

The workflow runtime contract remains:

```csharp
Task<WorkflowExecutionResult> ExecuteAsync(
    Workflow workflow,
    PipelineContext context,
    CancellationToken cancellationToken = default);
```

`IWorkflowRuntime` owns workflow execution. Step executors interpret the runtime workflow structure, and run steps coordinate agent execution. Provider integrations supply concrete model/provider behavior required by realized agents.

The resulting responsibility chain is therefore:

```text
Application Operation
        │
        ▼
Application Invocation
        │
        ▼
IWorkflowRuntime
        │
        ▼
Workflow Step Execution
        │
        ▼
Agent Runtime
        │
        ▼
Provider Integration
```

## Architectural lifecycle versus consumer API

The complete lifecycle is useful for understanding framework architecture:

```text
Author
  ↓
Persist
  ↓
Publish
  ↓
Resolve
  ↓
Graph Load
  ↓
Realize
  ↓
Invoke
  ↓
Runtime
  ↓
Provider
```

A normal persisted-application consumer, however, enters execution at the integrated operation boundary:

```text
Project AssetDefinitionKey
        │
        ▼
IApplicationOperation.ExecuteAsync(...)
        │
        ▼
ApplicationOperationResult
```

Applications may use lower-level boundaries when they specifically need those contracts, but ordinary execution does not require consumers to duplicate the coordination already owned by `IApplicationOperation`.

## Solution responsibilities

The current architecture is distributed across the framework packages rather than implemented by one monolithic runtime.

- **PulseStack.Abstractions** defines AI Assets and the public persistence, graph-loading, realization, invocation, operation, workflow-runtime, and related contracts.
- **PulseStack.Core** provides foundational asset, persistence, catalog, graph-loading, realization, invocation, and runtime services.
- **PulseStack.Agents** provides agent/workflow execution composition and the integrated application-operation coordination.
- **PulseStack.Tools** provides tool-related framework composition.
- **PulseStack.Providers.*** packages provide concrete provider integrations.

External applications consume the framework through its public packages and composition APIs.

## External reference application

MeridianWorks is the external reference application used to validate the current consumer path. It consumes PulseStackAI through NuGet package references rather than project references and exercises the declarative application lifecycle through persistence, publication, graph loading, realization, and integrated application execution.

MeridianWorks is evidence that the public consumer path works; it is not part of the PulseStackAI runtime and its application source is not duplicated into this architecture document.

## Documentation authority

Different documentation families answer different questions:

- **Architecture** describes boundaries that exist and how they relate.
- **Specifications** describe authoritative language and contract semantics after reconciliation with current contracts.
- **Guides** teach developers how to use those boundaries.
- **Engineering records** preserve the decisions and milestone history that produced the current architecture.
- **External reference applications** provide consumer-level proof of the documented path.

Historical design documents may remain valuable engineering evidence even when they no longer describe the current public architecture.

## Related documentation

This overview establishes the current architecture map. Focused architecture documents cover the AI Asset model, application language, runtime realization, workflow model/runtime, extension points, and solution structure. Some older focused documents still represent earlier architectural generations and are being reconciled against this overview and the current public contracts.

Engineering milestone closures, RFCs, ADRs, and roadmap records remain historical decision authority and should not be interpreted as replacements for this current architecture overview.
