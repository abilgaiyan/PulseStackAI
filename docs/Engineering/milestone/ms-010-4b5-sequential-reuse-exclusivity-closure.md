# MS-010.4B.5 — Sequential-Reuse / Exclusivity Closure

Status: **CLOSED / FROZEN — PR CANDIDATE**

## Scope

MS-010.4B.5 establishes provider-scoped sequential invocation ownership for a concrete `RealizedApplication` while preserving the existing workflow runtime as the execution authority.

This capability is coordination only. It does not introduce a new execution engine, persistence boundary, realization aggregate, hosting model, runtime event identity, or public coordination API.

## Authoritative baselines

- Pre-B.5 baseline: `49544767b62d6a3beba968af6d38802701349e38`
- Verified implementation/conformance head: `aa11620d03ef60870abbb0384884ddfc0bc1d855`
- Feature branch: `feature/ms-010-4b5-invocation-ownership`

## Frozen contract reconciliation

### A.1 — Sharing Boundary

A concrete `RealizedApplication` supports sequential reuse and sharing across invokers within its composed service-provider boundary. Independently composed providers are outside the shared coordination domain.

### A.2 — Coordination Authority

One provider-owned coordination authority is shared by all supported invokers in that provider graph. Occupancy is keyed by exact concrete `RealizedApplication` object identity. Distinct realized application objects remain independent. Coordination must not permanently retain application objects.

### A.3 — Contention Semantics

Admission is non-waiting. An idle application admits one invocation. An occupied application rejects a contender immediately. Rejected contenders do not enter `IWorkflowRuntime`, create application execution state, queue, spin, wait, or participate in fairness scheduling.

### A.4 — Contention Representation

After argument validation, already-observable cancellation has precedence over occupancy. If the application is occupied and cancellation does not already own the outcome, `IApplicationInvoker.InvokeAsync` throws `InvalidOperationException` before workflow execution. No `ApplicationInvocationResult` is manufactured for contention.

### A.5 — Weak Association and Release Ownership

The provider authority uses a `ConditionalWeakTable<RealizedApplication, OccupancyCell>`. Cell construction is inert; admission occurs only against the installed cell. Successful acquisition transfers one implementation-private ownership capability bound to the exact acquired cell. Rejected paths receive no release responsibility.

### A.6 — Atomic Admission and Release

Admission uses one atomic Idle-to-Active transition. Release performs a checked Active-to-Idle transition against the retained cell. An impossible release state is an internal coordination invariant violation. Release-invariant reporting cannot replace an invocation exception or cancellation that already owns the public outcome.

### A.7 — Invocation Authority, API, and DI Composition

`IApplicationInvoker` remains the public application invocation capability. `ApplicationInvoker` and the coordination authority are implementation-internal. Both are provider singletons registered through `AddPulseStackAgents()` using `TryAddSingleton` semantics. The invoker coordinates validation, cancellation precedence, admission, execution, result projection, and structured release. `IWorkflowRuntime` remains workflow execution authority.

Secondary release-invariant diagnostics belong to the application invocation completion boundary. They do not use runtime events and do not synthesize an execution ID. Reporter failure is isolated from an already-primary exception or cancellation.

## Implementation slices

- B.5B.1 — Coordination Authority Foundation: **CLOSED / FROZEN**
- B.5B.2 — Application Invoker Coordination: **CLOSED / FROZEN**
- B.5B.3 — Secondary Release-Invariant Diagnostics: **CLOSED / FROZEN**
- B.5B.4 — Provider Composition: **CLOSED / FROZEN**
- B.5B.5 — Exclusivity / Recovery Conformance: **CLOSED / FROZEN**
- B.5B.6 — Release-Invariant / Failure-Precedence Conformance: **CLOSED / FROZEN**
- B.5B.7 — Whole-Feature Closure: **CLOSED / FROZEN — PR CANDIDATE**

## Production footprint

The whole B.5 production delta is confined to:

- `src/PulseStack.Agents/DependencyInjection/AgentServiceCollectionExtensions.cs`
- `src/PulseStack.Core/PulseStack.Core.csproj`
- `src/PulseStack.Core/Runtime/Invocation/Application/ApplicationInvocationCoordinationAuthority.cs`
- `src/PulseStack.Core/Runtime/Invocation/Application/ApplicationInvocationReleaseInvariantDiagnostics.cs`
- `src/PulseStack.Core/Runtime/Invocation/Application/ApplicationInvoker.cs`

`PulseStack.Core.csproj` grants `InternalsVisibleTo` access to `PulseStack.Agents` so provider composition can construct the internal implementation without promoting coordination implementation types into public API.

## Conformance evidence

The verified implementation/conformance head `aa11620d03ef60870abbb0384884ddfc0bc1d855` passed:

- Tests: **1473 / 1473 PASS**
- Failed: **0**
- Skipped: **0**
- Build: **PASS**
- `git diff --check`: **PASS**
- B.5B.6 baseline-to-head diff check: **PASS**
- Working tree: **CLEAN**

Conformance covers:

- exact concrete application identity;
- weak association without permanent retention;
- atomic single admission;
- immediate overlap rejection before workflow runtime entry;
- equivalent but distinct application independence;
- cross-invoker shared-authority exclusivity;
- root/child-scope provider sharing and independent-provider isolation;
- successful sequential reuse;
- recovery after workflow failure, admitted cancellation, and contention;
- many-contender whole-boundary exclusivity;
- impossible release detection;
- normal completion plus corrupt release;
- exception plus corrupt release with exact primary preservation;
- cancellation plus corrupt release with cancellation preservation;
- exactly-once secondary diagnostic reporting;
- reporter-failure isolation for exception and cancellation primaries.

## Whole-feature scope reconciliation

Comparison from `49544767b62d6a3beba968af6d38802701349e38` to `aa11620d03ef60870abbb0384884ddfc0bc1d855` showed the branch ahead by 17 commits with 10 changed files before this closure record.

No changes entered:

- public application invocation contracts;
- `RealizedApplication`;
- application realization contracts or `ApplicationRealizer`;
- workflow or pipeline runtime implementation;
- step executors;
- runtime event contracts or dispatcher;
- persistence, serialization, catalog, resolution, or aggregate graph loading;
- package or library behavior.

There is no synthetic runtime execution identity and no new public DI registration API.

## Closure finding

Contract consistency: **PASS**  
Implementation coverage: **PASS**  
Conformance coverage: **PASS**  
Provider composition: **PASS**  
Failure precedence: **PASS**  
Scope compliance: **PASS**  
Blocking findings: **NONE**

No additional production implementation, public-contract change, architecture decision, or conformance test is required for MS-010.4B.5.

The closure commit is documentation-only. Final PR/merge verification should confirm the closure head contains no changes beyond this record relative to `aa11620d03ef60870abbb0384884ddfc0bc1d855` and that the full regression/build remains green.
