# Implementation Plan: Block Stale Generated Views at the Configured Governance Boundary

**Branch**: `070-stale-view-blocking` | **Date**: 2026-06-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/070-stale-view-blocking/spec.md`

## Summary

Let a project **configure** a stale generated-view currency finding to fold into a **blocking** verdict at a
chosen Governance boundary, so `fsgg verify` and `fsgg ship` can **Fail** when a Governance-generated view
(gate metadata, rule catalog, capability docs, skill references, API-surface docs, route projection, baseline)
is out of date relative to its declared sources. The feature closes Phase 7's last open functional row.

It introduces **no new staleness detection, no new severity, and no new truth-table branch**. The staleness
determination already exists: the F057 `fsgg refresh` world decides per-view currency by reusing the F029
`FreshnessKey.matches`/`diff` comparator over the `.fsgg/refresh.yml` `GenerationManifest`, yielding the closed
`RefreshModel.CurrencyStatus` (`Current` / `WouldRegenerate` / `Regenerated` / `StaleUnresolved` /
`NotEvaluated`). Today that determination only runs inside the `fsgg refresh` host and is **advisory** at the
merge boundary. This feature **wires the same determination into the verify/ship edge** and folds a stale-view
finding through the **existing F023 enforcement truth table** (`deriveEffectiveSeverity`) into the **existing
F024 verdict partition** — exactly mirroring the **F067 surface-check precedent** (sense finding → map to
`EnforcementInput` → fold through `deriveEffectiveSeverity` → adjust the `ShipDecision` Verdict/ExitCodeBasis →
ride in an additive JSON detail array) and deliberately contrasting with the **F25 cost-finding floor**
(fixed-`Advisory`, never blocks).

The delivery is one new **pure leaf** `FS.GG.Governance.CurrencyEnforcement` (the analogue of
`SurfaceChecks.Model`): it owns the `CurrencyFinding` vocabulary, the pure `decideCurrency` that re-expresses
the F057 per-view currency decision over `FreshnessKey` **verbatim**, the opt-in `findingsOf` gate, and the
`enforcementInputOf`/`decisionOf` bridge into F023 — adding **no** new severity, mode, profile, maturity value,
or truth-table branch. The maturity dial is an **additive** manifest-level field on the F057
`GenerationManifest` (`CurrencyEnforcement: Maturity option`, default `None`), parsed from a new
`currency-enforcement:` key in `.fsgg/refresh.yml`. Both the `fsgg verify` and `fsgg ship` hosts gain a
`SenseViewCurrency` effect / `ViewCurrencySensed` message / currency-findings model field (mirroring F067's
`SenseSurfaces`/`SurfacesSensed`/`SurfaceFindings`), fold the findings into their decision via a host-local
`foldViewCurrencyVerdict` (the analogue of `foldSurfaceVerdict`), and thread them into an **additive** projection
overload so `verify.json` / `ship.json` / `audit.json` gain a conditional `generatedViews` detail array.

This is a **Tier 1** contracted change. It is **opt-in and default-advisory**: with no `currency-enforcement`
configured, `findingsOf` returns `[]`, no verdict/exit-code basis changes, the detail array is omitted, and
every existing `route.json` / `audit.json` / `verify.json` / `ship.json` golden is **byte-identical** (FR-004,
SC-002). No existing public projection signature changes — every new entry point is an additive overload, and
the closed `EnforcedItemId` / `FindingId` / `Severity` / `Maturity` / `RunMode` / `Profile` cores are reused
verbatim, never reopened.

## Technical Context

**Language/Version**: F# on .NET 10 (`net10.0`), matching the rest of the solution.

**Primary Dependencies** (all in-repo; **no new external/NuGet dependency** — the leaf reuses only existing
pure cores, mirroring how `SurfaceChecks.Model` reuses F023):

- **NEW** `FS.GG.Governance.CurrencyEnforcement` — a **pure, packable leaf**. Defines the `CurrencyFinding`
  vocabulary and the bridge into enforcement. References only:
  - **`FS.GG.Governance.RefreshJson`** (`RefreshModel`: `ViewKind`, `GenerationEntry`, `CurrencyStatus`,
    `ViewDecision`, `viewKindToken`) — the existing currency representation it consumes;
  - **`FS.GG.Governance.FreshnessKey`** (`Model.FreshnessInputs`/`ArtifactHash`/`GeneratorVersion` and the
    ops module's `matches`/`diff`) — the existing comparator it re-expresses **verbatim** to decide currency;
  - **`FS.GG.Governance.Enforcement`** (`Severity`, `RunMode`, `Profile`, `EnforcementInput`,
    `EnforcementDecision`, `deriveEffectiveSeverity`) — the existing truth table it folds through;
  - **`FS.GG.Governance.Config.Model`** (`Maturity`, `InputCategory`) — the existing maturity vocabulary.
  It takes a ProjectReference on **no** command/host/Cli/Ship project, so it stays a leaf and cannot introduce
  a cycle (mirrors `SurfaceChecks.Model → Enforcement` only).
- **EDIT (additive)** `FS.GG.Governance.RefreshJson` — `GenerationManifest` gains
  `CurrencyEnforcement: Maturity option` (default `None`); `RefreshModel` opens `Config.Model` for `Maturity`.
  The new field is **not** projected into `refresh.json` (the projection reads only the fields it already
  renders), so `refresh.json` stays byte-identical.
- **EDIT (additive)** `FS.GG.Governance.RefreshCommand` — `Declaration.parse` reads the manifest-level
  `currency-enforcement:` key into the new field (absent ⇒ `None`). `Declaration.parse`'s **signature is
  unchanged** (only the `.fs` body), so its `.fsi` and surface baseline do not move.
- **EDIT (additive)** `FS.GG.Governance.VerifyJson` and `FS.GG.Governance.AuditJson` — the latter produces
  **both** `ship.json` and `audit.json` via `AuditJson.ofShipDecision` (there is no separate "ship.json
  projection" project). Each gains a **new additive overload** (e.g. `ofVerifyDecisionWith…` /
  `ofShipDecisionWith…` carrying the currency detail). Existing `ofVerifyDecision` / `ofShipDecision` /
  `ofAuditDecision` entry points are **untouched**.
- **EDIT (additive)** `FS.GG.Governance.VerifyCommand` and `FS.GG.Governance.ShipCommand` — each gains a
  `SenseViewCurrency` effect, a `ViewCurrencySensed` message, a `ViewCurrencyFindings` model field, a host-local
  `foldViewCurrencyVerdict`, the edge sensing (parse manifest, read recorded provenance, sense source digests +
  generator version), and the additive projection threading — all mirroring F067's VerifyCommand wiring.
- **REUSED VERBATIM** — `Enforcement.deriveEffectiveSeverity` and its closed levers; `Ship.Model.ShipDecision`
  / `Verdict` / `ExitCodeBasis` and the F024 partition rule; `FreshnessKey.matches`/`diff`; `RefreshModel`'s
  `CurrencyStatus`/`ViewDecision`; `RefreshCommand.Declaration.parse`. **No pure decision core is re-opened.**

**Storage**: filesystem only — the hosts read `.fsgg/refresh.yml`, the generated-view provenance lock, and the
declared sources through the existing F014 `Loader.FileReader` / refresh sensing ports. No new persisted store,
no sidecar, no new artifact file — the stale-view detail rides inside the existing `verify.json` / `ship.json`
/ `audit.json` documents.

**Testing**: Expecto, matching the solution. Pure leaf tests for `CurrencyEnforcement` (`decideCurrency`
reproduces the F057 outcomes over `FreshnessKey`; `findingsOf` gate — `None` ⇒ `[]`, `Current`/`NotEvaluated`
⇒ no finding, stale/unresolved ⇒ one finding each; `enforcementInputOf`/`decisionOf` reuse `deriveEffective`
verbatim; a **truth-table sweep** asserting the currency finding's effective severity matches
`deriveEffectiveSeverity` across every maturity × run mode × profile — SC-003; no-hide totality — SC-004);
projection tests (the `generatedViews` array carries view id + kind + stale cause + base **and** effective
severity, is sorted deterministically, and is **omitted** when empty ⇒ byte-identical — SC-002); pure `Loop`
transition + emitted-effect tests and real-`Interpreter` end-to-end tests for both hosts over the
`tests/golden-fixture/` tree (configured-blocking stale fixture ⇒ Fail + Blocked + named blocker — SC-001;
all-fresh fixture ⇒ no finding, no false positive — SC-006; verify-below-ship ⇒ warning — FR-009/US3); an
**unconfigured byte-identity guard** asserting every existing `route.json` / `audit.json` / `verify.json` /
`ship.json` golden is unchanged (SC-002); and new surface-drift baselines. Synthetic inputs, if any, carry
`Synthetic` in the test name with a use-site disclosure (Constitution V).

**Target Platform**: Linux/macOS/Windows CLI (`fsgg verify`, `fsgg ship`), same as the rest of the host suite.

**Project Type**: CLI command hosts over pure cores (single project family; `src/` + `tests/`).

**Performance Goals**: The currency sense is `O(views × sources)` digest comparison — the same work
`fsgg refresh` already performs, run once at the verify/ship edge. The fold and projection are
`O(findings)` string assembly with one deterministic sort. No perf target beyond "indistinguishable from
`fsgg refresh` on the same repo, plus the existing verify/ship cost."

**Constraints**:

- **Byte-identical when unconfigured** (FR-004, SC-002): with `CurrencyEnforcement = None`, `findingsOf`
  returns `[]`; `foldViewCurrencyVerdict` is identity over `ShipDecision`; the additive projection overload emits
  **no** `generatedViews` field. No verdict, exit-code basis, truth table, or existing artifact moves.
- **Reuse the truth table verbatim** (FR-003, SC-003): the only enforcement call is the existing
  `deriveEffectiveSeverity`. No new `Severity`/`RunMode`/`Profile`/`Maturity` value, no new `EnforcedItemId` or
  `FindingId` case, no new truth-table branch. The currency finding adjusts only `Verdict`/`ExitCodeBasis`
  (mirroring `foldSurfaceVerdict`) and rides in an additive detail array — it never becomes an `EnforcedItem`,
  so the closed partition-member cores stay closed.
- **No-hide** (FR-006, SC-004): when the configured maturity / run mode / profile relaxes a finding at the
  active boundary, it still appears in `generatedViews` as a **warning** carrying both base and effective
  severity, never dropped; relaxing it never changes the underlying `CurrencyStatus`.
- **Never fabricate currency** (FR-008): a view whose currency cannot be determined is the existing
  `StaleUnresolved of reason` — surfaced as a finding (and/or a host diagnostic), never coerced to `Current`
  and never silently passed; an operational sense failure surfaces through the host exit code/diagnostics, not
  as an "all current" pass.
- **Verify cannot escalate to the gate verdict** (FR-009): the verify host stays at `RunMode.Verify`; a
  finding configured `block-on-ship`/`block-on-release` derives `Advisory` under verify (a visible warning) and
  blocks only at the configured higher boundary `fsgg ship` reaches.
- **Purely additive** (FR-010): no existing public projection signature changes; every new entry point is an
  additive overload; the new config field and JSON detail are additive (existing fields neither removed nor
  reordered); the finding participates in the existing verdict and exit-code basis, not a parallel verdict.
- **MVU boundary** (Principle IV): all currency sensing (manifest read, lock read, source digest, generator
  version sense) lives in the edge `Interpreter`; the pure `update`/leaf perform no I/O, no clock, no git.

**Scale/Scope**: one new `src/` leaf (`CurrencyEnforcement`) + its one test project; additive edits to
`RefreshJson`, `RefreshCommand`, `VerifyJson`, `AuditJson`, the ship-json projection, `VerifyCommand`, and
`ShipCommand`; one new surface baseline (`surface/FS.GG.Governance.CurrencyEnforcement.surface.txt`) plus
additive updates to the `RefreshJson`, `VerifyJson`, `AuditJson`, ship-json, `VerifyCommand`, and `ShipCommand`
baselines; new goldens (configured-blocking verify/ship) and an unconfigured byte-identity guard; the `.sln`
additions; and a docs flip of the Phase-7 stale-view-blocking row. The F023 enforcement core, the F024 verdict
partition, and every existing projection entry point are untouched.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation**: PASS. Spec written. The new public surface —
  `CurrencyEnforcement.fsi` (the `CurrencyFinding` model, `decideCurrency`, `findingsOf`, `enforcementInputOf`,
  `decisionOf`) and the additive projection overloads and host `Loop`/`Interpreter` additions — is drafted and
  exercised in FSI (`scripts/prelude.fsx`) before any `.fs` body. Semantic tests call the leaf through its
  packed surface and the hosts through `parse`/`init`/`update` and the real interpreter. **No existing public
  signature changes** — every new projection entry point is an additive overload.
- **II. Visibility Lives in `.fsi`**: PASS. The new leaf's `.fsi` is the sole declaration of its surface; the
  `.fs` carries no access modifiers. One new surface-drift baseline is added; the `RefreshJson` / `VerifyJson`
  / `AuditJson` / ship-json / `VerifyCommand` / `ShipCommand` baselines move **additively** (a new manifest
  field, new overloads, new effect/msg/field cases) — no existing binding is removed or its signature changed.
- **III. Idiomatic Simplicity**: PASS. The leaf is pure total functions over closed DUs with exhaustive,
  wildcard-free token matches (the `viewKindToken`/`severityToken` precedent). The host wiring is a verbatim
  copy of the F067 `SenseSurfaces`/`foldSurfaceVerdict` triple. No new abstraction, operator, SRTP, reflection,
  active pattern, or dependency. The one deliberate reuse note: `decideCurrency` re-expresses RefreshCommand's
  **private** per-view currency helper as a shared pure function over `FreshnessKey.matches`/`diff` — the same
  comparator, surfaced once in the leaf rather than duplicated across two hosts.
- **IV. Elmish/MVU Is the Boundary**: PASS. Currency sensing is real I/O (manifest read, provenance-lock read,
  source digesting, generator-version sense) and multi-step, so it MUST go through MVU — and it does, mirroring
  F067 exactly: the pure `update` emits a `SenseViewCurrency` effect; the edge interpreter executes it and feeds
  `ViewCurrencySensed` back. The `CurrencyEnforcement` leaf is pure total functions and correctly carries **no**
  MVU ceremony (adding it would violate Principle III).
- **V. Test Evidence Is Mandatory**: PASS. Fail-before/pass-after: the truth-table sweep fails before
  `decisionOf` exists; a configured-blocking E2E fails while the finding is advisory and passes once folded; an
  unconfigured byte-identity guard freezes the existing goldens pre-change. Real evidence preferred — the host
  interpreters run against the real `tests/golden-fixture/` repo tree with real stale/fresh generated views.
  Synthetic inputs disclosed per V.
- **VI. Observability and Safe Failure**: PASS. The feature's reason to exist is safe-failure honesty:
  `StaleUnresolved` is surfaced and never coerced to `Current` (FR-008); an operational sense failure surfaces
  through the host exit code/diagnostics, not as a fabricated "all current" pass; a relaxed finding stays a
  visible warning (FR-006). Diagnostics distinguish absent/bad input (missing manifest entry, unreadable lock)
  from a tool defect (Principle VI).

**Change Classification**: **Tier 1** (one new packable leaf + additive config field + additive projection
overloads + additive host wiring). No truth-table or verdict-partition change; no existing projection-signature
change.

**Result**: PASS — no violations; Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/070-stale-view-blocking/
├── plan.md              # This file
├── research.md          # Phase 0 output — the decisions (D1–D8)
├── data-model.md        # Phase 1 output — the CurrencyFinding model + reused cores
├── quickstart.md        # Phase 1 output — runnable SC-001…SC-006 validation
├── contracts/
│   └── currency-enforcement.md  # Phase 1 output — config + fold + generatedViews wire contract
├── checklists/
│   └── requirements.md  # Authored by /speckit-specify (if present)
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── FS.GG.Governance.CurrencyEnforcement/          # NEW pure, packable leaf (analogue of SurfaceChecks.Model)
│   ├── FS.GG.Governance.CurrencyEnforcement.fsproj # NEW — IsPackable; refs RefreshJson + FreshnessKey + Enforcement + Config
│   ├── CurrencyEnforcement.fsi                      # NEW — CurrencyFinding, StaleCause, decideCurrency,
│   │                                                #        findingsOf, enforcementInputOf, decisionOf, tokens
│   └── CurrencyEnforcement.fs                       # NEW — pure, total, exhaustive token match (no wildcard)
│
├── FS.GG.Governance.RefreshJson/                   # EDIT (additive)
│   ├── RefreshModel.fsi                             # EDIT — GenerationManifest gains CurrencyEnforcement: Maturity option
│   └── RefreshModel.fs                              # EDIT — default None; refresh.json projection unchanged
│
├── FS.GG.Governance.RefreshCommand/                # EDIT (additive, .fs only)
│   └── Declaration.fs                              # EDIT — parse manifest-level `currency-enforcement:` key
│
├── FS.GG.Governance.VerifyJson/                    # EDIT (additive overload carrying currency detail)
├── FS.GG.Governance.AuditJson/                     # EDIT (additive overload; produces BOTH ship.json + audit.json
│                                                    #        via AuditJson.ofShipDecision — no separate ship.json project)
│
├── FS.GG.Governance.VerifyCommand/                 # EDIT — SenseViewCurrency/ViewCurrencySensed/ViewCurrencyFindings,
│   ├── Loop.fsi / Loop.fs                          #        foldViewCurrencyVerdict, additive projection threading
│   └── Interpreter.fsi / Interpreter.fs            #        edge: parse manifest → read recorded → sense → decide
│
└── FS.GG.Governance.ShipCommand/                   # EDIT — same SenseViewCurrency wiring + fold + projection
    ├── Loop.fsi / Loop.fs
    └── Interpreter.fsi / Interpreter.fs

tests/
└── FS.GG.Governance.CurrencyEnforcement.Tests/     # NEW — decideCurrency vs F057, findingsOf gate,
    │                                               #        truth-table sweep, no-hide, surface drift
    ├── FS.GG.Governance.CurrencyEnforcement.Tests.fsproj
    ├── DecideCurrencyTests.fs
    ├── FindingsGateTests.fs
    ├── EnforcementSweepTests.fs
    ├── NoHideTests.fs
    └── SurfaceDriftTests.fs
# plus additive tests in the existing VerifyJson/AuditJson/ship-json/VerifyCommand/ShipCommand test projects:
#   - generatedViews emitted (id/kind/cause/base+effective), sorted, omitted-when-empty
#   - configured-blocking E2E (Fail+Blocked+named blocker), all-fresh (no false positive),
#     verify-below-ship (warning), unconfigured byte-identity guard

surface/
├── FS.GG.Governance.CurrencyEnforcement.surface.txt # NEW baseline
├── FS.GG.Governance.RefreshJson.surface.txt          # EDIT (additive field)
├── FS.GG.Governance.VerifyJson.surface.txt           # EDIT (additive overload)
├── FS.GG.Governance.AuditJson.surface.txt            # EDIT (additive overload; covers ship.json + audit.json)
├── FS.GG.Governance.VerifyCommand.surface.txt        # EDIT (additive effect/msg/field)
└── FS.GG.Governance.ShipCommand.surface.txt          # EDIT (additive effect/msg/field)

FS.GG.Governance.sln                                  # EDIT — add the new leaf + its test project
docs/initial-implementation-plan.md                   # EDIT — flip the Phase-7 stale-view-blocking row to closed (cite 070)
CLAUDE.md                                             # EDIT — point the SPECKIT plan reference to 070
```

