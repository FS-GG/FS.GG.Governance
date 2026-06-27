# Research: Publish a Populated Reference `.fsgg` Gate Set

**Feature**: `079-reference-gate-set` | **Date**: 2026-06-27

Phase 0 consolidation. All NEEDS CLARIFICATION resolved; each decision records the
rationale and the alternatives rejected.

## D1 — Publish location

**Decision**: Publish the reference gate set at `samples/sdd-reference-gate-set/.fsgg/`
(a `.fsgg` directory under a new top-level sibling `samples/sdd-reference-gate-set/`,
next to the existing `samples/FS.GG.Governance.Sample.SddReferenceProvider/`). A
sibling `samples/sdd-reference-gate-set/README.md` carries the adopter-facing
explanation.

**Rationale**: The artifact is a copyable, adopter-facing *sample* (FR-001/FR-009),
not a test fixture. The SDD reference worked-example *code* already lives under
`samples/`, so this is the most discoverable "alongside" home. The downstream P4
Templates overlay copies it unedited (FR-009/SC-005); a `samples/` path reads as
"copy me," whereas `tests/.../fixtures/` and `fixtures/` read as test-only.

**Alternatives rejected**:
- `fixtures/sdd-reference/.fsgg/` (alongside the scaffold-manifest golden) — matches
  the spec's literal "alongside" wording but `fixtures/` connotes test-only and is
  undiscoverable for adopters. (User-confirmed against.)
- `reference/sdd-gate-set/.fsgg/` — a new dedicated top-level dir; rejected as an
  extra root concept when `samples/` already houses the reference provider.

## D2 — File set and schema versions

**Decision**: Publish the **full four-file set**: `.fsgg/project.yml` (`schemaVersion: 1`),
`.fsgg/capabilities.yml` (`schemaVersion: 2`), `.fsgg/policy.yml` (`schemaVersion: 1`),
`.fsgg/tooling.yml` (`schemaVersion: 1`).

**Rationale**: FR-001 requires the *full* project/policy/capabilities/tooling set.
`capabilities.yml` MUST be v2 (v1 is rejected by the loader with migration guidance).
Per the loader, `project.yml`+`capabilities.yml` are required; `policy.yml`+`tooling.yml`
are optional but both are needed here (policy declares the `light` default + the stricter
profiles the guard switches to; tooling declares the commands the checks bind to).

## D3 — Domains and path-map

**Decision**: Three domains — `build`, `test`, `evidence` — each reachable by ≥1
path-map glob shaped to the SDD reference skeleton (`<App>.sln`, `src/<App>/`,
`tests/<App>.Tests/`):

| glob          | capability |
|---------------|-----------|
| `src/**`      | build     |
| `*.sln`       | build     |
| `tests/**`    | test      |
| `build.fsx`   | evidence  |

**Rationale**: Routing selects *every* gate whose `Domain` equals a routed path's
domain (`Route.select`). One domain per check keeps the "appropriate gate for that
path" story crisp (US1 scenario 3) and guarantees each check is selectable by ≥1
governed path (SC-004). The skeleton has no `build.fsx` on disk, but the path-map may
govern paths the product *may* contain — SC-004 is a property of the path-map, not of
physical files; the guard supplies `build.fsx` as a candidate path.

**Alternatives rejected**: a single `product` domain holding all three checks — simpler
but less illustrative of routing, and makes every change fire every gate.

## D4 — The three checks and their metadata

**Decision** (all `owner: platform`):

| check    | domain   | command         | cost   | environment   | maturity        | tier         |
|----------|----------|-----------------|--------|---------------|-----------------|--------------|
| build    | build    | `dotnet-build`  | medium | local-or-ci   | block-on-ship   | restoreBuild |
| test     | test     | `dotnet-test`   | high   | ci            | block-on-ship   | focusedTests |
| evidence | evidence | `build-evidence`| medium | local-or-ci   | warn            | (omitted)    |

**Rationale**: build/test carry `block-on-ship` so the gate set *can* block (SC-006);
`evidence` carries `warn` so it is always advisory — the "evidence not yet present /
not-yet-satisfied" first-touch posture (US3 scenario 2, FR-003). All commands are
declared in `tooling.yml` (no dangling refs, FR-004). The `tier` attribute is the F23
production-shaped metadata; omitted on `evidence` (it is not one of the cost tiers).

## D5 — Non-blocking-by-default reconciliation (THE pivotal decision)

The enforcement core (`Enforcement.deriveEffectiveSeverity`) decides blocking from
`(BaseSeverity, Maturity, RunMode, Profile)`:
- `Observe`/`Warn` maturity → **always Advisory**, regardless of mode/profile.
- A base-`Blocking` finding blocks iff `runModeOrdinal(Mode) >= effectiveFloor`, where
  `effectiveFloor = clamp 0 5 (maturityFloor - profileTighten)`.
- `BlockOnShip` floor = **4**; `Light` tighten = **0**, `Standard` = 0, `Strict` = **1**,
  `Release` = 2. RunMode ordinals: `Sandbox 0, Inner 1, Focused 2, Verify 3, Gate 4,
  Release 5`.

**Decision**: The reference is non-blocking by default because its *blocking-capable*
checks (`block-on-ship`) only escalate at `Gate`/`Release` modes under `Light`, while
its `evidence` check (`warn`) is advisory everywhere. The regression guard exercises
the enforcement core at **`RunMode = Verify` (ordinal 3)** — the everyday inner/verify
loop — to demonstrate both directions on the **same** failing change:

