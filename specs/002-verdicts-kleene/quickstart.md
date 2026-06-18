# Quickstart & Validation: Verdicts — Kleene Composition (F02 · `002-verdicts-kleene`)

A run/validation guide that proves the verdict algebra works end-to-end **through its
public surface alone** (SC-004). It references the contract and data model rather than
restating them:
- Public surface: [`contracts/Verdict.fsi`](./contracts/Verdict.fsi)
- Truth tables, reason rule & invariants: [`data-model.md`](./data-model.md)
- Engineering decisions: [`research.md`](./research.md)

Implementation code (the `Verdict.fs` body, full test suites) belongs in `tasks.md`
and the implement phase, not here.

## Prerequisites

- .NET SDK `net10.0` (`dotnet --version` → `10.x`).
- **No new dependencies** (SC-005). The kernel assembly stays BCL+FSharp.Core; the
  test project reuses the Expecto + FsCheck already pinned at F01.

## What this feature changes

```text
src/FS.GG.Governance.Kernel/
  FS.GG.Governance.Kernel.fsproj   # add Verdict.fsi + Verdict.fs to the Compile list (before Kernel.*)
  Verdict.fsi                      # NEW — the curated contract (from contracts/Verdict.fsi)
  Verdict.fs                       # NEW — implementation against the stable signature
tests/FS.GG.Governance.Kernel.Tests/
  FS.GG.Governance.Kernel.Tests.fsproj   # add VerdictTests.fs to the Compile list (before Main.fs)
  VerdictTests.fs                  # NEW — V1–V10
scripts/prelude.fsx                # extend with a short Verdict sketch
surface/FS.GG.Governance.Kernel.surface.txt   # RE-BLESSED to include Verdict + the Verdict module
```

## FSI sketch (Principle I — do this first, before `Verdict.fs`)

Exercise the contract interactively via `scripts/prelude.fsx` (it `#r`s the built
kernel and `open`s `FS.GG.Governance.Kernel`). A representative session:

1. Construct the three kinds: `Pass`, `Fail "spacing 6px off-scale"`,
   `Uncertain "agent has not reviewed tone"`.
2. `Verdict.all [ Pass; Uncertain "…"; Pass ]` → `Uncertain "…"` (an undecided clause
   survives a conjunction of otherwise-passing clauses — the whole point).
3. `Verdict.all [ Fail "a"; Uncertain "b" ]` → `Fail "a"` (a definite fail dominates,
   even with an undecided sibling).
4. `Verdict.any [ Fail "a"; Pass ]` → `Pass`; `Verdict.any [ Fail "a"; Uncertain "b" ]`
   → `Uncertain "b"`.
5. Shuffle and re-nest the same multiset and confirm the result — **including the
   reason string** — is byte-for-byte identical:
   `Verdict.all [ Fail "a"; Fail "z" ] = Verdict.all [ Fail "z"; Fail "a" ]` and
   `Verdict.all [ Verdict.all [ Fail "a"; Fail "z" ]; Fail "m" ] =
    Verdict.all [ Fail "a"; Fail "z"; Fail "m" ]` (both `Fail "a; m; z"`).
6. `Verdict.negate (Fail "x") = Pass`; `Verdict.negate Pass = Fail ""`;
   `Verdict.negate (Uncertain "y") = Uncertain "y"`.
7. Identities: `Verdict.all [] = Pass`; `Verdict.any [] = Fail ""`.

If the shape is awkward in FSI, fix `Verdict.fsi` before writing `Verdict.fs` (FSI is
the honest audience).

## Validation scenarios (each maps to spec acceptance criteria)

Run with `dotnet test` (or `dotnet run` for the Expecto runner). Expected outcomes:

| # | Scenario | Asserts | Spec ref |
|---|----------|---------|----------|
| V1 | `all` with ≥1 `Fail` among undecided/pass siblings | result is `Fail` (dominates) | US1 AS1, FR-002 |
| V2 | `all` with no `Fail` but ≥1 `Uncertain` | result is `Uncertain`, not `Pass` | US1 AS2, FR-002/007 |
| V3 | `any` with ≥1 `Pass` among undecided/fail siblings | result is `Pass` (dominates) | US1 AS3, FR-003 |
| V4 | `any` with no `Pass` but ≥1 `Uncertain` | result is `Uncertain`, not `Fail` | US1 AS4, FR-003/007 |
| V5 | all-`Pass` under `all`; all-`Fail` under `any` | `Pass`; `Fail` respectively | US1 AS5 |
| V6 | **Property** (FsCheck): permute the input list | identical verdict **and** reason every order | US2 AS1/AS2, FR-005/006, SC-001 |
| V7 | **Property** (FsCheck): re-nest the same multiset (`all [all xs; ys]` vs `all (xs@ys)`) | identical verdict **and** reason (associativity) | US2 AS3, FR-005/006 |
| V8 | duplicate / shuffled identical reasons in a dominating combination | combined reason is dedup'd + ordinal-sorted, position-independent | US2 AS4, edge "reason determinism under duplication" |
| V9 | `negate` on each kind; and twice | `Pass`↔`Fail` tags swap, `Uncertain` fixed; double-negate recovers tags | US3 AS1–AS3, FR-004 |
| V10 | empty + single-element combinations | `all []=Pass`, `any []=Fail ""`, `all [v]=any [v]=v` | edges, FR-008/009 |
| V11 | Reflect over the built kernel assembly (existing test) | public surface (now incl. `Verdict` + module) equals the re-blessed baseline | FR-011, Principle II |

V1–V10 use **real** verdict values (Principle V) — no synthetic fixtures are required
for F02. The existing **V12** dependency-hygiene test re-confirms the kernel still
references only the BCL + FSharp.Core (FR-010, SC-005) after `Verdict.*` is added.

## Done-when (feature exit criteria — roadmap §F02)

- [X] `dotnet build` clean; `dotnet test` green: F02's new V1–V10 plus the inherited
      surface-drift (V11) and dependency-hygiene (V12) tests — these last two are F01's
      existing tests, re-run here to confirm they still pass after `Verdict.*` is added.
- [X] `Verdict.fsi` matches `contracts/Verdict.fsi`; `Verdict.fs` has no
      `private`/`internal`/`public` on top-level bindings.
- [X] `surface/FS.GG.Governance.Kernel.surface.txt` re-blessed (`BLESS_SURFACE=1
      dotnet test`) to include `Verdict` + the `Verdict` module, and committed.
- [X] Kernel assembly still carries zero heavy dependencies (V12 / SC-005).
- [X] Kleene "strong" truth tables and the reason-aggregation rendering (reserved
      `"; "` separator → split/dedup/ordinal-sort/join) documented and pinned by tests.
- [X] No packing yet — the kernel still packs at F06.
