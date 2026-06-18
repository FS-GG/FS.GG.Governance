# Quickstart & Validation: Check — The Reified Rule Algebra (F03 · `003-check-algebra`)

A run/validation guide that proves the `Check` algebra works end-to-end **through its
public surface alone** (SC-007). It references the contract and data model rather than
restating them:
- Public surface: [`contracts/Check.fsi`](./contracts/Check.fsi)
- Fold rules, hash canonicalization & invariants: [`data-model.md`](./data-model.md)
- Engineering decisions: [`research.md`](./research.md)

Implementation code (the `Check.fs` body, full test suites) belongs in `tasks.md` and the
implement phase, not here.

## Prerequisites

- .NET SDK `net10.0` (`dotnet --version` → `10.x`).
- **No new dependencies** (SC-008). The kernel assembly stays BCL+FSharp.Core (SHA-256 is
  `System.Security.Cryptography`, allowed by V12); the test project reuses Expecto +
  FsCheck already pinned at F01. `eval`/`explain` reuse the in-assembly F02 `Verdict`
  module.

## What this feature changes

```text
src/FS.GG.Governance.Kernel/
  FS.GG.Governance.Kernel.fsproj   # add Check.fsi + Check.fs to the Compile list (AFTER Kernel.*)
  Check.fsi                        # NEW — the curated contract (from contracts/Check.fsi)
  Check.fs                         # NEW — implementation against the stable signature
tests/FS.GG.Governance.Kernel.Tests/
  FS.GG.Governance.Kernel.Tests.fsproj   # add CheckTests.fs to the Compile list (before Main.fs)
  CheckTests.fs                    # NEW — V1–V11
scripts/prelude.fsx                # extend with a short Check sketch
surface/FS.GG.Governance.Kernel.surface.txt   # RE-BLESSED to include the F03 types + Check/Explanation modules
```

## FSI sketch (Principle I — do this first, before `Check.fs`)

Exercise the contract interactively via `scripts/prelude.fsx` (it `#r`s the built kernel
and `open`s `FS.GG.Governance.Kernel`; `open Check` brings the `==>`/`.&`/`.|` operators
into infix scope). A representative session, using a toy `'fact` of `string`:

1. Build two probes by hand from the smart constructors — one that reads an artifact and
   reports `Met`, one that reports `Unknown "agent has not reviewed tone"`:
   `let contrast = Check.probe "contrastRatio" [ { Kind = "token"; Key = "text" } ] [ NumberArg 4.5 ] (fun _ -> Met)`
   and `let tone = Check.probe "toneIsProfessional" [] [] (fun _ -> Unknown "not reviewed")`.
2. Compose a check that reads like its sentence: `let chk = contrast .& tone`
   (= `All [contrast; tone]`), and an implication `let imp = contrast ==> tone`.
3. **Evaluate** (the only fold that needs facts): `Check.eval [] chk` → `Uncertain "not
   reviewed"` (an undecided clause survives a conjunction of otherwise-passing clauses —
   the whole point, inherited from F02).
4. **Render without facts**: `Check.render chk` → a readable string such as
   `all of [contrastRatio(token:text, 4.5); toneIsProfessional]` — note **no `Eval` ran**.
5. **Hash** and confirm commutative canonicalization:
   `Check.hash (All [contrast; tone]) = Check.hash (All [tone; contrast])` → `true`;
   but `Check.hash (contrast ==> tone) <> Check.hash (tone ==> contrast)` → `true`
   (implication is positional).
6. **Explain** and confirm the cross-fold agreement:
   `Explanation.verdict (Check.explain [] chk) = Check.eval [] chk` → `true`.
7. **Reads / reified-ness** (structural, no facts): `Check.reads chk` → `[ { Kind =
   "token"; Key = "text" } ]`; `Check.isReified chk` → `true`; wrap an `Opaque` node in
   and confirm `Check.isReified` flips to `false`.