**Structure Decision**: Single-project-family layout (the established repo shape), mirroring the **F067
surface-check precedent** exactly: a **pure packable finding-vocabulary leaf** (`CurrencyEnforcement`, the
analogue of `SurfaceChecks.Model`) that owns the finding type and the `enforcementInputOf`/`decisionOf` bridge
into the F023 truth table, plus **host wiring** in `VerifyCommand` and `ShipCommand` that senses the existing
currency determination at its edge, folds it through `deriveEffectiveSeverity`, and threads it into an additive
projection detail array. The leaf references only the pure cores it consumes (`RefreshJson`, `FreshnessKey`,
`Enforcement`, `Config`); the hosts sit on top. No existing core, root, partition, or projection entry point
changes — the dependency direction stays one-way into the pure cores.

## Phase 0 — Research

See [research.md](./research.md). It resolves:

- **D1** — Mirror the **F067 surface-check folding** pattern (not a parallel verdict, not the F25 fixed-floor):
  sense finding → `enforcementInputOf` → `deriveEffectiveSeverity` → `foldViewCurrencyVerdict` adjusts
  Verdict/ExitCodeBasis → additive detail array. Why a finding-vocabulary **leaf** plus host wiring, not a new
  decision core.
- **D2** — The finding **never becomes an `EnforcedItem`**: `EnforcedItemId`/`FindingId` are closed cores;
  reopening them would breach FR-003. The currency finding rides in an additive `generatedViews` detail array
  and adjusts only the verdict, exactly as F067 surface findings ride in `surfaceChecks`.
