# Implementation Plan: Per-Finding Rule Identity

**Branch**: `068-finding-rule-id` | **Date**: 2026-06-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/068-finding-rule-id/spec.md`

## Summary

Give every emitted finding a **stable, deterministic rule id** that names the rule (or typed gate) responsible
for it, and surface it additively on each per-finding record in `audit.json` (`fsgg ship`), `verify.json`
(`fsgg verify`), and `route.json` (`fsgg route`). The id is derived **at projection time** from data the
projections already hold — the `GateId` of a selected gate, the `FindingId` of a kernel boundary
(unknown-governed-path) finding, the `Domain`+`Code` of a `SurfaceFinding`, the `ReleaseRuleKind` of a release
finding — through one new dependency-free leaf module, `FS.GG.Governance.RuleIdentity`, that defines a single
`RuleId` newtype with a **source-prefixed** wire representation (`gate:…`, `boundary:…`, `surface:…`,
`release:…`, and an honest `unattributed:…` marker). Because every projection already receives the source
value, **no projection function signature changes**: the work is one new leaf module + a new `ruleId` field on
each per-finding object in the three projection writers, plus re-blessed goldens.

The rule id anchors the long-standing **"profiles never alter rule hashes"** guarantee: the id is invariant
across profile and run mode (it is a pure function of the rule's identity, which carries no profile/mode), and
maps to the **existing catalog-wide `RuleHash`** (the rule-pack SHA-256 already sensed for freshness/provenance),
which is itself profile/mode-invariant by construction. The feature invents no new hashing scheme.

This is a **Tier 1** contracted change: the three JSON contracts each gain an additive per-finding `ruleId`
field and a new public module joins the surface. The addition is **purely additive** — existing fields,
ordering, verdicts, exit codes, and `schemaVersion`s are unchanged, and output is **byte-identical to the
pre-feature output for any input that produces no findings** (no finding ⇒ no object ⇒ no `ruleId`).

## Technical Context

**Language/Version**: F# on .NET 10 (`net10.0`), matching the rest of the solution.

**Primary Dependencies** (all in-repo; **no new external/NuGet dependency** — `System.Text.Json` from the
shared framework only):
- **NEW** `FS.GG.Governance.RuleIdentity` — a dependency-free leaf library: `type RuleId = RuleId of string`,
  source-prefixed smart constructors (`gate`/`boundary`/`surface`/`release`/`unattributed`), and
  `ruleIdToken: RuleId -> string`. References only `FSharp.Core` (no upstream governance project), so it cannot
  introduce a cycle and any projection may reference it.
- `FS.GG.Governance.Gates` — `GateId`, `gateIdValue` (the stable `"<domain>:<check>"` string).
- `FS.GG.Governance.Findings` — `FindingId`, `findingIdToken` (the boundary-finding wire token).
- `FS.GG.Governance.SurfaceChecks` — `Model.SurfaceFinding` (`Domain`, `Code`) for verify's `surfaceChecks`.
- `FS.GG.Governance.ReleaseRules` — `ReleaseRuleKind` (+ its kind token) for release findings.
- `FS.GG.Governance.Ship` — `EnforcedItem`/`EnforcedItemId` (`GateItem of GateId | FindingItem of FindingId *
  GovernedPath`), the shape audit.json/verify.json iterate.
- `FS.GG.Governance.AuditJson` / `VerifyJson` / `RouteJson` — the three projections that gain the field.
- `FS.GG.Governance.FreshnessKey` — `RuleHash` (the existing catalog hash; the rule-id → rule-hash anchor).

**Storage**: filesystem only — the existing deterministic `audit.json` / `verify.json` / `route.json` artifacts.
No new persisted artifact, no sidecar. The feature is emit-only and pure.

**Testing**: Expecto, matching the solution. Unit tests for `RuleIdentity` (derivation + token + prefix
distinctness + determinism); projection tests asserting the additive `ruleId` field and field order; re-blessed
byte-identity goldens (empty-case anchors unchanged, finding-bearing goldens re-blessed deliberately); an
all-profiles × all-modes invariance test (SC-002/SC-004) and a cross-surface (`verify` vs `ship`) id-match test
(SC-005). Synthetic inputs, if any, carry `Synthetic` in the test name with a use-site disclosure
(Constitution V).

**Target Platform**: Linux/macOS/Windows CLI (`fsgg ship`/`verify`/`route`), same as the rest of the host suite.

**Project Type**: CLI command hosts over pure projection cores (single project family; `src/` + `tests/`).

**Performance Goals**: Rule-id derivation is `O(1)` string-prefixing per emitted finding inside the existing
projection pass; no new traversal, no sort, no I/O. No perf target beyond "no observable change on the
no-findings path" (no finding ⇒ derivation never runs).

**Constraints**:
- Each affected JSON MUST stay **byte-identical** when there are no findings (no `ruleId` appears; schema version
  unchanged) — FR-007, SC-003.
- The enforcement truth table and the verdict rollup MUST NOT be re-opened — the id is identity metadata only,
  derived after enforcement (Assumptions; FR frames it as additive).
- The id MUST be **profile/mode-invariant** (FR-003) and **message-invariant** (FR-009): it is a pure function of
  the rule's structural identity, never of effective severity, profile, mode, or message text.
- Boundary-source ids MUST be **honest and distinguishable** from catalog-gate ids (FR-008) — enforced by the
  source prefix.
- An unattributable finding MUST surface a disclosed `unattributed:` marker, never an empty/guessed id (FR-010);
  in normal paths this marker is never produced (a test asserts so).
- All derivation is pure — no clock, host, env, or ordering influence (FR-002); no MVU needed (Constitution IV
  does not apply — there is no new I/O or state).

**Scale/Scope**: One new leaf project (`RuleIdentity` + tests), four per-finding writer edits across three
projection modules (audit item, verify item + surfaceChecks element, route finding + selected gate), the
`.fsproj` reference additions, a new surface-drift baseline for `RuleIdentity`, and re-blessed finding-bearing
goldens. Cores, hosts, and projection signatures are otherwise untouched.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation**: PASS. Spec written. The new public surface is
  `RuleIdentity.fsi` (the `RuleId` newtype, constructors, token), drafted/exercised in FSI before `.fs`. Semantic
  tests call the module through its packed/prelude surface and the projections through their existing public
  functions (whose signatures do **not** change). FSI updated before/with implementation.
- **II. Visibility Lives in `.fsi`**: PASS. `RuleIdentity.fsi` is the sole declaration of its surface; the `.fs`
  carries no access modifiers. A new surface-drift baseline is added for `RuleIdentity`; the three projection
  modules' `.fsi` are unchanged (no signature change), so their baselines are untouched.
- **III. Idiomatic Simplicity**: PASS. A single-case newtype + string-prefixing functions + an exhaustive token
  match — the plainest possible shape, mirroring the existing `gateIdValue`/`findingIdToken` precedents. No new
  abstraction, no custom operator, no SRTP/reflection, no new dependency.
- **IV. Elmish/MVU Is the Boundary**: N/A (PASS). The feature adds no I/O, no state, and no multi-step workflow —
  it is pure derivation inside the existing pure projections. No `Model`/`Msg`/`Effect` is warranted; adding MVU
  ceremony here would violate Principle III.
- **V. Test Evidence Is Mandatory**: PASS. Fail-before/pass-after: projection tests assert the `ruleId` field is
  absent before and present (with the right prefixed value) after; the empty-case byte-identity goldens are
  frozen pre-change and must stay identical; the all-profiles × all-modes invariance test fails before the id
  exists and passes after. Synthetic inputs disclosed per V.
- **VI. Observability and Safe Failure**: PASS. FR-010's unattributable case is surfaced as a disclosed
  `unattributed:` marker (honest, distinguishable, never empty), never a silent pass or a guessed id —
  Constitution VI's "missing/malformed input is disclosed, not mis-reported" applied to rule attribution.

**Change Classification**: **Tier 1** (three JSON contracts gain an additive per-finding field; one new public
module joins the surface). No truth-table or verdict change; no projection-signature change.

**Result**: PASS — no violations; Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/068-finding-rule-id/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── finding-rule-id.md   # the additive per-finding `ruleId` contract across the three projections
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── FS.GG.Governance.RuleIdentity/                # NEW leaf project (FSharp.Core only)
│   ├── FS.GG.Governance.RuleIdentity.fsproj      # NEW — packable leaf, no governance ProjectReference
│   ├── RuleIdentity.fsi                           # NEW — RuleId newtype, source constructors, ruleIdToken
│   └── RuleIdentity.fs                            # NEW — pure prefixing + exhaustive token
│
├── FS.GG.Governance.AuditJson/
│   ├── FS.GG.Governance.AuditJson.fsproj         # EDIT — add RuleIdentity ProjectReference
│   └── AuditJson.fs                               # EDIT — writeItem: emit `ruleId` per item (gate & finding)
│
├── FS.GG.Governance.VerifyJson/
│   ├── FS.GG.Governance.VerifyJson.fsproj        # EDIT — add RuleIdentity ProjectReference
│   └── VerifyJson.fs                             # EDIT — writeItem + surfaceChecks element: emit `ruleId`
│
└── FS.GG.Governance.RouteJson/
    ├── FS.GG.Governance.RouteJson.fsproj         # EDIT — add RuleIdentity ProjectReference
    └── RouteJson.fs                              # EDIT — writeFinding + writeSelectedGate: emit `ruleId`

tests/
├── FS.GG.Governance.RuleIdentity.Tests/          # NEW — derivation, prefix distinctness, token, determinism
│   ├── FS.GG.Governance.RuleIdentity.Tests.fsproj
│   ├── RuleIdentityTests.fs
│   └── SurfaceBaselineTests.fs                    # NEW — surface-drift baseline for the new module
├── FS.GG.Governance.AuditJson.Tests/             # EDIT — assert `ruleId` field + order; determinism
├── FS.GG.Governance.VerifyJson.Tests/            # EDIT — assert `ruleId` on items + surfaceChecks; re-bless golden
├── FS.GG.Governance.RouteJson.Tests/             # EDIT — assert `ruleId` on findings + gates
├── FS.GG.Governance.ShipCommand.Tests/           # EDIT — re-bless finding-bearing audit goldens; empty anchor unchanged
├── FS.GG.Governance.VerifyCommand.Tests/         # EDIT — re-bless finding-bearing verify goldens; empty anchor unchanged
├── FS.GG.Governance.RouteCommand.Tests/          # EDIT — re-bless finding-bearing route goldens; empty anchor unchanged
└── (cross-surface + invariance tests land in the projection/command test projects above)

docs/initial-implementation-plan.md               # EDIT — flip the Phase-5 rule-id rows to closed (cite 068)
```