8. **Never-executes proof**: build `let boom = Check.probe "boom" [] [] (fun _ -> failwith
   "executed")`. `Check.render boom` and `Check.hash boom` succeed; only `Check.eval []
   boom` throws — the inspectable-without-execution guarantee, shown with a real probe.

If the shape is awkward in FSI, fix `Check.fsi` before writing `Check.fs` (FSI is the
honest audience).

## Validation scenarios (each maps to spec acceptance criteria)

Run with `dotnet test` (or `dotnet run` for the Expecto runner). Expected outcomes:

| # | Scenario | Asserts | Spec ref |
|---|----------|---------|----------|
| V1 | `eval` atom for each outcome; `All`/`Any`/`Not` over mixed met/unmet/unknown | verdict matches Kleene (Fail dominates `All`; Pass dominates `Any`; `Uncertain` survives) | US1 AS1–4, FR-006 |
| V2 | `eval` of `a ==> b` vs `eval` of `Any [Not a; b]` | identical for all a,b (desugaring) | US1 AS5, FR-006 |
| V3 | `eval` of an `Opaque` node | maps its function's outcome (met→Pass, unmet→Fail, unknown→Uncertain) | US1 AS6, FR-006 |
| V4 | **never-executes**: probe whose `Eval` throws | `render`/`hash`/`reads`/`isReified` succeed; only `eval` throws | US2 AS1, FR-007/008, SC-001 |
| V5 | `render` of a composed check (no facts) | deterministic readable string; re-render identical | US2 AS1, FR-007 |
| V6 | **Property** (FsCheck): permute members of `All`/`Any` | `hash` identical every permutation | US2 AS3, FR-008, SC-002 |
| V7 | `hash (a ==> b)` vs `(b ==> a)`; probe with reordered `Args` | hashes **differ** (positional) | US2 AS4/AS5, FR-008, SC-002 |
| V8 | re-hash identical check; `Opaque` hashed twice | identical key; `Opaque` key depends on name only | US2 AS2/AS6, FR-008 |
| V9 | **Property** (FsCheck): any check + any facts | `Explanation.verdict (explain f c) = eval f c` | US3 AS1, FR-009, SC-004 |
| V10 | `explain` of a multi-level check | structure mirrors the check; each atom records its met/unmet/unknown outcome | US3 AS2, FR-009 |
| V11 | `isReified` with/without an `Opaque`; `reads` over declaring probes | false iff `Opaque` present; reads = exactly the declared `ArtifactRef`s (`Opaque` adds none) | US4 AS1–3, FR-010/011, SC-005 |
| V12 | empty `All`/`Any` and every combinator mix through all six interpreters | totality — none throws or returns partial; `All [] → Pass`, `Any [] → Fail ""` | edges, FR-013, SC-006 |

V1–V12 use **real** `Check` values and real probes (Principle V) — no synthetic fixtures
are required for F03. The existing **V11 surface-drift** test re-blesses to include the
F03 surface, and the existing **V12 dependency-hygiene** test re-confirms the kernel still
references only the BCL + FSharp.Core after `Check.*` is added (SHA-256 is `System.*`).

## Done-when (feature exit criteria — roadmap §F03)

- [ ] `dotnet build` clean; `dotnet test` green: F03's new validation scenarios plus the
      inherited surface-drift and dependency-hygiene tests.
- [ ] `Check.fsi` matches `contracts/Check.fsi`; `Check.fs` has no
      `private`/`internal`/`public` on top-level bindings.
- [ ] `surface/FS.GG.Governance.Kernel.surface.txt` re-blessed (`BLESS_SURFACE=1 dotnet
      test`) to include the F03 types + the `Check`/`Explanation` modules, and committed.
- [ ] Kernel assembly still carries zero heavy dependencies (V12 / SC-008).
- [ ] `render`/`hash`/`reads`/`isReified` proven execution-free by a throwing-`Eval` probe
      test (SC-001); `hash` commutative-canonicalization + positionality pinned (SC-002);
      `explain` verdict = `eval` verdict pinned (SC-004).
- [ ] No packing yet — the kernel still packs at F06.
