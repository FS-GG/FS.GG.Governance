# Quickstart — Validate `evidence.json` Projection

**Feature**: `069-evidence-json-projection` | **Date**: 2026-06-26

Runnable validation that the effective-evidence projection works end-to-end and meets the Success Criteria.
Prerequisites: .NET 10 SDK; the solution builds (`dotnet build FS.GG.Governance.sln`). The host is
`fsgg evidence` (project `FS.GG.Governance.EvidenceCommand`); the projection is `FS.GG.Governance.EvidenceJson`.

## Build & unit-test the projection

```bash
dotnet test tests/FS.GG.Governance.EvidenceJson.Tests
dotnet test tests/FS.GG.Governance.EvidenceCommand.Tests
```

## Scenario 1 — Inspect the effective-evidence world (US1 / SC-001, SC-004)

Run the host over the golden-fixture repo and read the document:

```bash
dotnet run --project src/FS.GG.Governance.EvidenceCommand -- evidence --repo tests/golden-fixture --format json
cat tests/golden-fixture/readiness/evidence.json
```

**Expect**: a `{ "schemaVersion": "fsgg.evidence/v1", "graphFailure": null, "nodes": [...], ... }` object where
every graph node appears once with **both** `declared` and `effective` states. A node declared `Real` that
depends (transitively) on a `Synthetic` node shows `effective: "AutoSynthetic"` while keeping `declared:
"Real"` (the demotion is visible, FR-002). A node with only real inputs shows `declared`/`effective` both
`"Real"`. The effective set matches `Kernel.Evidence.effective` exactly (SC-004) — asserted by
`ProjectionTests`/`EndToEndTests`.

## Scenario 2 — Byte-identity across runs (US1 / SC-002)

```bash
dotnet run --project src/FS.GG.Governance.EvidenceCommand -- evidence --repo tests/golden-fixture
cp tests/golden-fixture/readiness/evidence.json /tmp/evidence.1.json
dotnet run --project src/FS.GG.Governance.EvidenceCommand -- evidence --repo tests/golden-fixture
diff -q /tmp/evidence.1.json tests/golden-fixture/readiness/evidence.json && echo "BYTE-IDENTICAL"
```

**Expect**: `BYTE-IDENTICAL`. Covered by `DeterminismTests` (pure `ofReport`) and the host re-run test.

## Scenario 3 — Empty evidence set (Edge Case / FR-010)

Point the host at a repo with no evidence-bearing units.

**Expect**: a valid deterministic document with `"nodes": []` — not an error, not a missing file. Covered by
`ProjectionTests` (`WellFormed ([], [])`).

## Scenario 4 — Why a node is not effective (US2 / SC-006)

Use the per-cause fixtures (stale, synthetic-tainted, failed, skipped).

**Expect**: each non-effective node is self-describing from the document alone:
- tainted → `effective` ≠ `declared`;
- stale → `freshness.kind = "stale"` with `cause.kind` `noPriorEvidence` or `inputsChanged` naming the exact
  changed-input categories;
- unresolved → `freshness.kind = "unresolved"` with a non-empty `missing` list naming every missing fact;
- skipped → `declared: "Skipped"` (distinct from `Failed`/`Pending`).

Covered by `FreshnessCauseTests` / `NoHideTests`.

## Scenario 5 — Graph failures named, never swallowed (US3 / SC-003)

Feed each `GraphError` kind (cycle, unknown-node, auto-synthetic-declared) via fixtures.

**Expect**: the document carries `graphFailure: { "kind": "cycle" | "unknownNode" | "autoSyntheticDeclared", … }`
and **omits** `nodes`/`dependencies` — no guessed per-node effective map (FR-004). Covered by
`GraphFailureTests`.

## Scenario 6 — Additivity (SC-005)

```bash
git stash --keep-index   # or build the pre-feature commit
# regenerate route/audit/verify/cache-eligibility goldens and compare — expect 0-byte diff
dotnet test tests/FS.GG.Governance.EvidenceCommand.Tests --filter Additivity
```

**Expect**: every existing `route.json` / `audit.json` / `verify.json` / `cache-eligibility.json` golden and
the existing `fsgg-governance evidence` output are byte-unchanged; no verdict or exit-code basis changes.
Covered by `AdditivityTests`.

## Success-criteria map

| SC | Scenario | Test home |
|---|---|---|
| SC-001 100% of nodes, both states | 1 | `ProjectionTests`, `EndToEndTests` |
| SC-002 byte-identical re-run | 2 | `DeterminismTests`, host re-run |
| SC-003 all 3 graph-failure kinds, 0 guessed maps | 5 | `GraphFailureTests` |
| SC-004 effective set matches the closure | 1 | `ProjectionTests`, `EndToEndTests` |
| SC-005 0-byte change to existing goldens/verdicts | 6 | `AdditivityTests` |
| SC-006 every non-effective node self-describing | 4 | `FreshnessCauseTests`, `NoHideTests` |
