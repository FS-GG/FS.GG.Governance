---
schemaVersion: 1
workId: 366-fsharp-surface-governance
title: "General F# gate for curated signature surfaces, explicit visibility, and API documentation"
stage: specify
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# General F# gate for curated signature surfaces, explicit visibility, and API documentation Specification

Prose status: specified

## User Value
Maintainers can govern curated F# public contracts across libraries and executables without relying on the SDD delivery route.

## Scope
- SB-001: Typed sensing, findings, reusable gate/profile, documentation, and fixtures for non-test F# projects; package baseline checks only where a package or tool-facing contract exists.

## Non-Goals
- SB-002: Do not implement later lifecycle commands or Governance enforcement in this specification.

## User Stories
- US-001 (P1): As a user, I can maintainers can govern curated F# public contracts across libraries and executables without relying on the SDD delivery route.

## Acceptance Scenarios
- AC-001 [US-001] [FR-001]: Given a non-test F# library or executable compiles a source module without a paired signature, when the module is not explicitly internal, generated, an entry point, or covered by a governed exemption, then the gate reports its project and module with a bounded remediation.
- AC-002 [US-001] [FR-002]: Given a paired signature file, when it is not compiled immediately before its implementation or does not describe a compilable source contract, then the gate reports the exact pairing or order violation.
- AC-003 [US-001] [FR-003]: Given a public declaration in a signature, when its contract lacks useful XML documentation, then the gate reports that declaration; documentation on the implementation alone does not clear the finding.
- AC-004 [US-001] [FR-004]: Given a package or tool-facing contract with a committed baseline, when its baseline is stale, then the gate reports the applicable baseline; an executable-only product is not required to create a package baseline.
- AC-005 [US-001] [FR-005]: Given a Rogue3-shaped executable containing public modules and zero signatures, when the reusable profile runs outside an SDD route, then it fails advisory policy with per-module findings and migration guidance.

## Functional Requirements
- FR-001: The gate MUST sense each compiled non-test F# module and require a paired `.fsi` or mechanically verifiable explicit non-public visibility, subject only to generated, entry-point, and governed-exemption handling. (Stories: US-001; Acceptance: AC-001)
- FR-002: The gate MUST report missing signatures, wrong project compile order, public-by-default unpaired modules, and signature/source mismatch without mechanically exposing implementation helpers. (Stories: US-001; Acceptance: AC-002)
- FR-003: The gate MUST evaluate XML documentation on exported `.fsi` declarations and report omissions with a useful remediation route. (Stories: US-001; Acceptance: AC-003)
- FR-004: The gate MUST require and evaluate public-surface baselines only for package or tool-facing contracts, while applying signature and visibility rules to executable projects too. (Stories: US-001; Acceptance: AC-004)
- FR-005: The reusable governance gate and generated guidance MUST be consumable independently of SDD, start advisory, and state a dated, tested path to blocking promotion. (Stories: US-001; Acceptance: AC-005)

## Ambiguities
- AMB-001: The governed exemption configuration shape must fit the existing typed policy/configuration model.
- AMB-002: The first implementation should select the existing sensing and gate seams that deliver the required reusable profile without duplicating package-check behavior.

## Public Or Tool-Facing Impact
- This specification is an SDD lifecycle artifact and command-report contract input.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd clarify --work 366-fsharp-surface-governance`.
