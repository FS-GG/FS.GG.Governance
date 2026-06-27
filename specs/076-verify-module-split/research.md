# Phase 0 Research: Verify god-module split (Phase C)

All NEEDS CLARIFICATION resolved. The one genuinely user-owned decision (split
mechanism) was confirmed via a planning clarification; the rest follow from the
working tree at `00e731e` and the spec.

## Working-tree facts (verified)

- `src/FS.GG.Governance.VerifyCommand/Loop.fs` = **1,009 LOC**; one `module Loop`
  (`[<CompilationRepresentation(ModuleSuffix)>]`) over the whole pipeline.
- `src/FS.GG.Governance.VerifyJson/VerifyJson.fs` = **582 LOC**; one `module
  VerifyJson` with the four entry points + `schemaVersion`.
- Both projects have a **reflective surface-drift test** that renders the WHOLE
  assembly's `GetExportedTypes()` (declared public members, sorted) and asserts
  equality against a committed baseline:
  - `tests/FS.GG.Governance.VerifyJson.Tests/SurfaceDriftTests.fs` →
    `surface/FS.GG.Governance.VerifyJson.surface.txt` (7 lines).
  - `tests/FS.GG.Governance.VerifyCommand.Tests/SurfaceDriftTests.fs` →
    `surface/FS.GG.Governance.VerifyCommand.surface.txt` (640 lines).
  Both re-bless via `BLESS_SURFACE=1 dotnet test`.
- **There are zero `module internal` / `module private` top-level modules in
  `src/`** today — internal modules would be a brand-new repo pattern.
- Goldens: `tests/FS.GG.Governance.VerifyCommand.Tests/goldens/verify-*.json`,
  plus the projection goldens exercised by `VerifyJson.Tests/GoldenTests.fs`.

## Decisions

### D1 — Split mechanism: new PUBLIC seam modules + additively re-blessed baselines

**Decision.** Each extracted seam is a new **public** sibling module (`.fsi` + `.fs`,
compiled before the host/entry module). The two surface-drift baselines are
re-blessed **once, additively** — recording exactly the new modules' curated `.fsi`
types and changing no existing baseline line. The EXISTING `Loop`/`VerifyJson` module
surfaces stay byte-identical.

**Rationale.** This was the user's explicit choice in planning. It maximizes
Constitution Principle II (`.fsi` is the sole, curated declaration of every public
surface) and keeps the seams genuinely separate, openable files (FR-001, SC-006). The
cost — additive public surface — is acknowledged and bounded: each seam's `.fsi`
exposes ONLY the entry points the composing module calls (the `rr*` writers, token
helpers, etc. stay absent ⇒ private). It promotes the feature to **Tier 1**, which
the constitution already accommodates (additive surface + baseline updates + tests).

**Alternatives rejected.**
- **`module internal` sibling files** — keeps `GetExportedTypes()` byte-identical
  (no re-bless), but introduces a never-used `internal` module pattern with no `.fsi`
  curation, weakening Principle II. Rejected by the user in favor of the explicit
  `.fsi`-first surface.
- **Nested private modules in one file** — purest presence/absence visibility and no
  re-bless, but the seams stay in a single physical file, defeating the "open one
  named module without reading the base loop" maintainability win (SC-006). Rejected.

**Spec impact.** FR-004 and US1/US2 surface-drift acceptance scenarios + the
internal-helpers edge case were refined to read "existing module surface
byte-identical; baselines re-blessed additively for the new seam modules." (Edited in
`spec.md` during planning.) The byte-identity gate on **goldens/snapshots**
(FR-005/SC-002) is unchanged and absolute.

### D2 — VerifyCommand host seam mapping (3 folds)

From `Loop.fs` bindings (line refs at `00e731e`):

| New module | Moved bindings | Notes |
|---|---|---|
| `…VerifyCommand.SurfaceFold` | `surfaceBlocks` (404), `foldSurfaceVerdict` (416) | Pure over `Profile` + `SurfaceFinding list` + `ShipDecision`; already host-`Model`-free. Clean lift. |
| `…VerifyCommand.ViewCurrencyFold` | `viewCurrencyBlocks` (433), `foldViewCurrencyVerdict` (437), `viewCurrencyDetail` (451) | Pure over `Profile` + `CE.CurrencyFinding list`. Clean lift. |
| `…VerifyCommand.ReleasePreview` | `previewFrom` (481), `previewOf` (495) | `previewFrom` is domain-typed (decl/sensed/snapshot) → clean. `previewOf` takes the host `Model` → **decompose** (Phase B `baseHeadOf` precedent): the module takes the decomposed fields (`ReleaseDecl`/`ReleaseSensed` + snapshot); a one-line wrapper in `Loop` projects `model` into the call so the module stays host-`Model`-free. |

**What STAYS in `Loop` (FR-001 base pipeline + the MVU boundary):** `parse`, `init`,
`update`, `render`, `exitCode`, `applyExecution`, `kindOf` (re-exported from
`CommandHost` already), `fail`, `exitFromBasis`, `awaitingPersist`, `emptyAcc`,
`verifyPlan`, `tryExecute`, `projectEmpty`, `projectExecuted`, and ALL `Msg` handling.
The folds are *called from* `update`/`projectExecuted`; no `update` case or effect
leaves the host (Principle IV). `reviewMarkOf`/`taintOf` are trivial and stay local.

