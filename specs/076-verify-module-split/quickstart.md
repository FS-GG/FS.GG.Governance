# Quickstart: validating the Verify god-module split (Phase C)

Acceptance is **byte-identical goldens/snapshots + a green suite**. This guide is the
run/validation recipe; implementation details live in `tasks.md` and the code.

## Prerequisites

- .NET `net10.0` SDK; repo builds today at `00e731e`.
- Run from repo root: `/home/developer/projects/FS.GG.Governance`.

## 0. Capture the pre-refactor baseline (once, before any change)

```bash
dotnet test tests/FS.GG.Governance.VerifyJson.Tests    # record pass count
dotnet test tests/FS.GG.Governance.VerifyCommand.Tests # record pass count
```

These two counts are the FR-006/SC-003 floor — later runs must be ≥ this (the only
tolerated exception is the pre-existing Cli `dotnet pack` timeout flake).

## 1. Per-seam loop (FR-010 — ONE seam per commit)

For each of the seven seams (3 host folds, then 4 projection seams), in its own commit:

1. Add the new `<Module>.fsi` + `<Module>.fs`; add both to the project's `<Compile>`
   list **before** the host/entry module.
2. Move the bindings (see `contracts/` + research.md D2/D3); replace the in-place code
   with a call to the new module. Change no emitted byte.
3. Build + run the affected suite:
   ```bash
   dotnet test tests/FS.GG.Governance.VerifyJson.Tests        # for a projection seam
   dotnet test tests/FS.GG.Governance.VerifyCommand.Tests     # for a host fold
   ```
   **Expected:** all goldens/determinism/rollup/preview tests stay green and
   byte-identical. A golden diff = the extraction changed behavior → investigate and
   revert, never re-baseline (FR-005, edge case "Golden drift as a failure signal").
4. Commit (`076 …: extract <seam>`).

## 2. Re-bless the two surface baselines (ONCE, additive only)

After the seams exist, the reflective drift tests will report new public module types.
Re-bless **once**, then verify the diff is purely additive:

```bash
BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.VerifyJson.Tests
BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.VerifyCommand.Tests
git diff -- surface/FS.GG.Governance.VerifyJson.surface.txt \
            surface/FS.GG.Governance.VerifyCommand.surface.txt
```

**Expected diff:** only ADDED `TYPE …CoreModule` / `…SurfaceChecksModule` /
`…ReleaseReadinessModule` / `…GeneratedViewsModule` (projection) and `…SurfaceFoldModule`
/ `…ViewCurrencyFoldModule` / `…ReleasePreviewModule` (host) blocks. **No existing line
removed or changed** — the `VerifyJsonModule` and `LoopModule` blocks must be untouched
(FR-004). If an existing line moved, the split widened an existing surface → fix before
committing the re-bless.

Re-run both drift tests (no `BLESS_SURFACE`) → green against the re-blessed baseline.

## 3. Full-suite gate

```bash
dotnet test   # whole solution
```

**Expected:** green save the known Cli pack-timeout flake; per-project counts ≥ the
Step-0 floor (additive structural tests — e.g. a scope guard over the new modules — may
raise them).

## 4. GateRunHost ADR (US3 — documentation only)

Confirm exactly one new ADR exists and takes an unambiguous position:

```bash
ls docs/decisions/0003-*.md      # 0003-gaterunhost-unification.md (planned verdict: DEFER)
```

The ADR states pursue/defer/drop with rationale referencing Phase B's clean golden diff
(research.md D6). Because it defers, confirm route/ship/verify host skeletons are
untouched:

```bash
git diff --stat src/FS.GG.Governance.RouteCommand src/FS.GG.Governance.ShipCommand
# expect: no changes (FR-008 second clause)
```

## Done when

- [ ] Every `VerifyJson.Tests` / `VerifyCommand.Tests` golden + snapshot byte-identical (SC-002).
- [ ] Both surface baselines re-blessed additively only; drift tests green (FR-004).
- [ ] `VerifyJson` exposes the same four entry points + `schemaVersion`, unchanged (FR-003/SC-004).
- [ ] Base `Loop.fs` measurably smaller; 3 host folds + 4 projection seams in their own modules (SC-001/SC-006).
- [ ] Full suite green ≥ baseline counts (SC-003).
- [ ] Exactly one GateRunHost ADR under `docs/decisions/` (SC-005); route/ship/verify untouched.
