# Quickstart / Validation: Promote `governedReferences` to First-Class Routing Facts

**Feature**: `082-route-governed-refs`

This guide lists the runnable scenarios that prove the feature end-to-end. Each maps to a
spec Success Criterion and is exercised through the **real** `Config→Gates→Routing→Route`
pipeline (real evidence, no mocks of the selection algorithm). See
[contracts/consumer-candidatePaths.fsi.md](./contracts/consumer-candidatePaths.fsi.md) and
[contracts/host-candidate-seam.md](./contracts/host-candidate-seam.md) for the unit of change;
[data-model.md](./data-model.md) for the data flow.

## Prerequisites

- .NET `net10.0` SDK; repo restored.
- A reference catalog with a path-map routing `src/**`→`build`, `tests/**`→`test`
  (the existing `samples/sdd-reference-gate-set/.fsgg/` set, or the test fixtures already used
  by the three host test projects).
- A handoff fixture `readiness/<id>/governance-handoff.json` declaring `governedReferences`
  in those domains (added under the relevant test project's `fixtures/`).

## Build & test

```bash
# whole-solution (bounded wrapper — see build.fsx)
dotnet fsi build.fsx

# focused:
dotnet test FS.GG.Governance.Adapters.SddHandoff.Tests   # candidatePaths unit cases (C1–C8)
dotnet test FS.GG.Governance.RouteCommand.Tests           # US1/US2/US3 routing scenarios
dotnet test FS.GG.Governance.ShipCommand.Tests            # SC-004 verdict flip
dotnet test FS.GG.Governance.VerifyCommand.Tests          # SC-005 strict-blocking

# re-bless the additive surface line after adding candidatePaths:
BLESS_SURFACE=1 dotnet test FS.GG.Governance.Adapters.SddHandoff.Tests
```

## Validation scenarios

### V1 — Declared surface drives selection (SC-001, US1)
Load a handoff whose `governedReferences` declare paths routing to `build`/`test`, with an
**empty** sensed change set, and run `route` through the host `update`. **Expect**: `build:build`
and `test:test` appear in `result.SelectedGates`, each with a selecting-path naming the declared
path and the real matched glob. **Baseline contrast**: the pre-feature pipeline selects neither.

### V2 — Real-glob, de-duplicated provenance (SC-006, SC-003, US2)
(a) The declared-path-driven gate's selecting-path records the **real** path-map glob, not a
self-glob (SC-006). (b) When the same gate is also reached by a sensed path, it carries one
merged, de-duplicated, deterministically-ordered selecting-path set; a path present in BOTH
sources appears exactly once and is counted once in the cost rollup (SC-003).

### V3 — Ship verdict flip from the declared surface (SC-004, US1 AS2)
Given a handoff declaring paths under a `block-on-ship` domain and a failing change there, with
a sensed diff that does **not** touch that domain, run `ship` in a blocking mode. **Expect**: the
gate appears in `Blockers` and the verdict is non-shippable — a flip caused *solely* by the
declared surface. This is the headline behavioral proof.

### V4 — Verify strict-blocking from the declared surface (SC-004, US1 AS3)
Given a handoff declaring paths in a domain whose gate is verify-blocking under `Strict`, run
`verify --strict`. **Expect**: the verdict is blocked by that gate.

### V5 — Byte-identical no-op (SC-002, US3) — the safety boundary
Run `route`/`ship`/`verify` in: (a) an empty repo with **no** handoff; (b) a repo with a handoff
declaring an **empty** `governedReferences`. **Expect**: output **byte-identical** to the
pre-feature baseline across all three hosts (every existing golden/snapshot unchanged).

### V6 — Bad-document boundary holds (SC-005, US3 AS3)
Run with a malformed / major-version-mismatched handoff. **Expect**: the document's blocking
integrity gate fires (as in F081) **and** none of its `governedReferences` contribute routing
candidates (no widened enforcement).

### V7 — Unit: `candidatePaths` (C1–C8)
The adapter unit tests in [contracts/consumer-candidatePaths.fsi.md](./contracts/consumer-candidatePaths.fsi.md):
empty ⇒ `[]`; consumable-only; dedup across docs/work-items; bad doc ⇒ no contribution;
normalized output.

## Done when

- [ ] V1–V7 pass through the real pipeline (no synthetic routing facts).
- [ ] All pre-existing route/ship/verify goldens + snapshots are byte-identical (V5).
- [ ] The adapter surface baseline grows by exactly one additive line (`candidatePaths`).
- [ ] ADR-0002 item #3 reads **Resolved (F082)** and the handoff tutorial shows declared paths
      driving selection (FR-012).
