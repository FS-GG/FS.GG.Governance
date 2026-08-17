---
schemaVersion: 1
workId: 413-adapter-spi-package
title: Adapter Spi Package
stage: clarify
changeTier: tier1
status: clarified
sourceSpec: work/413-adapter-spi-package/spec.md
publicOrToolFacingImpact: true
---

# Adapter Spi Package Clarifications

## Source Specification
- work/413-adapter-spi-package/spec.md

## Clarification Questions
- **CQ-001**: Is the supported SDK a new wrapper package or the existing SPI assembly packaged under its matching identity?
- **CQ-002**: Does package publication authorize any dependency beyond Kernel and FSharp.Core?
- **CQ-003**: Is a source-project consumer an acceptable package qualification fixture?

## Answers
- CQ-001 → package the existing curated SPI assembly directly as `FS.GG.Governance.Adapters.Spi`; do not create a duplicate wrapper contract.
- CQ-002 → no; the SPI remains a pure adapter value/composition layer over Kernel, and qualification inspects the nupkg dependency graph.
- CQ-003 → no; the positive fixture consumes produced nupkgs from an isolated feed in locked mode, and the negative fixture removes or disconnects that package surface.

## Decisions
- **DEC-001** [CQ-001] [FR-001] [FR-005]: Publish the existing SPI project with explicit package identity/version metadata and preserve its curated `.fsi` baseline.
- **DEC-002** [CQ-002] [FR-002] [FR-004]: Retain the single Kernel project dependency and prove the packed dependency group contains Kernel without host/command/I/O Governance assemblies.
- **DEC-003** [CQ-003] [FR-003] [FR-006]: Qualify through a clean package-only consumer with paired detached/reattached negative-control states; a repository `ProjectReference` is not acceptance evidence.

## Accepted Deferrals
- None.

## Remaining Ambiguity
- None. Package identity, purity, dependency, and qualification boundaries are decided above.

## Lifecycle Notes
- The downstream S.I.R.#198 pin/adoption is sequenced after producer merge/public availability and remains outside this work item.
- Next lifecycle action: `fsgg-sdd checklist --work 413-adapter-spi-package`.
