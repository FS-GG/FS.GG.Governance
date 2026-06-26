# Contract — Migration acceptance (behaviour-preserving)

The "contract" this feature exposes is not a runtime API but the **acceptance invariants** the
migration must satisfy at every committed step. These are machine-checkable and mirror how
Phase A (feature 073) was accepted.

## Per-commit gate (every migration commit MUST pass all)

| # | Invariant | Check | Spec |
|---|---|---|---|
| C1 | Full test suite green | `dotnet test FS.GG.Governance.sln` exits 0 | FR-007, SC-006 |
| C2 | Per-project test count unchanged for every **migrated** project | compare per-project counts vs. the captured pre-migration baseline | FR-004, SC-001 |
| C3 | Every golden and snapshot fixture byte-identical | `git diff --stat` shows **no** change under any `*.Tests` golden/snapshot fixture | FR-004, SC-002 |
| C4 | Each migrated helper has exactly one compiled definition for that project | the local copy is deleted in the **same** commit that adds the `ProjectReference` (no project compiles with both) | FR-003, SC-004 |
| C5 | `FS.GG.Governance.Tests.Common` referenced by **no** `src` project | scope-guard test: no `src/*.fsproj` references the library | FR-008 |
| C6 | The library's public surface matches its blessed baseline | `SurfaceBaselineTests` vs. `surface/FS.GG.Governance.Tests.Common.surface.txt` | FR-009 |

## Whole-feature acceptance (after the sweep)

| # | Invariant | Spec |
|---|---|---|
| A1 | Total test count == pre-migration baseline **+** only the additive `Tests.Common.Tests` project (like Phase A's `2237 → 2259`) | SC-001, research D3 |
| A2 | Every golden/snapshot byte-identical end-to-end | SC-002 |
| A3 | Net test-support LOC reduced by ≥ ~1,000 (target; up to ~3,500) | SC-003 |
| A4 | `findRepoRoot` and the real-`git` helper each have exactly one shared definition (plus any explicitly-documented per-project variant) | SC-004, FR-010 |
| A5 | A change to any consolidated helper requires editing exactly **one** file to take effect across consumers | SC-005 |

## Baseline capture (prerequisite, before any migration)

Record the authoritative pre-migration signal so C2/A1 are decidable:

```bash
# Per-project test counts (the C2/A1 baseline)
dotnet test FS.GG.Governance.sln --logger "trx" ...   # or the repo's standard count harness
# Golden/snapshot fixtures are pinned by git itself: any byte change shows in `git diff`.
```

## Divergence handling (NOT acceptance failures — design choices)

- A fixture/helper that is **not** byte-identical across projects is an **intentional variant**:
  keep it **local**, do not force it into `Tests.Common` (FR-006, spec Edge Cases). C3 drift on
  a moved helper is the signal that it was not actually shared — revert the move, keep local.
- A project with **no** `Support.fs` (10 of 78) is **out of scope** for deletion; it may opt
  into referencing the library but is not required to.
