# MS-010.4 — Portable Application Invocation Closure

## Status

```text
Milestone               MS-010.4 — Portable Application Invocation
Status                  CLOSED / FROZEN
Closure baseline        c2350b5636b5e87eca00d3c90516a291f582e05d
Final verified proof    1473 / 1473 PASS
Failed                  0
Skipped                 0
Build                   PASS
Blocking findings       NONE
```

MS-010.4 establishes the portable application invocation boundary over an already-realized Project application. It coordinates invocation through the existing workflow runtime without introducing a second execution engine, persistence boundary, hosting model, or application aggregate.

## Authoritative Boundary

```text
AIAssetGraph
    ↓
IApplicationRealizer
    ↓
RealizedApplication
    ↓
IApplicationInvoker
    ↓
fresh PipelineContext
    ↓
IWorkflowRuntime
    ↓
ApplicationInvocationResult
```

MS-010.4 begins at `RealizedApplication`. Loading persisted Project identity, constructing an aggregate graph, and realizing that graph remain separate lower/outer capabilities and are not absorbed by the invocation contract.

## Frozen Contract Decisions

MS-010.4A established and froze the following decisions:

1. **Invocation role** — invoke an already-realized Project application through the existing workflow runtime.
2. **Invocable root** — Project is the supported application root.
3. **Realized application** — `RealizedApplication` preserves Project provenance, entry-Workflow provenance, and the executable Workflow.
4. **Invocation request** — request carries input plus an immutable snapshot of invocation items.
5. **Context projection** — every invocation receives a fresh `PipelineContext`; request input initializes both `Input` and `CurrentOutput`, and request items are copied into context items.
6. **Invocation result** — completed workflow execution projects to `ApplicationInvocationResult` with success, final output, steps, Project provenance, and entry-Workflow provenance.
7. **Failure and cancellation** — existing runtime exception and cancellation semantics remain authoritative; invocation coordination does not manufacture a result for failure paths.
8. **Realization reuse** — a realized application may be reused sequentially; each invocation receives fresh execution context while the realized runtime graph may retain its own runtime state according to existing contracts.
9. **Exclusivity** — overlapping invocation of the same concrete `RealizedApplication` instance is unsupported and is rejected immediately; sequential reuse is supported.

## Implementation Closure

### MS-010.4B.1–B.4 — Contracts and Projection

Merged through PR #23 at `20f35a5b1370b329f21c3b23f723424d0609cddd`.

This increment established:

- `ApplicationInvocationRequest`;
- `ApplicationInvocationResult`;
- `IApplicationInvoker`;
- `RealizedApplication` as the realization-success handoff;
- request-to-`PipelineContext` projection;
- workflow-result-to-application-result projection;
- positive contract and projection conformance.

It deliberately did not introduce invocation ownership, provider coordination, or another execution engine.

### MS-010.4B.5 — Sequential Reuse / Exclusivity

Merged through PR #25 at `c2350b5636b5e87eca00d3c90516a291f582e05d`.

B.5 established:

- provider-owned invocation coordination;
- concrete-object-identity ownership using weak association;
- non-waiting atomic admission;
- immediate overlap rejection before workflow-runtime entry;
- checked structured release;
- sequential recovery after success, failure, cancellation, and contention;
- invocation-private secondary release-invariant diagnostics;
- preservation of primary exception/cancellation precedence;
- provider composition for `IApplicationInvoker` and its coordination authority;
- whole-boundary exclusivity and failure-precedence conformance.

The detailed B.5 closure authority is `ms-010-4b5-sequential-reuse-exclusivity-closure.md`.

## MS-010.4C — Whole-Feature Boundary Reconciliation

The post-B.5 read-only reconciliation at `c2350b5636b5e87eca00d3c90516a291f582e05d` found:

```text
D1–D9 reconciliation    PASS
B.1–B.5 reconciliation  PASS

Invocation contracts    COMPLETE
Context projection      COMPLETE
Result projection       COMPLETE
Realization handoff     COMPLETE
Failure semantics       COMPLETE
Cancellation semantics  COMPLETE
Sequential reuse        COMPLETE
Exclusivity             COMPLETE
Provider composition    COMPLETE

Required B.6+           NONE FOUND
Production gap          NONE
Public-contract gap     NONE
Conformance gap         NONE
Architecture gap        NONE
Blocking findings       NONE
```

No additional invocation implementation slice is required to close MS-010.4.

## Failure and Cancellation Authority

Invocation preserves the existing workflow-runtime outcome boundary.

- Argument validation occurs before admission.
- Cancellation already observable at admission takes precedence over occupancy.
- An occupied application is rejected with `InvalidOperationException` before application execution state is created or the workflow runtime is entered.
- Once admitted, success, failure, and cancellation all pass through structured release.
- A release invariant violation becomes the primary invocation failure only when no earlier invocation failure exists.
- If an invocation exception or cancellation is already primary, it remains unchanged and the release invariant is reported only as a secondary invocation-private diagnostic.
- Failure of the secondary reporting mechanism cannot replace the primary exception or cancellation.

No synthetic runtime execution identity is created for invocation coordination diagnostics.

## Provider and Lifetime Boundary

The supported sharing boundary is one composed root service-provider graph.

```text
root IServiceProvider
        ↓
provider-singleton IApplicationInvoker
        ↓
provider-singleton coordination authority
        ↓
all child scopes share the same authority
```

Independent root providers own independent invocation/coordination instances. The coordination authority is implementation-internal; MS-010.4 introduces no public occupancy, lease, admission, or coordination API.

## Explicit Non-Goals

MS-010.4 does not own:

- serialized asset storage or loading;
- persistent catalog publication or exact resolution;
- aggregate declarative graph loading;
- application realization itself;
- workflow execution-engine semantics;
- hosting, background execution, scheduling, or distributed runtime;
- application activation/lifecycle management;
- public coordination or lease abstractions;
- runtime-event identity changes;
- synthetic execution identifiers;
- persistence of invocation results;
- load → realize → invoke orchestration from a persisted Project identity.

## Future Boundary Classification

A future capability may coordinate the complete application operation:

```text
persisted Project identity
        ↓
aggregate graph loading
        ↓
application realization
        ↓
application invocation
        ↓
result
```

That is an outer application-operation/use-case boundary. It requires separate decisions for cross-phase failure algebra, snapshot/reload semantics, realization reuse, lifetime ownership, and provider composition. It is not required for MS-010.4 closure and must not be retroactively absorbed into portable invocation.

## Closure

```text
MS-010.4A
Contract / Repository Inventory
Status                  CLOSED / FROZEN

MS-010.4B
Portable Invocation Implementation
Status                  CLOSED / FROZEN / MERGED

  B.1–B.4               CLOSED / FROZEN / MERGED
  B.5                   CLOSED / FROZEN / MERGED

MS-010.4C
Whole-Feature Closure / Boundary Inventory
Status                  CLOSED / FROZEN

MS-010.4
Portable Application Invocation
Status                  CLOSED / FROZEN

Additional invocation
implementation          NOT REQUIRED
```

The next capability must be selected from a fresh post-MS-010.4 repository/roadmap inventory. Frozen MS-010.4 decisions should not be reopened unless later repository evidence reveals a concrete contradiction.