# Phase 0 Research: Deferred tail of the 2026-07-02 code review

Resolves the design questions that made these items "deferred" rather than folded into #82. Each
decision is grounded in the current code (file:line from the pre-plan survey) and the repo's
constitution (Principle I/II surface discipline; the dependency-fence suite as fence arbiter).

## R1 — B4: reshape `GateOutcome` and `commandFor`

**Question**: How to make "`Executed` without an exit code" unrepresentable, and how to give
`commandFor` a typed reason instead of `None`, with the smallest sound surface move?

**Decision**:
- **`GateOutcome` → move the per-disposition payload into the `GateDisposition` cases.** Today
  (`GateRun/Model.fs:14-23`) `GateDisposition = Executed | Reused | NotExecuted` is a bare DU and
  `GateOutcome` carries independent `ExitCode: ExitCode option` / `Passed: bool option`, so
  `{ Disposition = Executed; ExitCode = None; Passed = None }` type-checks. Reshape so the outcome
  is `{ GateId; Result: GateResult }` where `GateResult = Executed of ExitCode * bool | Reused of
  ExitCode * bool | NotExecuted` (exact field set per case decided in data-model.md). This makes
  the illegal combination unrepresentable *by construction* — the constitution's preferred form
  (Principle I: "if the shape is awkward in FSI, it is awkward in production").
- **`commandFor` → return `Result<GateCommand, NoCommand>`** (or a small `CommandResolution` DU)
  where `NoCommand = NoPrerequisite | UnresolvedCommand of CommandId | EmptyCommandLine`, one case
  per current `None` at `Plan.fs:100/106/109`. Callers that only need "is there a command"
  (`CommandHost.fs:200`) pattern-match the `Ok`; callers wanting the reason get it for free.

**Rationale**: Both are exported (`Model.fsi:38-42`, `Plan.fsi:37`), so this is Tier-1 — but the
surface *shrinks the set of legal values* rather than widening it, which is the safe direction.
The JSON/human projections that read `outcome.Disposition`/`.ExitCode`/`.Passed`
(`JsonWriters.fs:55-61`, `VerifyJson/Core.fs:108-114`, `HumanText/ReportView.fs:74-130`,
`RefreshCommand/Interpreter.fs:102`) all become a single match on `GateResult` that emits the
**identical tokens/JSON** — byte-identical output is the acceptance bar (SC-003).

**Alternatives considered**: (a) a smart constructor `mkExecuted` keeping the flat record —
rejected: it does not make the bad state *unrepresentable*, only inconvenient, so a future direct
record literal reopens the hole. (b) leave `commandFor : … option` and add a separate `whyNoCommand`
— rejected: two functions re-deriving the same match invites drift (the very A6 anti-pattern this
feature removes).

**Blast radius**: GateRun consumers — GateExecution, VerifyCommand (`Loop.fs:591-607` builds
outcomes), ShipCommand, RoutePipeline, CommandHost, and the four projections above. All are in-repo;
the surface-drift baseline for `GateRun` moves in lockstep. **PR = US1.**

## R2 — A6: shared homes for the cross-project duplicates + fence review

**Question**: Where does each duplicated helper live so exactly one copy remains *without* a new
unsound dependency edge? The `*Checks` packs are deliberately fenced (each references only
`SurfaceChecks` + `Config`), so the home matters.

**Decision** (home per helper; each validated against
`tests/FS.GG.Governance.DependencyFences.Tests` before it lands):

| Helper | Copies | Home | New edge? |
|---|---|---|---|
| `mkFinding` | DesignChecks/DocsChecks/PackageChecks/SkillChecks (`*.fs:19-25`) | **SurfaceChecks** (parameterize domain + maturity) | **No** — all four already reference SurfaceChecks + Config (where `normalizePath` lives) |
| `safe` | Design/Docs/Package/Skill `Interpreter.fs:~99-187` + ReleaseFactsSensing `:102` | **SurfaceChecks** for the four packs | The 5th (`ReleaseFactsSensing`) does **not** reference SurfaceChecks → **keep its local copy** (re-deferred with rationale) or add a *justified* edge — default: keep local (FR-009) |
| `valuesFor` | DesignChecks `:91`, SkillChecks `:78` | **SurfaceChecks** | No |
| `sha256Hex` | CacheEligibilityCommand/CurrencySensing/FreshnessSensing/RefreshCommand | see below | **Needs a home decision** |
| `stakesOf`↔`combineReasons` | Kernel `Route.fs:48-54` inlines `Verdict.fs:30` | **Kernel** — export `Verdict.combineReasons` via `Verdict.fsi`; `Route.stakesOf` calls it | **No new project**, but a **Tier-1 export** |
| `buildGate` | Adapters.SddHandoff `Readiness.fs:35`, `Consumer.fs:41` | one private `buildGate` inside `Adapters.SddHandoff` | No (same project) |

- **`sha256Hex`**: the four consumers (two Commands, two Sensing) share no hashing home today.
  Options: (a) a tiny new `FS.GG.Governance.Hashing` leaf — **rejected here**: a *new project* for a
  6-line helper is disproportionate to a Low finding and the plan forbids adding a project (Complexity
  Tracking). (b) Kernel — plausible only if all four already sit above Kernel *and* the fence suite
  allows it. **Decision: gate on the fence suite** — if (b) is sound with no fence violation, land it
  in Kernel; otherwise **keep `sha256Hex` duplicated and re-defer on #83 with rationale** (FR-009).
  The adjacent `digestPath` twins in CurrencySensing/RefreshCommand ride along with whatever
  `sha256Hex` does.