- **Under `Light`** (default): `block-on-ship` → floor 4, `3 >= 4` false → **Advisory**;
  `warn` evidence → **Advisory**. ⇒ 0 blocking outcomes (SC-003, US2-2).
- **Under `Strict`**: `block-on-ship` → floor `4-1 = 3`, `3 >= 3` true → **Blocking**.
  ⇒ ≥1 blocking outcome on the same change (SC-006, US2-3).

`Verify` is the *only* mode where `block-on-ship` is Light-advisory yet Strict-blocking
(at `Focused`/below neither blocks; at `Gate`/above even Light blocks). This is by
design: "`light` is non-blocking on first touch" is the **everyday inner/verify
experience**; the ship/release gate is the deliberate ratchet, and switching the
*profile* to `strict` is the demonstrated ratchet *at the same mode*. This nuance is
recorded so the guard's chosen mode is not mistaken for arbitrary.

**Rationale**: This satisfies SC-003 *and* SC-006 with one populated, realistic gate
set — proving the non-blocking result is a deliberate default, not an inability to
block, without inventing per-class strictness dials (out of scope).

**Alternative rejected**: make all three checks `warn`/`observe` — robustly advisory but
then nothing can *ever* block, failing SC-006 ("gates can block").

## D6 — Regression-guard test home

**Decision**: A new focused test project `tests/FS.GG.Governance.ReferenceGateSet.Tests/`
(xUnit, `IsPackable=false`) referencing `Config`, `Gates`, `Routing`, `Route`,
`Enforcement`, and `Tests.Common`; added to `FS.GG.Governance.sln`.

**Rationale**: The guard (FR-010) must load via the config edge *and* assemble the
registry *and* route *and* derive severity — spanning four upper layers plus
`Enforcement`. No existing single test project references all of these (`Route.Tests`
lacks `Enforcement`; `Config.Tests` is below `Gates`/`Routing`/`Route`). A dedicated
project gives FR-010 a clear owner and matches the repo's one-concern-per-project style.

**Alternative rejected**: extend `Route.Tests` — would force a new `Enforcement`
reference onto it and mix the reference-artifact concern into the routing unit suite.

## D7 — Change classification (tier)

**Decision**: **Tier 2** (internal/additive). The feature adds YAML data artifacts, an
adopter doc, and a test project. It introduces **no new public F# module**, no new
dependency, and changes no existing observable behavior — so **no `.fsi` and no
surface-area baseline changes** (Constitution Change Classification).

**Caveat recorded**: the published `.fsgg` becomes a *de-facto* contract the downstream
P4 overlay copies (FR-009). The FR-010 regression guard is what *freezes* that contract
(non-empty build/test/evidence set, `light` default, no dangling refs) so it cannot rot.
No F# package/API contract changes, so Tier 2 holds.

## D8 — Evidence command shape

**Decision**: Declare the evidence command in `tooling.yml` as
`build-evidence → "dotnet fsi build.fsx -- evidence"` (env `local-or-ci`), representing
the generated product's in-process EvidenceGraph/EvidenceAudit step.

**Rationale**: Per the spec's assumptions, the FAKE-style `build.fsx` lives in the
**generated product**, not in this Governance repo. Governance only *declares* this as a
normal check binding to a declared command. This feature does **not** re-introduce the
deleted Spec Kit evidence-audit extension, DAG validation, merge gates, or
`readiness/synthetic-evidence.json` — confirmed absent from this repo (constitution Sync
Impact Report). The command string is illustrative of the intended shape only.

## D9 — Adopter documentation location

**Decision**: `samples/sdd-reference-gate-set/README.md` (gate-by-gate explanation +
the `light`/ratchet posture, co-located with the artifact for copy-along) plus a short
cross-link from `docs/tutorials/` (sibling to the existing 072 tutorials
`adopter-onboarding.md` / `provider-author.md` / `sdd-governance-handoff.md`).

**Rationale**: FR-011 wants adopter-facing docs explaining each gate and the
non-blocking-by-default (`light`) posture and how to ratchet strictness up. Co-locating
the README with the copyable artifact means a downstream copy carries its own
documentation; the tutorials cross-link makes it discoverable in the onboarding flow.

## D10 — How the guard loads and exercises the reference

**Decision**: The guard resolves the repo root (via `Tests.Common.RepositoryHelpers`),
loads `samples/sdd-reference-gate-set` through `Config.Loader.loadAndValidate`, asserts
`Valid` with an empty diagnostics list (FR-007/SC-002), then:
`Gates.buildRegistry` → assert 3 gates, each with a non-empty command prerequisite where
declared (SC-001/SC-004); `Routing.route` + `Route.select` over candidate paths
`[src/App/Program.fs; tests/App.Tests/Tests.fs; build.fsx]` → assert build/test/evidence
each selected (FR-008/SC-004); then `Enforcement.deriveEffectiveSeverity` per selected
gate at `RunMode.Verify` with `BaseSeverity = Blocking` (the failing-change case) under
`Light` (all Advisory, SC-003) and under `Strict` (≥1 Blocking, SC-006). A `light`-default
assertion and a non-empty-check-set assertion complete FR-010/SC-007.

**Rationale**: Real-evidence test (Constitution Principle V) against the on-disk artifact
through the same public surfaces an adopter/CLI uses — no synthetic facts, no mocked
domain logic. The test fails before the artifact exists and passes after.
