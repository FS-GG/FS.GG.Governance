---
schemaVersion: 1
workId: 413-adapter-spi-package
title: Adapter Spi Package
stage: plan
changeTier: tier1
status: planned
sourceSpec: work/413-adapter-spi-package/spec.md
sourceClarifications: work/413-adapter-spi-package/clarifications.md
sourceChecklist: work/413-adapter-spi-package/checklist.md
publicOrToolFacingImpact: true
---

# Adapter Spi Package Plan

Prose status: planned

## Source Snapshot
- spec: work/413-adapter-spi-package/spec.md sha256:55c57556af9d698c47bb6c423896bf9ffbafcd45c5079bd208c1b99d2ff6cfca schemaVersion:1
- clarifications: work/413-adapter-spi-package/clarifications.md sha256:c394446a2d934f28df14a851746fed37fbf0404daaed326ee3fb43559445f508 schemaVersion:1
- checklist: work/413-adapter-spi-package/checklist.md sha256:34e0b5a36165cc7f44188d448bb06ad39ac9a0659f7797538ee30b4fe27f09a5 schemaVersion:1

## Plan Scope
- Convert the existing SPI project from internal-only output into one independently packable SDK package, preserving its `.fsi` surface and single Kernel dependency.
- Add one bounded qualification route that packs Kernel and SPI, creates an isolated local feed, restores/builds/runs a locked external F# consumer, inspects the nupkg, and runs a paired missing/disconnected negative control.
- Keep package metadata, surface baseline, tests, docs, and release workflow obligations coherent; downstream S.I.R adoption remains sequenced after publication.

## Plan Decisions
- PD-001 [AC-001] [FR-001] [DEC-001] complete: Give `FS.GG.Governance.Adapters.Spi.fsproj` explicit package identity, version, packability, description/tags/readme/icon assets, and retain the existing `.fsi`-first compile order.
- PD-002 [AC-001] [FR-002] [DEC-002] complete: Keep the project reference to Kernel as the sole Governance dependency so `dotnet pack` emits the Kernel package dependency; assert its version and transitive locked restore from the isolated feed.
- PD-003 [AC-002] [FR-003] [DEC-003] complete: Add a standalone F# consumer fixture whose program exercises the real public Adapter, Check, FixedPoint, rendering/hash/explanation, and routing APIs and compares one stable output projection.
- PD-004 [AC-003] [FR-004] [DEC-002] complete: Inspect the SPI nuspec and archive contents, allowing only Kernel/FSharp.Core dependencies and refusing command-host, host, command, process, filesystem, or tool assemblies.
- PD-005 [AC-003] [FR-005] [DEC-001] complete: Extend maintained surface/package evidence so the new packable project is included in normal package validation and its curated `.fsi` contract remains the API source.
- PD-006 [AC-004] [FR-006] [DEC-003] complete: Pair the clean consumer with a subject mutation that removes/disconnects the SPI package and must fail, then restore the exact control and require it to pass.
- PD-007 [AC-005] [FR-007] complete: Update adapter documentation with the package IDs, exact consumer pin, package-only commands, purity limits, and producer-before-S.I.R rollout order.

## Contract Impact
- PC-001 [PD-001] [PD-002] packageSurface: `FS.GG.Governance.Adapters.Spi` becomes a supported NuGet package at the repository baseline version and carries a package dependency on `FS.GG.Governance.Kernel`; no public F# signature is intentionally changed.

## Verification Obligations
- VO-001 [PD-001] [PD-002] [PD-003] [PD-004] [PD-005] [PD-006] [PD-007] [PC-001] packageQualification: Focused qualification must pack both packages once, inspect dependency/content metadata, restore/build/run the external consumer in locked mode from the isolated feed, demonstrate the missing/disconnected subject mutation reds, restore the control green, and run API/package validation for the SPI surface.

## Performance Intent
No performance intent is declared for this work item.

## Migration Posture
- PM-001 [PC-001] additive: Existing source consumers keep the same assembly/API; package consumers adopt the new package only after producer publication, with S.I.R.#198 updating its own lock and adapter in a later consumer change.

## Generated View Impact
- GV-001 [PD-005] packageEvidence: SDD analysis/verify/ship views and committed qualification evidence bind the exact package inputs and candidate revision; no generated file is hand-authored as authority.

## Accepted Deferrals
No accepted plan deferrals recorded.

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- Publishing the package is a post-merge delivery obligation if the normal repository workflow does not already publish this package from the merged revision.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd tasks --work 413-adapter-spi-package`.
