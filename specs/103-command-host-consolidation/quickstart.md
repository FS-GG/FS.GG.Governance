# Quickstart / Validation Guide: Command-host second extraction pass (#49)

Runnable validation for each story. All commands run from repo root through the bounded entrypoint. No new tooling.

## Prerequisites

```bash
dotnet restore FS.GG.Governance.sln --locked-mode
dotnet fsi build.fsx --no-restore        # baseline build (~90s)
```

## Story 1 (P1) — the behavior fixes, RED→GREEN

Each fix ships with an Expecto test that fails before and passes after. Drive the **real parsed surface**, not private helpers.

- **M-CLI-3 (argv value swallow)** — for each host, assert that `["--repo"; "--json"]` parses to `Error (MissingValue "--repo")` (or the host's equivalent), not `Repo = "--json"`. Also assert `["--repo"; "acme/x"; "--json"]` still parses cleanly (JSON mode on, repo = `acme/x`).
- **M-CLI-7 (Evidence `--plain`)** — assert `--format json --plain` still emits the JSON contract string (unchanged), and that `--plain` is no longer a silently-unconsumed field (usage/help documents it as an inert no-op for Evidence, or the dead field is gone).
- **F15 (`Wrote(Ok)` model)** — drive Ship's `update` through the `Wrote(Ok())` transition and assert the emitted summary reflects `Phase = Persisted` (post-update), matching Verify.
- **F13 (Evidence Done-inertness)** — set Evidence model to `Phase = Done`, feed a `Wrote(Ok())`/`Reported`/`Emitted` Msg, assert `update` returns `(model, [])` with no effects and no phase mutation.

Run the affected suites:
```bash
dotnet fsi build.fsx test --no-restore --no-build \
  # (or the whole suite; see Story 3)
```
Expected: the four new tests are RED on a clean checkout of this branch's first WIP commit and GREEN after the corresponding fix.

## Story 2 (P2) — one implementation per leaf

- **Single-definition check** — after Phase A, a repo grep finds exactly one *defining* site for each shared leaf:
  ```bash
  for s in writeAtomic realHandoffs senseEnvironmentReal senseBuilderReal; do
    echo -n "$s defs: "; grep -rn --include=*.fs "let $s" src | grep -vE 'obj/|bin/' | wc -l
  done   # each expected: 1 (in CommandHost)
  ```
- **ArtifactReading dedup (Phase B)** — assert `EvidenceCommand/Interpreter.fs` no longer contains the copied readers and shrank ~325 lines:
  ```bash
  wc -l src/FS.GG.Governance.EvidenceCommand/Interpreter.fs   # ~515 → ~190
  grep -n "designFactsFromFile\|specKitFacts" src/FS.GG.Governance.EvidenceCommand/Interpreter.fs   # expected: none (now via Cli)
  ```
- **Unchanged output** — the pre-existing golden/summary assertions for every host stay green with no edits (proves consolidation is behavior-preserving).

## Story 3 (P3) — conventions converge or are documented

- **ExitDecision (Phase C)** — `grep -rn "type ExitDecision" src | grep -vE 'obj/|bin/'` returns **1** (only `CommandHost`), or, if the fallback was taken, the dead canonical is gone and hosts are unchanged — either way no redundant live+dead pair remains.
- **F9 format-flag vocabulary** — either the four vocabularies are converged (a single `--format`/`--json`/`--plain` grammar), or each surviving divergence carries an in-repo note (comment/spec) explaining why. Verify by reading the per-host parse arms listed in research.md D2/F9.

## Full-suite + gate evidence (SC-005)

```bash
dotnet fsi build.fsx test --no-restore     # full Expecto suite incl. SurfaceDrift — expect all green
```
On the PR:
- **SurfaceDrift** green: Phase A adds only the `CommandHost` `val`s; Phase B/C baseline deltas are committed deliberately.
- **Deterministic gate** green (locked restore + build).
- **API-compat gate** reports the intended `ExitDecision`/`ArtifactReading` surface deltas — expected, not a regression.

## Net-LOC check (SC-002)

```bash
git diff --stat main...HEAD -- 'src/**/*.fs'   # expect net removal on the order of 600–800 lines
```