- **D3** — The **maturity dial home**: an additive manifest-level `CurrencyEnforcement: Maturity option` on the
  F057 `GenerationManifest`, parsed from `currency-enforcement:` in `.fsgg/refresh.yml`. Why co-locate it with
  the view declarations it gates; why `.fsgg/policy.yml` (no view declarations, a second parser) was rejected;
  why manifest-level (one dial, spec's singular "maturity dial") with per-entry override deferred.
- **D4** — The **currency-determination source**: reuse the F029 `FreshnessKey.matches`/`diff` comparator and
  the F057 `Declaration.parse` + provenance-lock read at the verify/ship edge, producing the existing
  `CurrencyStatus`/`ViewDecision` — the same determination `fsgg refresh` makes, not new detection. Why
  `decideCurrency` is surfaced once in the leaf rather than reusing RefreshCommand's **private** helper or
  widening RefreshCommand's surface.
- **D5** — The **opt-in gate**: `findingsOf None = []`; `Current`/`NotEvaluated` ⇒ no finding;
  `WouldRegenerate`/`Regenerated` (drift) and `StaleUnresolved` ⇒ one finding each, `BaseSeverity = Blocking`,
  `Maturity` = the configured dial. Why default-`None` gives byte-identity.
- **D6** — The **no-hide warning** under verify and under a relaxing profile: the finding carries base **and**
  effective severity into `generatedViews`; `decisionOf` reuses `deriveEffectiveSeverity` so `Observe`/`Warn`
  and below-boundary run modes derive `Advisory` (a visible warning) without altering `CurrencyStatus`.
- **D7** — The **additive projection**: a new `ofVerifyDecisionWith…`/`ofShipDecisionWith…`/`ofAuditDecisionWith…`
  overload threads the currency findings+decisions; the existing entry points are untouched; the array is
  **omitted when empty** so existing goldens stay byte-identical.
- **D8** — **Undeterminable & operational failure**: `StaleUnresolved` is a finding (never coerced to
  `Current`); a manifest/lock read failure surfaces through the host exit code/diagnostics as input-unavailable,
  never as a fabricated "all current" pass.

No `NEEDS CLARIFICATION` markers remain.

## Phase 1 — Design & Contracts

- [data-model.md](./data-model.md) — the `CurrencyFinding` / `StaleCause` model, the `decideCurrency`
  mapping from `(GenerationEntry, recorded, sensed)` to `ViewDecision`/`CurrencyStatus` over `FreshnessKey`,
  the `findingsOf` gate, the `enforcementInputOf`/`decisionOf` bridge into F023, and the reused cores
  (`ShipDecision`, `deriveEffectiveSeverity`, `RefreshModel`, `FreshnessKey`). Determinism + no-hide invariants.
- [contracts/currency-enforcement.md](./contracts/currency-enforcement.md) — the contract: C1 the
  `currency-enforcement:` config key + default-`None`; C2 the `foldViewCurrencyVerdict` verdict rule (any
  effective-`Blocking` ⇒ `Fail`/`Blocked`, mirroring `foldSurfaceVerdict`); C3 the additive `generatedViews`
  wire shape (per-view `viewId`/`kind`/`cause`/`baseSeverity`/`effectiveSeverity`), sort order, and the
  omitted-when-empty rule; C4 the no-hide warning grammar; C5 byte-determinism + unconfigured byte-identity;
  C6 additivity (existing artifacts/entry points unchanged).
- [quickstart.md](./quickstart.md) — runnable validation scenarios mapping to SC-001…SC-006.

## Complexity Tracking

> No Constitution Check violations — section intentionally empty.
