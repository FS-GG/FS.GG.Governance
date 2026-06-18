# Quickstart & Validation: Kernel Core (F01 · `001-kernel-core`)

A run/validation guide that proves the kernel works end-to-end **through its public
surface alone** (SC-004). It references the contract and data model rather than
restating them:
- Public surface: [`contracts/Kernel.fsi`](./contracts/Kernel.fsi)
- Entity meanings & behavioral contract: [`data-model.md`](./data-model.md)
- Engineering decisions: [`research.md`](./research.md)

Implementation code (the `Kernel.fs` body, full test suites) belongs in `tasks.md`
and the implement phase, not here.

## Prerequisites

- .NET SDK `net10.0` (`dotnet --version` → `10.x`).
- No other dependencies for the kernel itself (SC-005). The test project adds
  Expecto + FsCheck (D5) — test-only, the kernel stays BCL-only.

## Project layout this feature creates

```text
src/FS.GG.Governance.Kernel/
  FS.GG.Governance.Kernel.fsproj
  Kernel.fsi                       # the curated contract (from contracts/Kernel.fsi)
  Kernel.fs                        # implementation against the stable signature
tests/FS.GG.Governance.Kernel.Tests/
  FS.GG.Governance.Kernel.Tests.fsproj
  FixedPointTests.fs               # derivation, provenance, order-independence, dedup
  SurfaceDriftTests.fs             # FR-011 reflective baseline check (D6)
scripts/prelude.fsx                # FSI entry: loads the built kernel, opens the namespace
surface/FS.GG.Governance.Kernel.surface.txt   # committed API surface baseline
Directory.Build.props
Directory.Packages.props
```

## FSI sketch (Principle I — do this first, before `Kernel.fs`)

Exercise the contract interactively via `scripts/prelude.fsx`, which `#r`s the built
kernel and `open`s `FS.GG.Governance.Kernel`. A representative session over a toy
`'fact` (e.g. a string or a tiny union):

1. Define `identify` for the toy fact type.
2. Supply a couple of asserted facts (each `Provenance = []`).
3. Define 2–3 chained monotonic rules (rule A's output enables rule B).
4. `FixedPoint.evaluate identify rules supplied` and inspect:
   - `result.Facts` contains the asserted facts **plus** the transitive closure;
   - a derived fact's `Provenance` names its producing `RuleId` and input `FactId`s;
   - an asserted fact's `Provenance` is `[]`;
   - `result.Rounds` equals the chain depth (D4).

If the shape is awkward in FSI, fix the `.fsi` before writing `.fs` (FSI is the
honest audience).

## Validation scenarios (each maps to spec acceptance criteria)

Run with `dotnet test` (or `dotnet run` for the Expecto runner). Expected outcomes:

| # | Scenario | Asserts | Spec ref |
|---|----------|---------|----------|
| V1 | Supply facts + chained rules (A⇒B, B⇒C); evaluate | `Facts` = supplied + correct closure, no spurious/missing entries | US1 AS1, FR-001/002 |
| V2 | Rules whose preconditions are unmet | `Facts` = exactly the supplied facts; `Rounds = 0` | US1 AS2, edge "no rules"/"unmet" |
| V3 | Bounded monotone rule set, incl. a self-referential chain | evaluation terminates (quiesces) | US1 AS3, FR-003, SC-003 |
| V4 | Multi-step derivation | a known-derived fact's `Provenance` names the rule + exact inputs | US2 AS1, FR-004, SC-002 |
| V5 | Asserted fact | its `Provenance` is `[]` | US2 AS2, FR-005 |
| V6 | Fact derivable by two chains | recorded provenance is the deterministic first-establishing step (D2) | US2 AS3 |
| V7 | **Property**: shuffle rule order N ways (FsCheck permutation) | identical `Facts` **and** identical per-fact `Provenance` every run | US3 AS1, FR-006, SC-001 |
| V8 | Two facts of the same `identify` id produced | single deduplicated entry | US3 AS2, FR-007 |
| V9 | Repeat the same evaluation twice | byte-for-byte identical results | SC-001 |
| V10 | `Rounds` on no-derivation and on a depth-2 chain | `0` and `2` respectively | FR-008, D4 |
| V11 | Reflect over the built kernel assembly | public surface equals `surface/…surface.txt` baseline | FR-011, Principle II, D6 |
| V12 | Kernel project references | no package dependencies beyond the BCL | FR-010, SC-005 |

V1–V10 use **real** facts, rules, and evaluation (Principle V) — no synthetic
fixtures are required for F01.

## Done-when (feature exit criteria — roadmap §F01)

- [ ] `dotnet build` clean; `dotnet test` green (V1–V12).
- [ ] `Kernel.fsi` matches `contracts/Kernel.fsi`; `Kernel.fs` has no
      `private`/`internal`/`public` on top-level bindings.
- [ ] `surface/FS.GG.Governance.Kernel.surface.txt` committed and enforced by V11.
- [ ] Kernel assembly carries zero heavy dependencies (V12 / SC-005).
- [ ] Decision #4 (kernel preconditions: monotonic; negated/aggregated facts
      supplied from a lower stratum) documented as a precondition (FR-012).
- [ ] No packing yet — the Kernel packs to `~/.local/share/nuget-local/` at F06 (D7).
