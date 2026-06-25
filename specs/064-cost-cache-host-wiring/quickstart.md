# Quickstart — Cost-Cache Host Wiring (F25 wiring)

Validation scenarios proving the wiring end-to-end. Build first; run the two hosts' test suites; then the manual
checks. All scenarios use **real** F25 cores and real hosts — only edge ports (store reader, executor, artifact
writer, environment/builder sensors) are faked.

## Prerequisites
```bash
cd /home/developer/projects/FS.GG.Governance
dotnet build FS.GG.Governance.sln
dotnet test tests/FS.GG.Governance.VerifyCommand.Tests
dotnet test tests/FS.GG.Governance.ShipCommand.Tests
```

## Scenario 1 — Expensive recompute is bounded (US1, SC-001/SC-002)
Over a tree with one `Cheap` in-budget must-recompute gate, one `High`/`Exhaustive` over-budget must-recompute gate,
and one reusable gate, under a tight budget (`--profile Light` for `verify`; `--profile Light --mode Inner` semantics
at the boundary for `ship`):
- **Expect:** the over-budget gate is **absent** from the executed set, recorded `OverBudget` with a named
  `BudgetReason`, charges nothing, and is **never** in `Passing`; the in-budget gate runs; the reusable gate reuses.
- Cross-check `cost-budget.json`: the over-budget gate appears with its `OverBudget` decision and reason.

## Scenario 2 — Two deterministic sidecars, existing goldens untouched (US2, SC-003/SC-004)
Run `fsgg verify` twice over an unchanged tree:
- **Expect:** `cost-budget.json` (`fsgg.cost-budget/v1`) and `provenance.json` (`fsgg.provenance/v1`) are written and
  **byte-identical** across the two runs; reordering the candidate gates changes no byte.
- **Expect:** `verify.json` (and for `ship`, `audit.json`) are **byte-identical** to their frozen pre-wiring
  baselines. The same holds with an empty input set — both sidecars are well-formed empty-array documents.

## Scenario 3 — Standalone product, missing store (US3, SC-005)
Check out a generated product standalone (no monorepo) with its own recorded evidence; run `fsgg verify`:
- **Expect:** the budget/cache decision and provenance snapshot draw only on product-local sources.
- Remove/corrupt the evidence store and re-run:
- **Expect:** a clear input diagnostic names the offending source (input, not defect); no crash; no fabricated reuse;
  both sidecars still well-formed (everything `MustRecompute`/`NoEvidence`).

## Scenario 4 — Agent-reviewed stays advisory (SC-006)
With an agent-reviewed gate under any profile/mode and any cache decision:
- **Expect:** the agent-reviewed check is **never** promoted to a blocker; its evidence reuses only on a matching
  `CacheKey`; no exit-code or verdict change.

## Scenario 5 — Full suite green (SC-007)
```bash
dotnet test FS.GG.Governance.sln
```
- **Expect:** green, with every existing golden byte-identical to its baseline and the two new sidecars deterministic.

## References
- Budget filter: [contracts/budget-filter.md](./contracts/budget-filter.md)
- Kinded runs + provenance: [contracts/kinded-run-recording.md](./contracts/kinded-run-recording.md)
- Sidecars: [contracts/sidecars.md](./contracts/sidecars.md)
- Findings fold: [contracts/findings-fold.md](./contracts/findings-fold.md)
- Surface + byte-identity anchor: [contracts/host-surface.md](./contracts/host-surface.md)
- Host-edge glue & grown state: [data-model.md](./data-model.md)