### D3 — VerifyJson projection seam mapping (4 seams + thin entry)

From `VerifyJson.fs` bindings:

| New module | Moved bindings (line refs) | Curated `.fsi` exposes |
|---|---|---|
| `…VerifyJson.Core` | `verdictToken` (53), `dispositionToken` (62), `writeCauseValue` (72), `writeEnforcement` (90), `writeCache` (104), `writeExecution` (123), `writeItem` (145), `writeSection` (196), `gateItemIds` (212), `writeCurrency` (221), `writeCore` (444) | `writeCore` (the orchestrator the entry calls) + any writer a sibling seam needs; tokens/sub-writers stay private. |
| `…VerifyJson.SurfaceChecks` | `writeSurfaceFinding` (288) | `writeSurfaceFinding`. |
| `…VerifyJson.ReleaseReadiness` | `rrSurfaceValue`…`writeAttestationRef` (317–431), `writeReleaseReadiness` (432) | `writeReleaseReadiness`; the `rr*`/`writePack*` helpers stay private (FR: minimal additive surface). |
| `…VerifyJson.GeneratedViews` | `writeGeneratedView` (528), `writeGeneratedViews` (550) | `writeGeneratedViews`. |
| `module VerifyJson` (thin entry) | `schemaVersion` (47) + `ofVerifyDecision` (495), `ofVerifyDecisionWithSurfaceChecks` (482), `ofVerifyDecisionWithPreview` (507), `ofVerifyDecisionWithGeneratedViews` (565) | **UNCHANGED** `VerifyJson.fsi`. Bodies become thin compositions over the four seam modules, appending writers in the SAME order ⇒ byte-identical JSON. |

**Byte-identity argument.** The four entry points already call these writers in a
fixed order against one `Utf8JsonWriter`; moving a writer to a sibling module and
calling `Core.writeX`/`ReleaseReadiness.writeReleaseReadiness` in the identical order
changes no emitted byte. Determinism/golden tests are the proof per commit.

### D4 — `dispositionToken` / token divergences stay put (FR-009)

VerifyJson's `dispositionToken` (`not-executed`, hyphen) and `verdictToken` already
DIVERGE from the shared `JsonTokens` strings and were kept local in Phase A. They move
*into* `VerifyJson.Core` and stay private there — they are NOT re-unified with
`JsonTokens` (FR-009; the byte-identity gate caught this divergence in Phase A). No
new shared leaf is introduced by Phase C.

### D5 — Compile order & project files

Add each new `.fsi`/`.fs` pair to the project's `<Compile>` list **before** the
host/entry module (F# requires definition-before-use; the seams are leaves the entry
depends on). No `ProjectReference` changes — the seams use types already referenced by
the project. `VerifyJson` stays `IsPackable=true` (the new modules ship in the same
package, additively). `VerifyCommand` stays `IsPackable=false`/Exe.

### D6 — GateRunHost unification ADR verdict: **DEFER** (FR-008)

**Recommendation: DEFER**, recorded as `docs/decisions/0003-gaterunhost-unification.md`.

- **Gate satisfied to *consider* it:** Phase B (`075`) delivered with a byte-identical
  golden diff (CLAUDE.md / roadmap), so the roadmap's precondition for evaluating the
  semi-radical unification is met.
- **Why defer, not pursue:** Phase B's own delivery note already deferred the *smaller*
  `exitCode`+`ExitDecision` host adoption because it is "a six-host public-`Loop.fsi`
  surface cascade + interpreter/Cli/test ripple." A full `GateRunHost` — one
  parameterized host replacing three `executionPlan`/`tryExecute`/projection skeletons
  across route/ship/verify — strictly *contains* that cascade and is larger. Phase C's
  committed scope (the two splits) does **not** require it; forcing it in would couple
  this clarity-dominated phase to a Tier-1 surface cascade across three more hosts,
  against FR-008's "left unchanged unless the ADR elects to."
- **Re-entry condition (documented in the ADR):** revisit when the deferred
  `exitCode`/`ExitDecision` adoption follow-up lands (it pays the same surface cascade),
  or when a fourth gate-run host appears — at which point the unification amortizes.
- **Consequence:** the `route`/`ship`/`verify` host skeletons are **left unchanged**
  by this feature (FR-008 second clause). The ADR is the only Phase-C deliverable for
  US3.

## Consolidated outcome

- Mechanism: **public seam modules, `.fsi`-first, baselines re-blessed additively
  once** (D1) → Tier 1.
- Host: 3 folds extracted (`SurfaceFold`, `ViewCurrencyFold`, `ReleasePreview`); base
  `Loop` shrinks toward Route size; MVU boundary intact (D2).
- Projection: 4 seams (`Core`, `SurfaceChecks`, `ReleaseReadiness`, `GeneratedViews`)
  + unchanged thin `VerifyJson` entry (D3); `dispositionToken` divergence stays local
  (D4).
- One seam per commit (FR-010); goldens/snapshots byte-identical at every commit
  (FR-005); suite green (FR-006, modulo the known Cli pack-timeout flake).
- GateRunHost: **DEFER** via ADR 0003; route/ship/verify untouched (D6).
