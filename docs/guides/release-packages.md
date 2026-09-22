# Produce PulseStackAI Release Packages

> **Release package identity is authorized by a qualifying Git version tag at the exact clean committed source HEAD. The producer validates that authority; it does not create it.**

This guide covers release-package production only. Remote publication, CI orchestration, GitHub Releases, credentials, SourceLink, symbols, signing, automatic version advancement, and consumer adoption remain outside this boundary.

## Release identity authority

The repository-owned `VersionPrefix` defines the current release version line.

For a `VersionPrefix` of `1.0.4`, a qualifying tag at `HEAD` has this shape:

```text
v1.0.4
v1.0.4-<prerelease>
```

Examples:

```text
v1.0.4
    → PackageVersion 1.0.4

v1.0.4-rc.1
    → PackageVersion 1.0.4-rc.1

v1.0.4-preview.1
    → PackageVersion 1.0.4-preview.1
```

The source commit and `VersionPrefix` do not authorize a release identity by themselves. A branch name, arbitrary `PackageVersion`, or historical/non-version tag is not release authorization.

Release production requires exactly one qualifying release-version tag at the exact source `HEAD`. Multiple qualifying tags are rejected as ambiguous.

## Production

Release production starts from a clean committed repository state whose `HEAD` already carries the qualifying release tag:

```powershell
./scripts/Pack-Packages.ps1 -ProductionKind Release
```

The producer:

1. verifies the repository and clean working tree;
2. resolves the full source commit;
3. evaluates the repository-owned `VersionPrefix`;
4. discovers the qualifying release-version tag at that exact commit;
5. derives `PackageVersion` from the tag by removing the leading `v`;
6. enters the same Release build, test, pack, exact-package-set verification, provenance verification, hashing, and manifest pipeline used for development production.

The producer does not create tags, mutate `VersionPrefix`, or advance versions.

## Manifest

Release production extends the existing `package-production.json` handoff with release identity evidence:

```text
productionKind = release
releaseAuthority
    tagName
sourceCommit
versionPrefix
packageVersion
configuration = Release
packages[]
    id
    version
    fileName
    sha256
```

The existing provenance and hash guarantees remain unchanged.

Development production remains the default:

```powershell
./scripts/Pack-Packages.ps1
```

and continues to derive:

```text
<VersionPrefix>-dev.<full-lowercase-HEAD-SHA>
```

Its evolved manifest records `productionKind = development`.

## Boundary

Package production still ends at a verified staged artifact set plus its production manifest. A publisher must consume those already-produced bytes; it must not rebuild, repack, or convert a development package into a release package.

This release-production slice does not authorize remote publication.
