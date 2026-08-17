---
schemaVersion: 1
workId: 413-adapter-spi-package
title: Adapter Spi Package
stage: specify
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# Adapter Spi Package Specification

Prose status: specified

## User Value
Product repositories can author typed Governance adapters through a supported package-only SPI.

## Scope
- SB-001: Publish the existing adapter SPI with its Kernel dependency, prove clean locked external consumption, keep it free of command-host dependencies, and document the supported package setup; do not change adapter semantics or implement the blocked S.I.R. consumer.

## Non-Goals
- SB-002: Do not add new adapter semantics, alter Kernel APIs, or introduce Governance command execution into product runtime code.
- SB-003: Do not direct consumers to a sibling project, DLL `HintPath`, the CLI tool package, or a dotnet-tool installation directory.
- SB-004: Do not implement or pin the blocked S.I.R.#198 consumer in this producer change.

## User Stories
- US-001 (P1): As a product maintainer, I can reference a versioned Governance adapter SDK package and author a closed typed fact model without checking out Governance source.
- US-002 (P1): As a release maintainer, I can verify the SDK package carries exactly its pure Kernel dependency and remains API compatible.

## Acceptance Scenarios
- AC-001 [US-001] [FR-001] [FR-002]: Given only the produced packages in an isolated feed, when a clean external F# consumer restores and builds in locked mode, then it compiles a closed fact union, `Adapter`, reified `Check` values, and fence routing without project or DLL references.
- AC-002 [US-001] [FR-003]: Given that consumer executable, when it evaluates its adapter, then stable render, hash, explanation, verdict, and route-fence projections match the committed expected output.
- AC-003 [US-002] [FR-004] [FR-005]: Given the SPI nupkg, when package evidence inspects its dependency group and contents, then it names the exact Kernel package version, carries the SPI assembly, and contains no command-host or I/O assembly dependency.
- AC-004 [US-002] [FR-006]: Given the package/API gate is subject-mutated by removing or disconnecting the SPI package, when the negative fixture runs, then it fails for the missing or unusable compile-time surface while the restored control passes.
- AC-005 [US-001] [FR-007]: Given the consumer documentation, when a maintainer follows setup, then it uses supported package IDs and versioning only and explains the purity and rollout boundary.

## Functional Requirements
- FR-001: The repository MUST produce an exact-versioned `FS.GG.Governance.Adapters.Spi` package containing the curated public assembly. (Stories: US-001; Acceptance: AC-001)
- FR-002: The SPI package MUST declare an exact dependency on the package-consumable `FS.GG.Governance.Kernel` surface and a clean external consumer MUST restore it transitively in locked mode. (Stories: US-001; Acceptance: AC-001)
- FR-003: The external consumer MUST declare a closed fact union, construct an `Adapter`, author reified `Check` values, evaluate/render/hash/explain them, route fences, and emit one deterministic expected projection. (Stories: US-001; Acceptance: AC-002)
- FR-004: The SPI package dependency graph MUST exclude Governance command-host, command, host, filesystem, process, and other I/O assemblies. (Stories: US-002; Acceptance: AC-003)
- FR-005: Package and public-API compatibility evidence MUST inspect the produced nupkg and maintained surface baseline at the candidate revision. (Stories: US-002; Acceptance: AC-003)
- FR-006: The package gate MUST include a production-shaped negative fixture that fails when the SPI is absent or not transitively usable, plus a passing reattached control. (Stories: US-002; Acceptance: AC-004)
- FR-007: Documentation MUST state supported package IDs, exact-version policy, package-only setup, purity boundary, and producer-before-consumer rollout order without unsupported path-based workarounds. (Stories: US-001; Acceptance: AC-005)

## Ambiguities
No material ambiguities recorded.

## Public Or Tool-Facing Impact
- Adds the supported NuGet package `FS.GG.Governance.Adapters.Spi` while retaining the existing curated F# API surface.
- Adds package qualification/negative-consumer evidence and documents the exact Kernel dependency and absence of command-host dependencies.

## Lifecycle Notes
- Public package contract: surface, tests, docs, version, lock state, release obligation, and downstream rollout must remain coherent.
- Next lifecycle action: `fsgg-sdd clarify --work 413-adapter-spi-package`.