**Structure Decision**: Single-project-family layout (the established repo shape). The only new `src/` project
is the dependency-free `RuleIdentity` leaf; every source value the id is derived from is consumed from its
existing project at the projection edge. No host, core, or projection signature changes.

## Phase 0 — Research

See [research.md](./research.md). It resolves: (D1) the rule-id representation and source prefixes; (D2) where
the id is derived (projection edge, not the cores) and why no signature changes; (D3) the per-object field
placement and the byte-identity/golden strategy; (D4) the rule-id → rule-hash anchor and how FR-004/SC-002 is
satisfied without a new hashing scheme; (D5) profile/mode/message invariance; (D6) the honest `unattributed`
marker for FR-010; (D7) the new leaf project's dependency direction (no cycle). No `NEEDS CLARIFICATION` markers
remain.

## Phase 1 — Design & Contracts

- [data-model.md](./data-model.md) — the new `RuleId` entity, the per-source derivation table, the reused
  source types, and the determinism/invariance invariants.
- [contracts/finding-rule-id.md](./contracts/finding-rule-id.md) — the additive per-finding `ruleId` contract:
  C1 byte-identical-when-no-findings, C2 the field's value grammar + placement per projection, C3
  profile/mode/message invariance, C4 the rule-id → catalog-rule-hash anchor.
- [quickstart.md](./quickstart.md) — runnable validation scenarios mapping to SC-001…SC-006.

## Complexity Tracking

> No Constitution Check violations — section intentionally empty.
