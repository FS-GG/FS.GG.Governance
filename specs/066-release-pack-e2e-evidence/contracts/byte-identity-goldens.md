# Contract — Frozen Byte-Identity Goldens for the Unchanged Contracts (US3)

**Closes** `065` T009 / T024 · **Covers** FR-005, FR-006, SC-004.

A **test contract** pinning the four contracts that `065` was supposed to leave identical, byte-for-byte,
against frozen pre-wiring baselines. No product code changes.

## The four goldens

| Golden | Producing command | Frozen from | Test location |
|--------|-------------------|-------------|---------------|
| `route.json` | `fsgg route` | pre-wiring anchor **`5a0cb28`** (F25/`064`, parent of `065`) | `RouteCommand.Tests/PersistenceEdgeTests.fs` |
| `ship.json` | `fsgg ship` | pre-wiring anchor **`5a0cb28`** | `ShipCommand.Tests/PersistenceEdgeTests.fs` |
| `verify.json` (no `.fsgg/release.yml`) | `fsgg verify` | pre-wiring anchor **`5a0cb28`** | `VerifyCommand.Tests/PersistenceEdgeTests.fs` |
| `release.json` v2 (empty additive fields) | `fsgg release` | F26-blessed `fsgg.release/v2` contract (current) | `ReleaseCommand.Tests/PersistenceEdgeTests.fs` |

## Why two freeze sources (the honesty anchor — research.md D2)

- **route / ship / no-declaration verify** are byte-identical at `5a0cb28` and at `main` *by
  construction* (RouteCommand/ShipCommand untouched by `065`; the `verify.json` `releaseReadiness` block
  is emitted only when a declaration is present). Freezing from `5a0cb28` makes the check **prove** the
  wiring left them untouched rather than vacuously re-deriving from post-wiring code (spec edge case).
- **empty-v2 release.json** has no honest pre-wiring bytes — `release.json` v2 is *introduced* by
  F26/`065`. Its golden is the F26-blessed empty-additive shape, pinning the additive contract going
  forward (exactly `065` T024's scope).

## Freeze procedure (one-time, per golden)

1. For the three construction-identical goldens: check out `5a0cb28` in a throwaway `git worktree`, build
   the producing host, run it over the fixed fixture repo, and commit the captured bytes as the golden.
2. For the empty-v2 release golden: capture from current code over a product whose additive v2 fields are
   empty.

## Required behaviour (asserted)

For each golden: run the **real** producing command over the fixed fixture repo for identical repository
state, read the produced JSON, and `Expect.equal` the bytes against the committed golden. A mismatch
fails loudly. (FR-005, FR-006, SC-004)

## Anti-requirements

- MUST NOT re-derive route/ship/no-decl-verify goldens from post-wiring `main` (vacuous).
- MUST NOT place a golden test outside its producing host's `.Tests` project (would require a cross-host
  reference or a faked producer).
- Goldens are JSON test data — MUST NOT touch any `.fsi` or `surface/*.txt`.