**Rationale**: SurfaceChecks is the natural home for the `*Checks` helpers because it already sits
upstream of all four packs — no new edge, domain/maturity become parameters (Principle III:
plainest form). `combineReasons` is a same-module reuse; exporting it is the only intentional
surface widen (justified: it removes a re-spelled sort/dedup pipeline). Anything that would need a
*new* project or an *unsound* edge is explicitly re-deferred rather than forced — the constitution
requires deferrals be "explicit and bounded" and forbids gratuitous projects.

**Alternatives considered**: a single new `Checks.Common` project for all `*Checks` helpers —
rejected: SurfaceChecks already is that shared upstream, so a new project duplicates its role.

**PR split**: A1 (guard/drive) and A4 (JSON writers) are independent of A6 and land as their own
commits inside **US4**; A6's Kernel export + `*Checks` consolidation land together; `sha256Hex` is a
conditional tail. All are **US4**, but reviewable as separate commits.

## R3 — B9: route `GitUnavailable` through `Snapshot.assemble`

**Question**: Can the git-unavailable branch reuse `assemble`, and is that a surface change?

**Decision — yes, and it is Tier-1.** `assemble` (`Snapshot.fs:209`) currently hardcodes
`NotARepository` in its `not raw.RepoOk` branch (`Snapshot.fs:212-227`), so it cannot express
`GitUnavailable`. Replace `RawSensing.RepoOk: bool` (`Snapshot.fs:71`, `Snapshot.fsi:63`) with a
three-state field — `RepoState = Ok | NotARepository | GitUnavailable` — and have `assemble` emit
the matching diagnostic (`NotARepository`/`GitUnavailable`, both already in `DiagnosticId`,
`Model.fs:71-73`) with `Range = None`, empty path sets, `Digests = sortDigests raw.Digests`. The
interpreter's `GitUnavailable` branch (`Interpreter.fs:186-198`) then becomes
`Snapshot.assemble { raw with RepoState = GitUnavailable }`, exactly mirroring the sibling
not-a-work-tree path (`Interpreter.fs:206-210`) — deleting the hand-rolled record and its private
`sortDigests` call.

**Rationale**: One assembler owns *all* digest sorting and diagnostic shaping (Principle VI:
consistent, drift-free diagnostics; Principle IV: pure assembly, interpreter only at the edge).
Output stays byte-identical: same `GitUnavailable` diagnostic id/op/message, same empty sets, same
digest order. The `Snapshot.fsi` `RawSensing`/`assemble` doc-comments and the surface baseline move
in lockstep.

**Alternatives considered**: keep the hand-built record but factor a shared `sortDigests`-only
helper — rejected: it leaves two record-construction sites that can still drift on a new field, the
exact fragility the finding names.

**PR = US2** (with B6/B7).

## R4 — B6: drop vs derive `ComparisonSample.Agreement`

**Question**: Drop the unread per-sample `Agreement` field, or keep it as a derived function?

**Decision — drop the stored field.** The calibration `decide` (`Calibration.fs:29-49`) reads only
the sample *count* and the pre-aggregated `evidence.ObservedAgreement`; no code reads
`sample.Agreement`. Storing a per-sample agreement that nothing consumes is redundant state.
Removing the field from `ComparisonSample` (`Model.fs:24-27`, `Model.fsi:44-47`) is the honest
minimum. If a future feature needs per-sample agreement it can compute it from
`JudgeVerdict`/`HumanVerdict` on demand — a derived function is *not* added now (YAGNI; Principle III).

**Rationale**: Tier-1 surface shrink; the calibration decision is provably unchanged (it never
touched the field). Any test that constructs a `ComparisonSample` drops one field.

**PR = US2.**

## R5 — Test strategy & surface-baseline mechanics

**Question**: How do we prove the "make-illegal-unrepresentable" items (no runtime behaviour to
assert) and manage the five Tier-1 baseline moves?

**Decision**:
- **Compile-fail demonstration** for B4's core claim: an xUnit test file carries a commented
  (or `#if FALSE`-guarded) attempt to build `Executed` without an exit code, with a comment
  asserting "does not compile — see contracts/surface-deltas.md"; the *positive* tests assert the
  new `GateResult` round-trips to byte-identical JSON/human tokens. (F# has no `[<FactWontCompile>]`,
  so the negative is documentation + the type itself, per Principle V "prefer real evidence; the
  type is the evidence here".)
- **RED→GREEN** for `commandFor` (three distinct typed reasons), B9 (git-unavailable snapshot
  byte-identical to an `assemble`-produced one), B6/B7 (decision byte-identical after the
  field/param drop).
- **Byte-identical-output assertions** for B5, A1, A4, A6, C1a, C1b — reuse existing projection/
  config fixtures; assert the emitted JSON/config record is unchanged.
- **Surface baselines**: each Tier-1 PR regenerates only its module's surface-drift baseline; the
  api-compat gate must show *only* the planned delta (contracts/surface-deltas.md is the checklist).
  A baseline move on any *other* module is a defect (FR-016).
- **Dependency-fence suite** (`tests/FS.GG.Governance.DependencyFences.Tests`) runs on the A1
  (Scaffold→CommandHost) and A6 (any new edge) PRs and must stay green; a red fence is the signal to
  re-defer that helper (FR-009).

**Rationale**: matches the constitution's Tier-1 obligation ("a Tier-1 change that fails to update
`.fsi` or baselines is a defect") and the real-evidence mandate, while acknowledging F#'s lack of a
compile-fail test primitive.

## Resolved unknowns

All Technical-Context items are concrete; no `NEEDS CLARIFICATION` remains. The single conditional
— whether `sha256Hex` gets a sound Kernel home — is resolved *at implementation time by the fence
suite*, with a defined fallback (re-defer), so it does not block planning.
