# Feature Specification: `fsgg verify` Surface-Checks Host Wiring

**Feature Branch**: `067-verify-surface-checks-wiring`

**Created**: 2026-06-26

**Status**: Draft

**Input**: User description: "next backlog item"

## Context

This feature closes the one product-surface thread still open on a command host. The F23/F24
product-surface work delivered three fully-built, fully-tested **pure cores**:

- `ProductSurfaces.classify` — classifies each routed path to its product surface
  (package / docs / skill / design / sampleApp / generatedProduct) and selects a cost tier.
- `SurfaceChecks.Dispatch.Composition.run` — runs every applicable surface-check pack over a sensed
  fact bundle and aggregates the findings order-independently and deterministically.
- `VerifyJson.ofVerifyDecisionWithSurfaceChecks` — the pure, total projection that adds an additive
  `surfaceChecks` array to `verify.json`, byte-identical to `ofVerifyDecision` when the finding list is empty.

`fsgg route` **already wires the classification half** at its host edge (`RouteCommand/Loop.fs`): it classifies
the routed paths and emits a `productSurfaces` section in `route.json`. But **no host yet runs the surface
*checks*** — the surface-check dispatch (`Composition.run` → findings) is built and unit-tested in isolation
and wired into nothing. The parallel wiring on `fsgg verify` was deferred and is tracked as the still-open
tasks **T045 / T048 / T052** in `specs/059-package-docs-skills-design-checks/tasks.md`. Today `fsgg verify`
never classifies surfaces, never runs the checks, and never emits `surfaceChecks` — so a drifted
package/docs/skill/design surface is invisible at the verify boundary. The verify JSON projection **already
carries an empty surface-findings placeholder** (`VerifyJson.ofVerifyDecisionWithPreview … []`), and the
finding-to-enforcement hook (`SurfaceChecks.Model.enforcementInputOf`) already exists — so this feature is
genuinely host-edge wiring of finished cores, making `fsgg verify` the **first** host to run the checks.

This feature wires the existing cores into the `fsgg verify` host so the verify boundary acts on
product-surface findings with the same discipline route already applies. It adds **no** new pure core, **no**
new JSON contract (`verify.json` stays at its current schema version, the `surfaceChecks` section is the
already-specified F24 shape), and **no** enforcement-truth-table change.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Drifted product surface blocks `fsgg verify` (Priority: P1)

A developer runs `fsgg verify` on a repository that declares a product surface (e.g. a package whose
baseline drifted). The verify boundary classifies the surface, runs the applicable surface check, records the
resulting finding in `verify.json`, and folds it into the verdict so a blocking surface finding fails the run.

**Why this priority**: This is the missing governance behavior and the entire reason for the feature. Without
it, no host runs the surface checks at all, so a drifted package/docs/skill/design surface never blocks
anything — "verifies clean" can hold while a surface has silently regressed.

**Independent Test**: Run `fsgg verify` on a real temp repo with a package surface whose declared baseline has
drifted; assert `verify.json` gains a `surfaceChecks` entry `package.baseline-drift` with the drift detail and
declared evidence tag, and the exit code reflects the blocking finding at the verify run mode.

**Acceptance Scenarios**:

1. **Given** a repo with a package surface whose baseline drifted, **When** `fsgg verify` runs, **Then**
   `verify.json` contains a `surfaceChecks` entry `package.baseline-drift` carrying the drift detail and the
   declared evidence tag, and the verdict/exit code reflect the blocking finding.
2. **Given** a repo with multiple declared surfaces producing several findings, **When** `fsgg verify` runs,
   **Then** every finding appears in `surfaceChecks` in the deterministic aggregation order (surface id,
   domain, location, code) and re-running over unchanged inputs produces byte-identical output.

---

### User Story 2 - No declared surfaces leaves `verify.json` byte-identical (Priority: P2)

A developer runs `fsgg verify` on a repository that declares no product surfaces (the common case). The output
is byte-identical to what `fsgg verify` produced before this feature — the additive `surfaceChecks` section is
omitted entirely and every existing field is untouched.

**Why this priority**: The additive-and-non-breaking guarantee is what makes this safe to land. Every existing
`verify.json` consumer and golden must be unaffected when no surfaces are declared.

