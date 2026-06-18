# Quickstart & Validation: Evidence Model & Synthetic Taint (F05 · `005-evidence-model`)

A run/validation guide that proves the evidence model works end-to-end **through its public
surface alone** (SC-009). It references the contract and data model rather than restating them:
- Public surface: [`contracts/Evidence.fsi`](./contracts/Evidence.fsi)
- `build`/`effective` rules & invariants: [`data-model.md`](./data-model.md)
- Engineering decisions: [`research.md`](./research.md)

Implementation code (the `Evidence.fs` body, full test suites) belongs in `tasks.md` and the
implement phase, not here.

## Prerequisites

- .NET SDK `net10.0` (`dotnet --version` → `10.x`).
- **No new dependencies** (SC-009). The kernel assembly stays BCL+FSharp.Core; F05 uses only
  `Map`/`Set`/`List` (no `System.*` at all — lighter than F03/F04, which used `SHA256`). The
  test project reuses Expecto + FsCheck already pinned at F01.

## What this feature changes

```text
src/FS.GG.Governance.Kernel/
  FS.GG.Governance.Kernel.fsproj   # add Evidence.fsi + Evidence.fs to Compile (AFTER Kernel.*)
  Evidence.fsi                     # NEW — the curated contract (from contracts/Evidence.fsi)
  Evidence.fs                      # NEW — implementation against the stable signature
tests/FS.GG.Governance.Kernel.Tests/
  FS.GG.Governance.Kernel.Tests.fsproj   # add EvidenceTests.fs to Compile (before Main.fs)
  EvidenceTests.fs                 # NEW — V21–V29
scripts/prelude.fsx                # extend with a short Evidence/effective sketch
surface/FS.GG.Governance.Kernel.surface.txt   # RE-BLESSED to include the F05 types + Evidence module
```

## FSI sketch (Principle I — do this first, before `Evidence.fs`)

Exercise the contract interactively via `scripts/prelude.fsx` (it `#r`s the built kernel and
`open`s `FS.GG.Governance.Kernel`). Use a plain `string` as the node `'id` so the graph is
real and domain-neutral. A representative session:

1. **Build a small DAG.** One synthetic root with a chain of real nodes resting on it:
   ```fsharp
   let g =
       Evidence.build
           [ "data", Synthetic        // the root cause: only simulated data
             "analysis", Real         // rests on data
             "report", Real ]         // rests on analysis
           [ "analysis", "data"
             "report", "analysis" ]
   // g : Result<EvidenceGraph<string>, GraphError<string>>  →  Ok …
   ```
2. **Compute effective states — taint flows transitively.**
   ```fsharp
   g |> Result.map Evidence.effective
   // Ok (map [ "data", Synthetic; "analysis", AutoSynthetic; "report", AutoSynthetic ])
   ```
   `data` stays `Synthetic` (root cause); `analysis` and `report` — both `Real` — are
   `AutoSynthetic`, the taint reaching the full chain depth (US1, SC-001/002).
3. **Auto-clear by upgrading the root.** Re-declare `data` as `Real` and recompute — the taint
   is gone everywhere, with no other change (US2, SC-003):
   ```fsharp
   Evidence.build [ "data", Real; "analysis", Real; "report", Real ]
                  [ "analysis", "data"; "report", "analysis" ]
   |> Result.map Evidence.effective
   // Ok (map [ "data", Real; "analysis", Real; "report", Real ])
   ```
4. **The guardrails — `build` refusals.** A cycle is refused; a node declared `AutoSynthetic`
   is refused; an edge to an undeclared node is refused (US3, SC-005/006):
   ```fsharp
   Evidence.build [ "a", Real ] [ "a", "a" ]            // Error (Cycle [ "a" ])
   Evidence.build [ "x", AutoSynthetic ] []             // Error (AutoSyntheticDeclared "x")
   Evidence.build [ "a", Real ] [ "a", "ghost" ]        // Error (UnknownNode "ghost")
   ```
5. **Non-real states are inert; synthetic outranks inheritance.** A `Failed`/`Pending`/`Skipped`
   node on synthetic evidence keeps its state; a node both declared `Synthetic` and resting on
   another synthetic is reported `Synthetic`, not `AutoSynthetic` (US4, SC-006/007):
   ```fsharp
   Evidence.build [ "root", Synthetic; "f", Failed; "s2", Synthetic ]
                  [ "f", "root"; "s2", "root" ]
   |> Result.map Evidence.effective
   // Ok (map [ "root", Synthetic; "f", Failed; "s2", Synthetic ])
   ```
