# Quickstart: Ship Verdict Rollup (Pure Core)

**Feature**: `024-ship-verdict-rollup` | Validates the spec's three user stories end-to-end against the
**public** surface (`Ship.rollup` + `Ship.Model`), never private helpers (Constitution Principle V).

## Prerequisites

- .NET SDK (`net10.0`), repo restored.
- Projects: `FS.GG.Governance.Ship` (new) referencing `FS.GG.Governance.Enforcement` (F023) and
  `FS.GG.Governance.Route` (F019); `Gates`/`Findings`/`Config` arrive transitively.
- Test project `FS.GG.Governance.Ship.Tests` (Expecto + FsCheck, the F023 shape).

## Build & test

```bash
dotnet build FS.GG.Governance.sln
dotnet test  tests/FS.GG.Governance.Ship.Tests/FS.GG.Governance.Ship.Tests.fsproj
```

Expected: build succeeds; all Ship tests pass, including the surface-drift test against
`surface/FS.GG.Governance.Ship.surface.txt`.

## FSI smoke (Principle I — exercise the shape before trusting it)

Load the prelude (or `dotnet fsi` with the built DLLs referenced) and run:

```fsharp
open FS.GG.Governance.Enforcement.Enforcement   // RunMode, Profile
open FS.GG.Governance.Ship                       // Model, Ship

// An empty routed change rolls up to a clean pass (US3 / edge case).
let empty = Ship.rollup emptyRouteResult Gate Standard
// expect: { Verdict = Pass; Blockers = []; Warnings = []; Passing = []; ExitCodeBasis = Clean }

// The design's worked example as a block-on-ship gate at inner/light => a warning (US2).
let example = Ship.rollup routeWithBlockOnShipGate Inner Light
// expect: Blockers = []; Warnings = [ one item, effective Advisory, reason names the 'gate' boundary ]

// Same change at gate/light: block-on-ship gate blocks, block-on-release gate warns (US1/US2).
let blocked = Ship.rollup routeWithShipAndReleaseGates Gate Light
// expect: Verdict = Fail; Blockers = [block-on-ship]; Warnings = [block-on-release]; ExitCodeBasis = Blocked
```

(`emptyRouteResult`, `routeWith…` are built from real F018/F017/F019 values in the test `Support.fs` —
no mocks, no synthetic evidence; the input domain is finite and literally constructible.)

## Validation scenarios

### US1 — Roll a routed change up into a ship verdict (P1)

1. Build a `RouteResult` mixing gates of varied maturities and findings of both zones; pick a mode &
   profile; run `rollup`.
2. Assert `Verdict = Fail` **exactly when** ≥1 item derives effective `Blocking`; `Blockers` is exactly
   those items; `Warnings` is exactly the base-blocking-but-relaxed items; `ExitCodeBasis` matches the
   verdict. Assert every item carries base/effective severity, mode, profile, maturity, reason.
   → contract C1–C6.

### US2 — Worked example holds at change scale (P1)

1. Embed a `BlockOnShip` gate as one item among several; `rollup` at `Inner`/`Light`; assert it lands in
   `Warnings`, effective `Advisory`, with the documented reason, contributing no blocker.
2. Re-evaluate at `Gate`/`Light` (or a stricter profile): assert it moves into `Blockers` and the
   verdict flips to `Fail` — with no input (base severities, maturities, findings) altered.
3. Assert every item's output `Decision.BaseSeverity` equals its mapped base severity byte-for-byte.
   → contract C2–C5, SC-002.

### US3 — Deterministic, total, never-throwing (P2)

1. Run `rollup` twice over identical inputs; assert the two `ShipDecision`s are structurally identical
   (verdict, all three lists, exit-code basis, ordering). → C8, SC-004.
2. Run over the empty `RouteResult` at several modes/profiles; assert `Pass` / empty lists / `Clean`,
   never an error. → C7 edge case.
3. FsCheck property over the enumerated cross-product of gate maturities, finding zones, modes, and
   profiles: assert `rollup` never throws, the partition is disjoint and exhaustive
   (`|B|+|W|+|P| = N+M`), and each list is sorted by the composite key. → C7–C9, SC-005/SC-006.

## Negative / absence assertions (SC-007)

A property/example test asserts the `ShipDecision` contains **none** of: a serialized audit (or other)
document string, a numeric process exit code, a cache-eligibility verdict, a freshness evaluation, a
`policy.yml`-derived dial, a wall-clock value, or a machine-absolute path. The type itself excludes
these by construction; the test pins the intent. → contract C11.

## Detail references (no duplication here)

- Types & invariants: [data-model.md](./data-model.md)
- Public signatures: [contracts/Model.fsi](./contracts/Model.fsi), [contracts/Ship.fsi](./contracts/Ship.fsi)
- Behavioural contract & worked example: [contracts/ship-decision.md](./contracts/ship-decision.md)
- Plan-time reconciliations (D1–D8): [research.md](./research.md)
