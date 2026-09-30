# Consume PulseStackAI Development Packages

> **PulseStackAI development packages are immutable .NET/NuGet distribution artifacts produced from one exact committed framework source state.**

This guide explains how PulseStackAI produces a verified development package set, publishes that already-produced set to a local NuGet directory feed, and how an external application consumes the exact version it needs.

The R5 development/local-package path remains the foundation. The [release-publication layer](#release-publication-above-the-r5-foundation) below describes the delivered RP-1 through RP-6 capabilities above it; they preserve the development identity and consumer-adoption rules.

It documents **framework distribution**. It does not define AI Asset packaging.

## Keep the three package concepts separate

PulseStackAI currently contains three distinct concepts whose names include "package":

| Concept | Meaning in this guide |
| --- | --- |
| **NuGet package** | A .NET distribution artifact for PulseStackAI assemblies such as `PulseStack.Core` or `PulseStack.Agents`. This is the subject of R5 and this guide. |
| **AI Asset Package** | The declarative `AssetType.Package` concept in the AI Asset model. |
| **WorkflowPackage** | A separate/older workflow-packaging subsystem present in the repository. |

These concepts are not interchangeable.

This guide does not assign NuGet semantics to an AI Asset Package and does not reconcile AI Asset Package with `WorkflowPackage`. Those framework-language and historical-contract questions belong to separate specification work.

## Lifecycle at a glance

The R5 development-package lifecycle is:

```text
clean committed PulseStackAI HEAD
        ↓
Pack-Packages.ps1
        ↓
Release build
        ↓
PulseStack.Tests
        ↓
Release pack
        ↓
exact 10-package staging set
        ↓
metadata / provenance verification
        ↓
SHA-256 calculation
        ↓
package-production.json
        ↓
Publish-LocalPackages.ps1
        ↓
immutable local directory feed
        ↓
consumer-configured NuGet source
        ↓
exact PackageReference version
        ↓
dotnet restore / build
```

Package **production** and package **publication** are separate operations.

`Pack-Packages.ps1` produces and verifies a package set. `Publish-LocalPackages.ps1` accepts an already-produced set through its manifest and publishes those exact artifacts. Publication does not rebuild, repack, reinterpret, or choose a new version for them.

## Current package set

The current package-production boundary contains exactly ten packable projects:

```text
PulseStack.Abstractions
PulseStack.Core
PulseStack.Agents
PulseStack.Tools
PulseStack.Providers.OpenAI
PulseStack.Providers.AzureOpenAI
PulseStack.Providers.Ollama
PulseStack.Providers.Gemini
PulseStack.Providers.Groq
PulseStack.Providers.OpenRouter
```

`PulseStack.Tests` and `PulseStack.Showcase` are explicitly non-packable.

The current production projects target `net10.0`.

This ten-package set is the present development/distribution boundary. It is not a declaration that the taxonomy can never change.

## Development package identity

The repository-owned base version is currently:

```text
VersionPrefix
1.0.4
```

Development package production derives one version for the complete package set:

```text
SourceCommit
<full lowercase 40-character committed HEAD SHA>

PackageVersion
1.0.4-dev.<full-40-character-HEAD-SHA>
```

For example, if the committed source were:

```text
0123456789abcdef0123456789abcdef01234567
```

the development package version would be:

```text
1.0.4-dev.0123456789abcdef0123456789abcdef01234567
```

A development package version therefore identifies one exact committed PulseStackAI source state.

This is the current **development-package identity rule**. It does not authorize publication of stable `1.0.4` and does not define a future stable-release versioning policy.

## Producer and consumer ownership

The package workflow deliberately divides responsibility.

### PulseStackAI owns

PulseStackAI owns:

- package production;
- the exact package-set check;
- source-commit provenance;
- package metadata verification;
- package hashes and the production manifest;
- publication of a verified set into the chosen `FeedPath`;
- refusal to overwrite an already-published destination package.

### The consumer owns

An external application owns:

- admitting and selecting the NuGet source it trusts;
- selecting the PulseStackAI packages it directly uses;
- pinning the exact development package version;
- restoring and building against that version;
- deciding when to adopt a newer PulseStackAI development version.

PulseStackAI's local publisher creates package files in a directory feed. It does not globally configure every consumer's NuGet sources.

## 1. Start from a clean committed PulseStackAI source state

Package production must begin from a clean committed repository state.

`Pack-Packages.ps1` verifies:

- the script is running against the PulseStackAI repository that owns it;
- the Git working tree is clean;
- `HEAD` resolves to a full 40-character Git commit SHA.

If the working tree contains uncommitted changes, package production stops.

This matters because the development package version and embedded repository provenance identify the committed `HEAD`. Producing packages from additional uncommitted source would break that identity.

## 2. Produce the package set

From the PulseStackAI repository, run:

```powershell
./scripts/Pack-Packages.ps1
```

The script derives:

```text
VersionPrefix
    ↓
full HEAD SHA
    ↓
PackageVersion = <VersionPrefix>-dev.<full HEAD SHA>
```

It then executes the production sequence in this order:

```text
dotnet build PulseStackAI.sln
    --configuration Release

        ↓ success

dotnet test tests/PulseStack.Tests/PulseStack.Tests.csproj
    --configuration Release
    --no-build

        ↓ success

dotnet pack PulseStackAI.sln
    --configuration Release
    --no-build
    --output <private staging path>
    -p:PackageVersion=<derived development version>
    -p:RepositoryCommit=<full source SHA>
```

The pack operation therefore consumes the successful Release build rather than rebuilding as part of packing.

## 3. Verify the produced artifacts

Package production does not stop after `dotnet pack`.

The script requires the staging directory to contain exactly the expected ten `.nupkg` files. For every expected package it verifies:

- the exact expected file name;
- package ID;
- the single package version shared by the set;
- repository URL;
- repository type `git`;
- repository commit equal to the source commit;
- a canonical SHA-256 digest.

Unexpected packages cause production to fail.

The source repository metadata verified in each package is:

```text
RepositoryUrl
https://github.com/abilgaiyan/PulseStackAI

RepositoryType
git

RepositoryCommit
<the same full source commit used in PackageVersion>
```

The package set is therefore tied both by version and package metadata to the same source state.

## 4. Use package-production.json as the publication handoff

After package verification succeeds, `Pack-Packages.ps1` writes:

```text
package-production.json
```

beside the staged packages.

The manifest records:

```text
sourceCommit
versionPrefix
packageVersion
configuration

packages[]
    id
    version
    fileName
    sha256
```

Conceptually:

```text
verified package files
        +
package-production.json
        ↓
authorized input to local publication
```

The manifest is the handoff between production and publication. The publication script consumes this produced authority rather than deriving a new package version or rebuilding package metadata.

## 5. Publish the verified set to a local directory feed

Choose a directory to act as the local NuGet feed.

The feed does not need to live inside the PulseStackAI repository. For example:

```text
F:\NuGet\PulseStackAI
```

Publish the produced set by supplying the production manifest and destination:

```powershell
./scripts/Publish-LocalPackages.ps1 `
    -ManifestPath "<staging>/package-production.json" `
    -FeedPath "F:\NuGet\PulseStackAI"
```

The publication script validates the manifest and source package set before the first copy.

Among other checks, it requires:

- a lowercase full 40-character `sourceCommit`;
- `Release` configuration;
- exactly ten manifest package entries;
- exactly ten source `.nupkg` files beside the manifest;
- the expected package IDs in the expected package-set order;
- each manifest package version to equal the manifest `packageVersion`;
- each exact manifest file name to exist;
- each source package SHA-256 to equal the manifest SHA-256;
- no unexpected source package.

Only an admitted package set reaches publication.

## 6. Publication is immutable

Before copying any package, `Publish-LocalPackages.ps1` preflights every destination name.

If any destination package already exists, publication is refused.

```text
destination package absent
        ↓
eligible for publication

destination package already exists
        ↓
publication refused
        ↓
no overwrite
```

This makes the current local development-package publication append-only by package file identity.

After copying, the publisher recalculates every destination SHA-256 and requires it to match the manifest.

The publication operation therefore preserves the produced artifact bytes; it does not rebuild or repack them.

## 7. Configure the NuGet source in the consumer

The consuming repository decides how to admit the directory feed.

A repository-local `nuget.config` can make that source explicit and reproducible. A minimal example is:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add
      key="PulseStackAI Local"
      value="F:\NuGet\PulseStackAI" />
    <add
      key="nuget.org"
      value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

This configuration belongs to the consumer. The exact set and ordering of sources is a consumer policy decision; the example merely shows how a local directory feed can be admitted alongside other required sources.

PulseStackAI's publication script does not modify consumer NuGet configuration.

## 8. Reference only the packages the application directly requires

A consumer should add direct `PackageReference` entries for the PulseStackAI packages its code directly uses.

For an application using the current Core, Agent, and OpenRouter surfaces, that can look like:

```xml
<ItemGroup>
  <PackageReference
      Include="PulseStack.Core"
      Version="1.0.4-dev.0123456789abcdef0123456789abcdef01234567" />
  <PackageReference
      Include="PulseStack.Agents"
      Version="1.0.4-dev.0123456789abcdef0123456789abcdef01234567" />
  <PackageReference
      Include="PulseStack.Providers.OpenRouter"
      Version="1.0.4-dev.0123456789abcdef0123456789abcdef01234567" />
</ItemGroup>
```

Use the **same exact development version** for the directly referenced PulseStackAI package set.

Do not mechanically reference all ten framework packages. PulseStackAI's project relationships produce package dependency relationships, so dependencies of the packages you select can resolve transitively through NuGet.

For example, the current source relationships include:

```text
PulseStack.Agents
    → PulseStack.Abstractions
    → PulseStack.Core

PulseStack.Providers.OpenRouter
    → PulseStack.Core
    → PulseStack.Abstractions
```

The consumer declares what it directly needs; NuGet resolves the package dependency graph.

## 9. Restore and build against the exact version

After configuring the source and direct references, verify the consumer normally:

```powershell
dotnet restore
dotnet build --configuration Release
```

A successful restore/build establishes that the selected consumer source can resolve the requested exact package versions and their dependencies for that consumer project.

When investigating package adoption, the consumer can additionally inspect its resolved dependency graph with standard .NET/NuGet tooling. That verification remains consumer-owned.

## 10. Adopt a newer development package deliberately

A later PulseStackAI commit produces a different development version because the full source commit is part of `PackageVersion`.

```text
PulseStackAI commit A
    ↓
1.0.4-dev.<SHA-A>

PulseStackAI commit B
    ↓
1.0.4-dev.<SHA-B>
```

Publishing the newer set does not replace the older set. The consumer continues to reference its existing exact version until it deliberately changes its `PackageReference` version and verifies restore/build again.

That gives the development workflow an explicit adoption boundary:

```text
producer publishes new immutable version
        ↓
existing consumer remains pinned
        ↓
consumer chooses to adopt
        ↓
PackageReference version changes
        ↓
restore / build verification
```

There is no automatic version advancement in this workflow.

## Package dependency authority

The source projects use `ProjectReference` relationships while developing PulseStackAI itself. Package production translates the packable project graph into NuGet package dependencies.

The source project relationships—not a separately maintained documentation graph—remain the authority.

The guide therefore distinguishes:

```text
PulseStackAI source repository
    ProjectReference graph

external application
    direct PackageReference selection
        +
    transitive NuGet dependency resolution
```

Consumers should not duplicate transitive framework dependencies merely to mirror the complete PulseStackAI package-production set.

## Relationship to application development

Package consumption ends when the external application can restore and build against its chosen exact PulseStackAI version.

Application authoring begins after that boundary.

```text
R5 — framework distribution

produce
    ↓
publish
    ↓
consume exact packages
    ↓
restore / build

---------------- boundary ----------------

R4 — declarative application development

author
    ↓
persist
    ↓
publish AI Asset definitions
    ↓
execute Project
```

The word "publish" appears on both sides, but the operations are unrelated:

- R5 publishes **NuGet package files** to a NuGet directory source.
- R4 publishes **persisted AI Asset definitions** to the PulseStackAI asset catalog.

They must not be conflated.

## MeridianWorks as external-consumer evidence

MeridianWorks provides external evidence for this distribution model. It consumes PulseStackAI through exact development-package versions rather than project references or copied framework binaries, and its package-adoption workflow verifies the resolved framework graph before application execution.

MeridianWorks is evidence that an external repository can use the development-package boundary. This evidence does not prove RP-6 release continuation or recovery. Its repository-specific updater is not a mandatory PulseStackAI API or a required consumer implementation.

## Release publication above the R5 foundation

Release production uses a qualifying version tag at the exact clean committed source HEAD, as described in [Release Package Production](release-packages.md). Development production still uses `<VersionPrefix>-dev.<full HEAD SHA>`. Both paths produce verified artifact bytes and a manifest; publication consumes those bytes without rebuilding or repacking them.

The delivered release-publication capabilities are layered:

| Layer | Responsibility |
| --- | --- |
| RP-1 | Admit the produced release manifest and exact artifact set, validating release authority, package identity, provenance, and hashes before publication. |
| RP-2 | Observe the remote registry through read-only preflight; fresh publication requires the admitted set to be absent. Preflight does not publish packages. |
| RP-3 | Execute publication with durable operation evidence, observe remote byte equivalence, and recover uncertain outcomes through evidence convergence. |
| RP-4 | Admit the exact successor remotely and issue a grant for a new continuation operation at that single package position. |
| RP-5 | Define canonical admitted release identity, bind publication and recovery evidence to it, and project effective per-position and whole-release state. |
| RP-6 | Select the next successor from eligible recovery or effective-release evidence, use fresh admission and single-position publication, and feed normal or recovered evidence back into the same effective-release projection. |

### Canonical identity and durable evidence

The canonical admitted release identity binds the release production kind, source commit, package version, release-authority tag, and ordered package positions with their IDs, versions, and admitted SHA-256 hashes. Its profile and digest identify the same release across separate publication, continuation, and recovery operations. An operation ID identifies an attempt; it is not the release identity.

Durable publication ledgers record operation provenance and package mutation outcomes. Recovery evidence remains bound to the exact operation and package attempt it observes. Normalized claims join those records to the canonical release position without rewriting the original outcome or treating a historical pointer as new authority.

RP-5 reduces evidence at each position to `Satisfied`, `Unsatisfied`, `Blocked`, or `Indeterminate`. The whole-release projection returns `ReleaseComplete`, `ContinuationEligible`, `Blocked`, or `Indeterminate`. Continuation eligibility identifies the first unsatisfied position after a satisfied prefix, with an unsatisfied remainder; conflicts, uncertainty, or out-of-sequence satisfaction prevent ordinary advancement. Projection itself grants no permission to publish.

### One successor requires one fresh admission

RP-6 accepts either an eligible RP-3 recovery-continuation decision or an RP-5 `ContinuationEligible` result as selection evidence. Both select one exact package position in the admitted release. The RP-3 path preserves genuine historical operation provenance. The RP-5 path does not synthesize historical publication provenance.

The selected successor must receive fresh remote admission. Authoritative absence permits grant issuance; an already-present package, including equivalent bytes, does not authorize another publication attempt through this admission path. Conflicting or indeterminate observations cannot issue a grant. The grant binds the release, package position, admitted bytes, target registry, and new continuation operation.

That grant provides **single-position publication authority**. Once its durable evidence is available, effective-release projection can select the next successor. Every successor requires fresh admission and its own grant; selection of a later position never inherits mutation authority from the previous one.

### Recovery converges evidence instead of replaying publication

When an eligible publication attempt has an uncertain outcome or a recoverable identity conflict, recovery observes remote content and records whether its bytes converge with the admitted package. Recovery does not replay the publication request.

Recovered-equivalent evidence converges to the **same canonical effective-release state as normal accepted/equivalent evidence**. The historical mutation result remains intact while converged recovery can satisfy that release position. The unchanged projection can then select the next successor, which still needs fresh remote admission.

The frozen limits remain explicit:

- **No suffix-wide mutation authority:** a continuation grant covers one position, never all remaining packages.
- **No automatic continuation loop:** next-successor selection does not automatically admit or publish the remainder.
- **No historical-ledger reopening:** recovery and projection do not resume or rewrite an old publication operation.
- **No historical-pointer traversal:** recovery uses the exact source operation evidence; historical provenance does not authorize following a chain of earlier operations.

These are repository-owned release capabilities. Their implementation and conformance evidence do not establish that packages have been distributed on NuGet.org or that public general availability has occurred.

## Current boundary and deferred capabilities

The R5 local development-package workflow and the RP-1 through RP-6 release-publication capabilities above are delivered boundaries. Neither establishes a public distribution event or a general release-automation service.

It does **not** establish or authorize:

- an actual stable `1.0.4` distribution or public GA release;
- actual NuGet.org or other public distribution;
- CI/CD package publication;
- automatic package-version advancement;
- symbols, `.snupkg`, or SourceLink policy;
- package signing;
- AI Asset Package semantics;
- `WorkflowPackage` reconciliation;
- a mandatory MeridianWorks-style updater;
- declarative application authoring.

Those concerns require separate authority if and when they are introduced.