6. **Domain-neutral.** The same model over a research scenario — a `Real` finding resting on a
   `Synthetic` "simulated data" node is `AutoSynthetic` (US4 AS3): exactly step 1–2 with
   `'id = string` naming findings instead of build artifacts.

If the shape is awkward in FSI, fix `Evidence.fsi` before writing `Evidence.fs` (FSI is the
honest audience).

## Validation scenarios (each maps to spec acceptance criteria)

Run with `dotnet test` (or `dotnet run` for the Expecto runner). Expected outcomes:

| # | Scenario | Asserts | Spec ref |
|---|----------|---------|----------|
| V21 | one `Synthetic` root + chain of `Real` nodes; and a graph with no `Synthetic` anywhere | every `Real` descendant ⇒ `AutoSynthetic`, root stays `Synthetic`; no-synthetic graph ⇒ effective = declared everywhere | US1 AS1–3, FR-005/006, SC-001 |
| V22 | **Property** (FsCheck): chain of N `Real` nodes rooted at one `Synthetic`, arbitrary N | all N descendants ⇒ `AutoSynthetic` (taint reaches full depth) | US1 AS2, FR-006, SC-002 |
| V23 | upgrade the sole `Synthetic` root to `Real` and recompute; and a two-synthetic-root graph, upgrade one | all formerly-tainted ⇒ `Real`; with two roots, only nodes resting solely on the upgraded root clear | US2 AS1–3, FR-009, SC-003 |
| V24 | diamond: a `Real` node reaching one `Synthetic` root by two paths | reported `AutoSynthetic` once (idempotent, order-independent) | US1 AS4, FR-005, SC-001 |
| V25 | **Property** (FsCheck): permute `nodes` and `dependencies` of the same graph | `effective` map is identical across all permutations (deterministic least-fixed-point) | FR-010, SC-004 |
| V26 | `build` over a self-dependency, a multi-node cycle, an `AutoSynthetic` declaration, and an undeclared dependency endpoint; and an acyclic graph | `Error (Cycle …)` / `Error (AutoSyntheticDeclared …)` / `Error (UnknownNode …)`; acyclic ⇒ `Ok` and `effective` computes | US3 AS1–3, FR-002/004, SC-005/006 |
| V27 | `Pending`/`Failed`/`Skipped` nodes on a synthetic dependency; a node declared `Synthetic` also depending on a synthetic node | non-real states unchanged (no taint); declared `Synthetic` ⇒ `Synthetic`, not `AutoSynthetic` | US4 AS1–2, FR-007/008, SC-006/007 |
| V28 | empty graph; a `Real` node with no deps; `effective` over every prior graph | empty graph ⇒ empty map; lone `Real` ⇒ `Real`; nothing throws or returns a partial (totality) | Edge cases, FR-011, SC-008 |
| V29 | build from `nodes`/`dependencies` with a duplicate id (last wins), a duplicate edge, and unsorted input | `nodes`/`dependencies` return de-duplicated pairs/edges ordered by id (accessors order-free & history-free) | FR-003, data-model §accessors, INV-13 |

V21–V29 use **real** `EvidenceGraph` values built from real declared states (Principle V) — no
synthetic fixtures are required for F05 (the inputs ARE declared-state graphs). The existing
**V11 surface-drift** test re-blesses to include the F05 surface, and the existing **V12
dependency-hygiene** test re-confirms the kernel still references only the BCL + FSharp.Core
after `Evidence.*` is added (F05 introduces no `System.*` reference at all).

## Done-when (feature exit criteria — roadmap §F05)

- [X] `dotnet build` clean; `dotnet test` green: F05's new validation scenarios plus the
      inherited surface-drift and dependency-hygiene tests.
- [X] `Evidence.fsi` matches `contracts/Evidence.fsi`; `Evidence.fs` has no
      `private`/`internal`/`public` on top-level bindings; `EvidenceGraph<'id>` is abstract.
- [X] `surface/FS.GG.Governance.Kernel.surface.txt` re-blessed (`BLESS_SURFACE=1 dotnet test`)
      to include the F05 types + the `Evidence` module, and committed.
- [X] Kernel assembly still carries zero heavy dependencies (V12 / SC-009).
- [X] Transitive `AutoSynthetic` flow pinned (SC-001/002); auto-clear on `Synthetic → Real`
      pinned (SC-003); determinism across permutations pinned (SC-004); cycle + `AutoSynthetic`
      + `UnknownNode` rejection pinned (SC-005/006); real-only inertness pinned (SC-007);
      totality incl. the empty graph pinned (SC-008). **Reinforces decision #4** (DAG only).
- [X] No I/O and no real-artifact reads in the kernel (FR-013) — F05 is a pure derivation over
      declared states.
- [X] No packing yet — the kernel still packs at F06.