**Independent Test**: Run `fsgg verify` on a real temp repo with no declared product surfaces; assert
`verify.json` is byte-identical to the pre-wiring golden.

**Acceptance Scenarios**:

1. **Given** a repo with no declared product surfaces, **When** `fsgg verify` runs, **Then** `verify.json` is
   byte-identical to the pre-wiring golden (no `surfaceChecks` field, schema version unchanged).
2. **Given** any repo, **When** `fsgg verify` runs, **Then** the persisted `route.json`/`ship.json`/other host
   goldens remain byte-identical (this feature touches only the verify host).

---

### User Story 3 - Advisory surface findings surface without changing the verdict (Priority: P3)

A developer runs `fsgg verify` on a repository whose only surface finding is advisory. The advisory finding
appears in `verify.json` so it is visible, but the exit code is unchanged from a clean run — advisory never
escalates at the verify boundary.

**Why this priority**: Advisory visibility-without-escalation preserves the existing enforcement semantics
(the truth table is not re-opened) while still reporting the finding. It rounds out the behavior but is lower
risk than P1/P2.

**Independent Test**: Run `fsgg verify` on a real temp repo whose only surface finding is advisory; assert the
entry appears with advisory severity and the exit code equals a clean run's exit code.

**Acceptance Scenarios**:

1. **Given** a repo whose only surface finding is advisory, **When** `fsgg verify` runs, **Then** `verify.json`
   contains the entry with advisory severity and the exit code is identical to a clean run.

---

### Edge Cases

- **Sensing failure**: A surface-fact sensor that cannot read its inputs (missing/unreadable file) must
  produce a clear, disclosed signal rather than a fabricated pass or a crash — consistent with how the verify
  host already treats sensing diagnostics.
- **Declared surface, no findings**: A repo that declares surfaces but where every check passes ⇒ the finding
  list is empty ⇒ `verify.json` is byte-identical to the no-surface case (the section is omitted, not emitted
  empty).
- **Mixed advisory + blocking findings**: Both appear in `surfaceChecks`; only the blocking ones affect the
  verdict; ordering stays deterministic regardless of discovery order.
- **Absent package baseline**: A declared package surface with **no committed baseline** ⇒ the existing
  `package.baseline-absent` blocking finding is reported, but verify writes **nothing** to the working tree (the
  read-only port no-ops the baseline write) and runs no transcript — so two consecutive verify runs over the
  same tree yield byte-identical output (no first-run-writes-baseline / second-run-sees-it divergence).
- **Declared transcripts**: A package surface declaring published FSI transcripts ⇒ they are **not executed** at
  verify (no process is spawned); transcript-result enforcement remains at the route/ship boundaries.
- **Synthetic/fake test inputs**: Any synthetic surface input used in tests must be disclosed at its use site
  per the constitution's real-evidence discipline.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `fsgg verify` MUST classify the **declared product surfaces intersected with the verify scope**
  using the existing classification core, with no behavioral divergence from how `fsgg route` classifies the
  same surfaces (a surface absent from the declaration contributes no request and no finding).
- **FR-002**: `fsgg verify` MUST run the applicable surface checks over sensed fact bundles at the host
  interpreter edge (never inside a pure update step) and obtain the resulting findings from the existing
  aggregation core.
- **FR-003**: `fsgg verify` MUST emit the findings as the additive `surfaceChecks` section of `verify.json`,
  using the already-specified F24 shape (per-element: domain, surface id, code, file, detail, severity, input
  state, optional evidence tag, message).
- **FR-004**: When there are no findings, `verify.json` MUST be byte-identical to the pre-wiring output —
  the `surfaceChecks` field omitted and the schema version unchanged.
- **FR-005**: The `surfaceChecks` section MUST be deterministic: stable element ordering, and byte-identical
  output across re-runs over unchanged inputs and across input-discovery reorderings.
- **FR-006**: The `surfaceChecks` section MUST contain no absolute path, timestamp, username, or environment
  value; the `evidenceTag` MUST be emitted only when the surface declared one.
- **FR-007**: `fsgg verify` MUST fold the surface findings into the existing verdict roll-up at the verify run
  mode via the existing effective-severity derivation — a blocking surface finding fails the run; an advisory
  surface finding never changes the exit code. The enforcement truth table MUST NOT be re-opened or altered.
