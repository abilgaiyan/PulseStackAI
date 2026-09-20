# Workflow Language Grammar

> **This guide describes the current durable declarative Workflow authoring vocabulary.**

For persisted applications, Workflow authoring must preserve stable identity across process restarts. The canonical durable path therefore uses explicit Workflow-step identities and produces a `WorkflowAsset`.

This guide does not teach the older `Workflow.Create(...)` / `WorkflowBuilder` surface.

## Authoring boundary

The current durable authoring path is:

```text
explicit WorkflowStepId values
        ↓
DurableWorkflowStep.*
        ↓
IdentityCompleteWorkflowStep subtrees
        ↓
IdentityCompleteWorkflowAssetOptions
        ↓
WorkflowAssetFactory
        ↓
WorkflowAsset
```

The result is a declarative Workflow Asset. It is not the runtime `Workflow` consumed by `IWorkflowRuntime`.

## Why explicit identity matters

The base `WorkflowStepDefinition` contract can generate a new `WorkflowStepId` when one is not supplied.

That behavior is not sufficient when an application recreates the same logical persisted definition on a later process run. Re-authoring the definition with new random step identities changes the persisted definition.

The durable authoring contract makes identity explicit:

```text
same logical persisted step
        ↓
same authored WorkflowStepId
        ↓
stable persisted Workflow definition
```

Asset identity and Workflow-step identity are separate. A stable Workflow Asset ID does not by itself make its nested step identities stable.

## Core step vocabulary

`DurableWorkflowStep` exposes the current identity-complete declarative constructs.

### Run

```csharp
var analyze = DurableWorkflowStep.Run(
    analyzeStepId,
    agentReference);
```

A Run definition references an Agent Asset.

### Parallel

```csharp
var checks = DurableWorkflowStep.Parallel(
    parallelStepId,
    "Checks",
    new[]
    {
        policyCheck,
        riskCheck
    });
```

Every child must already be an `IdentityCompleteWorkflowStep`.

### Conditional

```csharp
var decision = DurableWorkflowStep.Conditional(
    conditionalStepId,
    "Approval Decision",
    condition,
    thenStep,
    elseStep);
```

The `thenStep` is required. The `elseStep` is optional. Child steps must be identity-complete before they are attached.

### Retry

```csharp
var retry = DurableWorkflowStep.Retry(
    retryStepId,
    submission,
    maxAttempts: 3,
    name: "Retry Submission");
```

Retry wraps one identity-complete child definition.

### Loop

```csharp
var loop = DurableWorkflowStep.Loop(
    loopStepId,
    items,
    processItem,
    name: "ForEach Item");
```

The item source is a declarative `WorkflowValueDefinition`; the body is an identity-complete step.

### Switch

Create each case from an identity-complete step:

```csharp
var approved = DurableWorkflowStep.SwitchCase(
    "approved",
    approvedStep);

var rejected = DurableWorkflowStep.SwitchCase(
    "rejected",
    rejectedStep);
```

Then author the switch itself:

```csharp
var route = DurableWorkflowStep.Switch(
    switchStepId,
    selector,
    new[]
    {
        approved,
        rejected
    },
    defaultStep,
    name: "Route Decision");
```

The switch owns its explicit `WorkflowStepId`; each case points to a step whose subtree is already identity-complete.

## Recursive identity completeness

The durable API intentionally accepts `IdentityCompleteWorkflowStep` for nested step positions.

That gives the authoring contract a recursive shape:

```text
explicit ID
   ↓
leaf step
   ↓
IdentityCompleteWorkflowStep
   ↓
parent created only from identity-complete children
   ↓
IdentityCompleteWorkflowStep
   ↓
...
   ↓
identity-complete root subtree
```

This is stronger than merely assigning an ID to the root of a composite step.

## Create the Workflow Asset

Once the root step subtrees are identity-complete, place them in `IdentityCompleteWorkflowAssetOptions`:

```csharp
var options = new IdentityCompleteWorkflowAssetOptions
{
    Name = "RFQ Analysis",
    Description = "Analyze an incoming manufacturing RFQ.",
    Steps = new[]
    {
        analyze
    }
};
```

Create the Asset with an explicit stable `AssetId` when the definition is intended to be persisted and recreated:

```csharp
var workflow = workflowFactory.Create(
    workflowAssetId,
    options);
```

The factory converts the identity-complete authoring representation into the normal `WorkflowAssetOptions` / `WorkflowStepDefinition` representation carried by the resulting `WorkflowAsset`.

## Conditions and values

Conditional, Loop, and Switch constructs reference declarative condition/value definitions.

Those definitions are inputs to the Workflow-step definitions; they are not runtime delegates embedded in the durable Workflow Asset.

This keeps the persisted authoring representation separate from the older runtime builder, whose conditions, selectors, and loops can be expressed with runtime-oriented objects or delegates.

## What this grammar produces

The durable grammar produces a declarative tree:

```text
WorkflowAsset
└── WorkflowStepDefinition
    ├── Run
    ├── Conditional
    │   ├── Then
    │   └── Else?
    ├── Parallel
    │   └── Steps[]
    ├── Retry
    │   └── Step
    ├── Loop
    │   └── Step
    └── Switch
        ├── Cases[]
        └── Default?
```

This tree belongs to the Workflow Asset model.

Persistence maps the Workflow Asset through the canonical AI Asset persistence path. Application realization later composes it into the runtime `Workflow` representation.

## Relationship to the older builder grammar

PulseStackAI still contains `WorkflowBuilder` and related grammar builders.

For example, the runtime-oriented surface includes constructs such as:

```text
Workflow.Create(...)
Run(...)
If(...).Then()...End()
Parallel()
ForEach(...)
Switch(...)
Retry(...)
Build()
```

Those APIs are not removed or declared invalid by this guide.

They construct runtime `Workflow` objects and represent an earlier/currently separate authoring surface. They are **not** the canonical guidance for persisted declarative applications.

Do not infer durable persistence identity from the builder grammar.

## Next boundary

After authoring a Workflow Asset, the normal persisted application path continues through the AI Asset platform:

```text
WorkflowAsset
        ↓
Project references Workflow
        ↓
map + persist
        ↓
publish
        ↓
Project AssetDefinitionKey
        ↓
IApplicationOperation
```

For that complete procedure, use [Build and Execute a Declarative Application](../declarative-application.md).

For representation boundaries, see [Workflow Model](../../architecture/workflow-model.md). For execution behavior, see [Workflow Runtime](../../architecture/workflow-runtime.md).

## Scope boundary

This guide owns current durable declarative Workflow authoring grammar.

It does not:

- redefine AI Asset persistence;
- define application realization;
- define `IWorkflowRuntime` execution semantics;
- deprecate or remove the older runtime Workflow builder;
- reconcile the historical `WorkflowDocument` specification;
- define future Workflow language constructs.
