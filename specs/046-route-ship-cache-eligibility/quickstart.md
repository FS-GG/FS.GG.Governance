# Quickstart — Emit Real Cache-Eligibility Verdicts From `fsgg route` and `fsgg ship` (F046)

A validation/run guide. Implementation detail lives in the contracts and `tasks.md`. All commands run from
the repo root.

## Prerequisites

- .NET `net10.0` SDK; the solution restores from the configured feeds.
- The merged cache-eligibility thread (F029–F045) and the two host commands (F022/F026) are present.

## 1. Design-first (Principle I) — FSI before `.fs`

1. Draft `contracts/FreshnessSensing.fsi` into `src/FS.GG.Governance.FreshnessSensing/FreshnessSensing.fsi`
   and the two command `Loop.fsi`/`Interpreter.fsi` deltas (route-/ship-command-delta.md) **first**.
2. Append an F046 section to `scripts/prelude.fsx` that runs each command's `Interpreter.run` with **faked**
   ports (a fixed-hash `FreshnessSensor`, an absent `StoreReader`) over a small in-memory catalog, and:
   - asserts the emitted `route.json` / `audit.json` carry `"cacheEligibilityEvaluated": true` and a per-gate
     verdict (every gate `mustRecompute` / `noPriorEvidence` with an empty store);
   - runs one **degrade** path (a `StoreReader` returning `Error`) and asserts the document still emits, the
     exit code is unchanged, and a cache note appears.

   ```bash
   dotnet fsi scripts/prelude.fsx
   ```

## 2. Build

```bash
dotnet build FS.GG.Governance.sln
```

Expected: the new `FS.GG.Governance.FreshnessSensing` library and the two edited commands build clean across
the whole solution; every other project is unchanged (SC-008).

## 3. Test

```bash
dotnet test FS.GG.Governance.sln
```

Confirm:

- **`FreshnessSensing.Tests`** — `realSensor` over a temp dir yields stable, ordinal-sorted SHA-256 hashes;
  `loadStore` maps absent ⇒ empty, present-well-formed ⇒ parsed, present-malformed ⇒ `Error`.
- **`RouteCommand.Tests` / `ShipCommand.Tests`** — the loop runs the sense→resolve→evaluate→embed pipeline;
  the emitted documents equal the live-recomputed projection with `Some expectedReport`; US3 degrade tests
  pass (unsensed fact ⇒ `notEvaluated` + note, exit unchanged; malformed store ⇒ degrade-to-empty + note,
  exit unchanged).
- **SC-003 ship-invariant** — the `ShipCommand` test proving verdict / partition / enforcement fields /
  `ExitCodeBasis` / numeric exit are identical with `Some report` vs the `None` projection of the same input.
- **Untouched** — `RouteJson.Tests`, `AuditJson.Tests`, and `EnforcementFixtures.Tests` pass **without
  edits** (still on the `None` path; F028 golden snapshots unchanged).

## 4. Re-bless the surface baselines (Tier 1)

Only the three genuinely-changed public surfaces:

```bash
BLESS_SURFACE=1 dotnet test FS.GG.Governance.sln
```

Re-blesses `surface/FS.GG.Governance.FreshnessSensing.surface.txt` (new),
`surface/FS.GG.Governance.RouteCommand.surface.txt`, and `surface/FS.GG.Governance.ShipCommand.surface.txt`.
Confirm `git diff surface/` touches **only** those three files — `RouteJson` / `AuditJson` surfaces must be
untouched. **Do not** run `BLESS_FIXTURES=1`: no golden fixture changes this row (the F028 audit snapshots
stay on the `None` path).

## 5. Exercise against a real repo (optional manual check)

```bash
dotnet run --project src/FS.GG.Governance.RouteCommand -- route --repo . --json
dotnet run --project src/FS.GG.Governance.ShipCommand  -- ship  --repo . --mode gate --profile standard --json
```

Expected: both emit a document whose cache section is **evaluated** (`cacheEligibilityEvaluated: true`); with
no `readiness/evidence-reuse.json` present, every selected gate reads `mustRecompute` / `noPriorEvidence`.
`fsgg route` exits 0; `fsgg ship`'s exit code matches its verdict exactly as before this row.

## 6. Update the agent context

Point `CLAUDE.md` (between the `<!-- SPECKIT START/END -->` markers) at
`specs/046-route-ship-cache-eligibility/plan.md`.

## Done When

- New library + two commands build clean; all tests green (`dotnet test`), including the US3 degrade tests
  and the SC-003 ship-invariant.
- `git diff surface/` shows only the three expected baselines; no golden fixture diff; no edit to
  F041/F042/F043/F045/F044 or their baselines (SC-008).
- `route.json` / `audit.json` carry real per-gate verdicts; `fsgg route` exits 0 and `fsgg ship`'s
  verdict/exit are unchanged.