- **FR-008**: This feature MUST add no new pure core, no new JSON contract, and no new enforcement rule; it is
  host-edge wiring of already-built and already-tested cores.
- **FR-009**: This feature MUST change the observable output of no host other than `fsgg verify`; all other
  hosts' goldens MUST remain byte-identical.
- **FR-010**: A surface-fact sensing failure MUST produce a clear, disclosed diagnostic at the verify host
  rather than a silent pass, a fabricated finding, or a crash.
- **FR-011**: On completion, the still-open `059` tasks **T045 / T048 / T052** MUST be marked complete (citing
  this feature) and the roadmap note MUST record the verify surface-checks wiring as closed.
- **FR-012**: Surface sensing at the verify boundary MUST be **read-only and non-executing**: it MUST NOT write
  to the working tree and MUST NOT shell out to a build/script process. Specifically, the **package** domain's
  sensor — which in its default form regenerates-and-**writes** an absent baseline and shells FSI to run
  declared transcripts — MUST be wired through a read-only port at verify: an absent baseline is **reported**
  (the existing `package.baseline-absent` blocking finding) but **never written**, and declared transcripts are
  **not executed** at verify (transcript execution and baseline establishment remain the responsibility of the
  route/ship boundaries). This keeps verify cheap, side-effect-free, and deterministic across re-runs (FR-005,
  SC-004). It introduces no core change — only the choice of port at the host edge (FR-008).

### Key Entities *(include if feature involves data)*

- **Product surface classification**: the mapping of each verify-scope path to its product surface kind and
  cost tier (produced by the existing classification core; not redefined here).
- **Surface finding**: a single product-surface check result — domain, surface id, code, location, base
  severity, input state, optional evidence tag, message — as already modeled by the surface-checks core.
- **`surfaceChecks` section**: the additive, deterministic array of surface findings within `verify.json`,
  emitted only when non-empty.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a repo with a drifted blocking surface, `fsgg verify` reports the finding in `verify.json`
  and exits with the blocking exit code — i.e. the surface check that today runs in no host now runs at the
  verify boundary and is enforced.
- **SC-002**: For a repo with no declared surfaces, `verify.json` is byte-identical to the pre-wiring golden,
  and every other host's golden is byte-identical (100% no-regression on the empty case).
- **SC-003**: For a repo whose only surface finding is advisory, the entry appears and the exit code equals a
  clean run's exit code (advisory never escalates).
- **SC-004**: Re-running `fsgg verify` over unchanged inputs, and over reordered input discovery, yields
  byte-identical `verify.json` (deterministic output) — including the absent-baseline case, because the
  read-only port never mutates the tree between runs (FR-012).
- **SC-005**: The full solution build + test sweep is green, the `059` tasks T045/T048/T052 are marked
  complete, and the roadmap records the verify surface-checks wiring as closed.

## Assumptions

- The pure cores (`ProductSurfaces.classify`, `SurfaceChecks.Dispatch.Composition.run`,
  `VerifyJson.ofVerifyDecisionWithSurfaceChecks`) are stable and already covered by their own tests; this
  feature reuses them verbatim and does not modify them.
- The surface-check domains in scope are the already-implemented packs (package / docs / skill / design), each
  with its own sensor and pure `evaluate`, already unit-tested via the dispatch core; this feature does not add
  new domains or new check packs — it senses their facts and runs them from the verify host for the first time.
- `verify.json` keeps its current schema version; the `surfaceChecks` section is additive and follows the
  F24 contract already drafted under `specs/059-…/contracts/verify-json-surfacechecks.md`.
- The verify scope (the set of paths classified) is the same scope `fsgg verify` already evaluates today; no
  new scoping or sensing surface beyond the surface-fact sensors is introduced.
- The package domain's sensor is wired through a **read-only port** at verify (no baseline write, no transcript
  execution — FR-012); the docs/skill/design sensors are read-only file readers and are wired through their
  real ports unchanged. The surface-sense step is exposed as a host **port** on the verify `Ports` record (wired
  in `realPorts repo`, faked by tests), mirroring the existing `SenseRelease`/`Execute` ports — so the repo root
  and execution port are captured at port-construction time, not carried on the effect.
- Tests run against real cores and the real verify host; only the edge ports (filesystem/fact sensors) are
  faked, with any synthetic input disclosed at its use site per the constitution.
