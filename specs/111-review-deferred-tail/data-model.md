# Phase 1 Data Model: Deferred tail of the 2026-07-02 code review

The "entities" here are the **types that change shape** (Tier-1) and the **duplication home map**
(dedup). No new persisted data; all JSON/config/snapshot output is byte-identical for real inputs.

## Types that change shape (Tier-1)

### E1 — `GateOutcome` / `GateDisposition` reshape (B4)

`FS.GG.Governance.GateRun/Model.fs` + `Model.fsi`

> **Design refinement (adopted at implementation).** The spec sketched a *separate* `GateResult`
> DU with `GateOutcome { GateId; Result }`. Implementation folded the exit/pass payload **directly
> into the `GateDisposition` cases** instead — one type, no parallel vocabulary, and it leaves
> `JsonTokens.dispositionToken`'s public signature (`GateDisposition -> string`) untouched (its body
> changes `Executed` → `Executed _` only — Tier-2, no third surface move). Same B4 outcome
> (illegal state unrepresentable), strictly smaller blast radius.

**Before** (illegal state representable):
```fsharp
type GateDisposition = Executed | Reused | NotExecuted
type GateOutcome =
    { GateId: GateId
      Disposition: GateDisposition
      ExitCode: ExitCode option     // could be None while Disposition = Executed
      Passed: bool option }         // could be None while Disposition = Executed
```

**After** (payload lives on the two cases that have one; illegal state unrepresentable):
```fsharp
type GateDisposition =
    | Executed of exitCode: ExitCode * passed: bool
    | Reused   of exitCode: ExitCode * passed: bool
    | NotExecuted
type GateOutcome = { GateId: GateId; Disposition: GateDisposition }

val isPassing: disposition: GateDisposition -> bool   // Executed/Reused ⇒ passed; NotExecuted ⇒ false
```

- **Invariant gained**: an executed/reused outcome *always* carries an exit code and pass/fail; a
  not-executed outcome carries neither. No `option` to misuse.
