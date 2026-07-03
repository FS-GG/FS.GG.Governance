# Quickstart: validating the deferred-tail cleanup

This is a **validation/run guide**, one section per user story (= one PR). Each proves its slice
with real evidence and leaves the tree green. Implementation detail lives in `tasks.md` (Phase 2);
the authoritative surface deltas are in [contracts/surface-deltas.md](./contracts/surface-deltas.md).

## Prerequisites

```sh
cd /home/developer/projects/FS.GG.Governance
git switch 111-review-deferred-tail
dotnet build FS.GG.Governance.sln -c Release      # baseline: green before any change
```

Per-module surface baselines live in `tests/<Project>.Tests/SurfaceDriftTests.fs` (embedded
expected surface). The dependency-fence suite is `tests/FS.GG.Governance.DependencyFences.Tests`.

## Global acceptance (every PR)

```sh
dotnet build FS.GG.Governance.sln -c Release
dotnet test  FS.GG.Governance.sln                 # full suite green (review H1: runs on every PR)
```
- Surface-drift: only the module(s) in the PR's contract row may change; every other
  `SurfaceDriftTests` stays green **unmodified** (SC-001, FR-016).
- Fence suite: green (mandatory on the A1/A6 PRs).

---

## US1 — Illegal gate-outcome states unrepresentable (B4) · Tier 1 · contract C1+C2

```sh
dotnet test --filter "FullyQualifiedName~GateRun"
dotnet test --filter "FullyQualifiedName~JsonWriters|FullyQualifiedName~VerifyJson|FullyQualifiedName~HumanText"
```
**Expected**:
1. **Compile-fail evidence**: the `GateResult` type admits no `Executed`-without-exit-code value —
   the negative snippet in the GateRun test file is commented "does not compile" and the type is the
   proof (R5). Positive tests assert the new `GateResult` emits **byte-identical** `disposition`/
   `exitCode`/`passed` JSON and human strings vs `main` for every gate outcome.
2. `commandFor` returns `NoPrerequisite` / `UnresolvedCommand id` / `EmptyCommandLine` for the three
   no-command inputs (RED on `main` where all are `None`).
3. Surface-drift: **only** `GateRun/Model` and `GateRun/Plan` baselines move, matching C1/C2.

## US2 — Signatures state only what they use (B6/B7/B9) · Tier 1 · contract C3+C4+C5

```sh
dotnet test --filter "FullyQualifiedName~Snapshot"      # B9
dotnet test --filter "FullyQualifiedName~Calibration"   # B6
dotnet test --filter "FullyQualifiedName~ValidationMatrix"  # B7
```
**Expected**:
- **B9**: a git-unavailable snapshot via `assemble { raw with RepoState = GitUnavailable }` equals
  the previous hand-rolled record field-for-field (same `GitUnavailable` diagnostic, `Range=None`,
  empty sets, digest order). `Snapshot/Snapshot` baseline moves (C3); `Interpreter.fs` is `.fs`-only.
- **B6**: `decide` output byte-identical for every fixture after `ComparisonSample.Agreement` drops;
  `Calibration/Model` baseline moves (C4).
- **B7**: `decideMatrix` `MatrixPlan` output identical after `boundary` drops; all callers compile;
  `ValidationMatrix/Matrix` baseline moves (C5).

## US3 — Config loader threads values (B5) · Tier 2 · no surface move

```sh
dotnet test --filter "FullyQualifiedName~Config"
```
**Expected**: every config fixture parses to the identical record + identical diagnostics; a review
`grep` shows no `.Value`/`Option.get` remains in the `finish` build thunks
(`Schema.fs:540-547,570-574,588-590,622-623,372`). No `.fsi`/baseline change.

## US4 — Dedup into shared homes (A1/A4/A6) · Tier 2 (+ C6 export) · fence-gated

```sh
dotnet test --filter "FullyQualifiedName~DependencyFences"   # MUST stay green with new edges
dotnet test --filter "FullyQualifiedName~RouteJson|FullyQualifiedName~AuditJson|FullyQualifiedName~GatesJson|FullyQualifiedName~ReleaseJson|FullyQualifiedName~VerifyJson"
dotnet test --filter "FullyQualifiedName~SurfaceChecks|FullyQualifiedName~Kernel|FullyQualifiedName~SddHandoff|FullyQualifiedName~EvidenceCommand|FullyQualifiedName~Scaffold"
```
**Expected**:
- **A1**: `EvidenceCommand`/`Scaffold` behaviour unchanged using `CommandHost.guard`/`drive`;
  Scaffold's new `CommandHost` reference keeps the fence suite green.
- **A4**: each projection emits byte-identical JSON; `grep` shows one definition per writer pair in
  `JsonWriters`.
- **A6**: one `mkFinding`/`safe`/`valuesFor` in SurfaceChecks (domain+maturity params); one
  `buildGate` in SddHandoff; `Route.stakesOf` yields byte-identical stakes via the newly-exported
  `Verdict.combineReasons` (**C6** — the only surface *widen*, `Kernel/Verdict` baseline moves).
- **`sha256Hex` (conditional)**: if the fence suite stays green with a Kernel home, it lands there;
  otherwise it **stays duplicated** and is re-annotated on #83 (FR-009) — either outcome is a
  passing PR.

## US5 — Dead code removed (C1a/C1b) · Tier 2

```sh
dotnet test --filter "FullyQualifiedName~DocsChecks|FullyQualifiedName~VerifyCommand"
grep -rn "ExampleFact\|exampleFindings\|SurfacesPending" src   # → no matches after
```
**Expected**: DocsChecks and VerifyCommand outputs byte-identical; the dead vocabulary is gone.

## US6 — Cosmetic hygiene (C1g) · Tier 2

```sh
dotnet build FS.GG.Governance.sln -c Release      # green ⇒ removed opens were unused
```
**Expected**: the six headers describe their file's actual access modifiers; the three (or wider)
dead `open System.IO`/`System.Text` are gone and the build is green.

## US7 — Docs (C2f/C2g) · Tier 2

```sh
ls docs/decisions/README.md          # new index over 0001–0008
```
**Expected**: `VerifyCommand.fsproj` carries a comment explaining the 43-declared/~32-reachable
full-surface-host convention (no reference pruned); `docs/decisions/README.md` lists every local
decision and cross-links `docs/adr/README.md` (org ADR-0012/0013), resolving the "stubs" ask.

---

## Definition of done (feature)

- All seven PRs merged, each green and standalone (SC-005).
- Surface baseline moved for **only** C1–C6; no other `SurfaceDriftTests` touched (SC-001).
- `grep` shows one definition per consolidated helper and no dead vocabulary (SC-003).
- Issue #83 checklist fully checked, with any fence-blocked helper (e.g. `sha256Hex`) annotated as
  re-deferred (SC-004); epic #44 closes once #83 closes.
