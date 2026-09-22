# Consume PulseStackAI Development Packages

> **PulseStackAI development packages are immutable .NET/NuGet distribution artifacts produced from one exact committed framework source state.**

This guide explains how PulseStackAI produces a verified development package set, publishes that already-produced set to a local NuGet directory feed, and how an external application consumes the exact version it needs.

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

The current development-package lifecycle is:

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

MeridianWorks is evidence that an external repository can use the package boundary. Its repository-specific updater is not a mandatory PulseStackAI API or a required consumer implementation.

## Current boundary and deferred capabilities

This guide describes the current local development-package workflow only.

It does **not** establish or authorize:

- stable `1.0.4` publication;
- NuGet.org or other public publication;
- a remote package-registry architecture;
- CI/CD package publication;
- automatic package-version advancement;
- symbols, `.snupkg`, or SourceLink policy;
- package signing;
- AI Asset Package semantics;
- `WorkflowPackage` reconciliation;
- a mandatory MeridianWorks-style updater;
- declarative application authoring;
- README restructuring.

Those concerns require separate authority if and when they are introduced.