- **Projection mapping** (must stay byte-identical): every reader replaces the
  `ExitCode`/`Passed` optional-match with one `match outcome.Disposition` (the disposition token is
  still `dispositionToken outcome.Disposition`):
  - `JsonWriters.fs:55-61`, `VerifyJson/Core.fs:108-114` — emit the same `disposition` token +
    optional `exitCode`/`passed` fields (NotExecuted ⇒ omit, matching today's `None`).
  - `HumanText/ReportView.fs:74-130` — same "executed/reused/not-executed" + passed text.
  - `RefreshCommand/Interpreter.fs:102` — `let (ExitCode code) = …` now binds from the case.
  - `VerifyCommand/Loop.fs:591-607` — construct `Executed(code, passed)` / `NotExecuted` directly.
- **Token divergence note**: `dispositionToken` (camelCase `notExecuted`, `JsonTokens.fs:55-57`)
  and the human `not-executed` (`ReportView.fs`, `VerifyJson/Core.fs:45-47`) **stay divergent** —
  that divergence is ADR-0006-adjacent and out of scope; the reshape must preserve *both*.

### E2 — `commandFor` result (B4)

`FS.GG.Governance.GateRun/Plan.fs:95-121` + `Plan.fsi:37`

**Before**: `commandFor : repoRoot -> tooling -> gate -> GateCommand option` (three `None`s collapse).

**After**:
```fsharp
type NoCommand =
    | NoPrerequisite              // no RequiresCommand prerequisite (Plan.fs:100)
    | UnresolvedCommand of CommandId   // id resolves to no CommandSpec (Plan.fs:106)
    | EmptyCommandLine            // command line lexed to nothing (Plan.fs:109)
val commandFor : repoRoot -> tooling -> gate -> Result<GateCommand, NoCommand>
```

- Callers: `CommandHost.fs:200` (`Plan.commandFor repo tooling gate`) matches `Ok cmd`; the
  RoutePipeline/ShipCommand/VerifyCommand paths that reference `commandFor` handle the `Ok` and may
  surface the `NoCommand` reason in diagnostics (Principle VI — defect vs missing input).

### E3 — `RawSensing.RepoState` (B9)

`FS.GG.Governance.Snapshot/Snapshot.fs:70-80` + `Snapshot.fsi:61-79`

**Before**: `RepoOk: bool` — `assemble`'s false branch hardcodes `NotARepository`.

**After**:
```fsharp
type RepoState = Ok | NotARepository | GitUnavailable
// RawSensing.RepoOk: bool  →  RawSensing.RepoState: RepoState
```
- `assemble` (`Snapshot.fs:212`) branches on `RepoState`: `NotARepository`/`GitUnavailable` each
  emit their matching `DiagnosticId` (`Model.fs:71-73`) with `Range=None`, empty sets,
  `sortDigests raw.Digests`. The `Ok` path is unchanged.
- Interpreter (`Interpreter.fs:186-198`) deletes the hand-rolled record and calls
  `Snapshot.assemble { raw with RepoState = GitUnavailable }`.

### E4 — `ComparisonSample` (B6)

`FS.GG.Governance.Calibration/Model.fs:24-27` + `Model.fsi:44-47`

**Before**: `{ JudgeVerdict; HumanVerdict; Agreement: AgreementClassification }`
**After**: `{ JudgeVerdict; HumanVerdict }` — the unread `Agreement` field removed. `decide`
(`Calibration.fs:29-49`) is untouched (never read it). `AgreementClassification` stays (still used
by `CalibrationEvidence.ObservedAgreement`).

### E5 — `decideMatrix` signature (B7)

`FS.GG.Governance.ValidationMatrix/Matrix.fs:14-27` + `Matrix.fsi:20-24`

**Before**: `decideMatrix (budget) (boundary: MatrixBoundary) (declared)` — `boundary` only `ignore`d.
**After**: `decideMatrix (budget) (declared)` — parameter removed from `.fs` and `.fsi`; every
caller drops the argument. `MatrixPlan` output unchanged (`MatrixBoundary` type itself stays — it is
used elsewhere).

### E6 — `Verdict.combineReasons` export (A6, Tier-1 widen)

`FS.GG.Governance.Kernel/Verdict.fs:30` → add to `Verdict.fsi`; `Route.stakesOf`
(`Route.fs:48-54`) replaces its inlined split/distinct/sort/concat with
`Verdict.combineReasons (tripped |> List.map (fun f -> f.Name))` (exact adaptor in the impl).

## Duplication home map (dedup, Tier-2 except E6)

| Item | Copies (file:line) | Single home | New `.fsproj` edge |
|---|---|---|---|
| A1 `guard`/`drive` | EvidenceCommand `Interpreter.fs:119,143`; Scaffold `:26,164` | `CommandHost.guard`/`drive` (`CommandHost.fs:228,238`) | Scaffold → CommandHost |
| A4 `writeFreshnessKey`/`writePrerequisite` | GatesJson `:42,58` ↔ RouteJson `:61,77` | `JsonWriters` | GatesJson → JsonWriters |
| A4 `writeCacheEligibility` | AuditJson `:99` ↔ RouteJson `:120` | `JsonWriters` | — (both ref it) |
| A4 `writeGeneratedView(s)` | AuditJson `:194,216` ↔ VerifyJson/GeneratedViews `:23,45` | `JsonWriters` | — |
| A4 attestation-ref | ReleaseJson `:280` (option) ↔ VerifyJson/ReleaseReadiness `:133` (non-opt) | `JsonWriters` (takes `option`) | ReleaseJson → JsonWriters |
| A6 `mkFinding` ×4 | Design/Docs/Package/Skill `*.fs:19-25` | SurfaceChecks (domain+maturity params) | — |
| A6 `safe` ×5 | Design/Docs/Package/Skill `Interpreter.fs`; ReleaseFactsSensing `:102` | SurfaceChecks (4); ReleaseFactsSensing **stays local** | — |
| A6 `valuesFor` ×2 | DesignChecks `:91`, SkillChecks `:78` | SurfaceChecks | — |
| A6 `sha256Hex` ×4 | CacheEligibilityCommand/CurrencySensing/FreshnessSensing/RefreshCommand | **conditional** (Kernel iff fence-sound, else stay duplicated) | conditional |
| A6 `buildGate` ×2 | SddHandoff Readiness `:35`, Consumer `:41` | one private `buildGate` in Adapters.SddHandoff | — |

## Elements removed (dead code, Tier-2)

| Item | Removed | Evidence it is dead |
|---|---|---|
| C1a | DocsChecks `exampleFindings` (`DocsChecks.fs:61-70`) + `ExampleOutcome`/`ExampleFact` vocab (`Model.fs`/`Model.fsi`) | `senseDocs` hardcodes `Examples = []` at both returns (`Interpreter.fs:110,139`); no `ExampleFact` is ever built |
| C1b | `VerifyCommand.SurfacesPending` (`Loop.fs:179`, `Loop.fsi:258`) | written at `Loop.fs:337,776,784,898`; read nowhere (repo-wide grep) |

## Cosmetic / docs (Tier-2, no type change)

- **C1g headers** (6): `ReleaseReport/Report.fs:14`, `Gates/Gates.fs:3`, `HumanRender/Capability.fs:2`,
  `CostBudget/Findings.fs:15`, `Findings/Findings.fs:3`, `AttestationJson/AttestationJson.fs:22`.
- **C1g dead opens** (≥3): `VerifyCommand/Interpreter.fs:14`, `ShipCommand/Interpreter.fs:13`,
  `EvidenceCommand/Interpreter.fs:14` (`open System.IO`); optional wider 073 sweep listed in the spec.
- **C2f**: `VerifyCommand.fsproj` — a `<!-- -->` comment documenting the 43-declared/~32-reachable
  full-surface-host convention.
- **C2g**: new `docs/decisions/README.md` indexing 0001–0008, cross-linking `docs/adr/README.md`
  (org ADR-0012/0013 pointers).
