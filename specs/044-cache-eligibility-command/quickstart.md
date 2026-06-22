# Quickstart: Cache-Eligibility Host Command

**Feature**: `044-cache-eligibility-command` | **Date**: 2026-06-22

A validation/run guide for `FS.GG.Governance.CacheEligibilityCommand`. Details live in
[data-model.md](./data-model.md) and [contracts/](./contracts/); this is how to build, exercise, test, and run.

## Prerequisites

- .NET `net10.0` SDK (repo standard; `Directory.Build.props` sets `Nullable=enable`,
  `TreatWarningsAsErrors=true`).
- The merged cores are already in the solution (F014–F043). No new third-party dependency is added.

## 1. Design-first in FSI (Principle I)

Append an **F044** section to `scripts/prelude.fsx` that drafts the `Loop` surface and exercises the pipeline
over **real** values before any `.fs` body exists — build a couple of real F018 `Gate`s, a `SensedFacts`
bundle (one gate fully sensed, one missing a fact), and show:

```fsharp
// resolve → candidate → evaluate → ofReport, plus the unresolved sidecar render
let report   = FreshnessResolution.resolve gates sensed
let cands    = FreshnessResolution.entries report |> List.choose FreshnessResolution.candidate
let cacheRpt = CacheEligibility.evaluate cands EvidenceReuse.empty
let cacheJson = CacheEligibilityJson.ofReport cacheRpt          // fsgg.cache-eligibility/v1
let unresolved =
    FreshnessResolution.entries report
    |> List.choose (fun e -> match e.Outcome with
                             | Unresolved facts -> Some (Gates.gateIdValue e.Gate, facts |> List.map FreshnessResolution.missingFactToken)
                             | _ -> None)
```

Confirm the shapes compose without adaptation (F043 `candidate` → F041 `evaluate` → F042 `ofReport`).

## 2. Build

```bash
dotnet build FS.GG.Governance.sln
```

The two new projects (`src/FS.GG.Governance.CacheEligibilityCommand`,
`tests/FS.GG.Governance.CacheEligibilityCommand.Tests`) compile `Loop.fsi → Loop.fs → Interpreter.fsi →
Interpreter.fs → Program.fs` and the test files.

## 3. Test (Expecto + FsCheck; three tiers, Principle V)

```bash
dotnet test tests/FS.GG.Governance.CacheEligibilityCommand.Tests/FS.GG.Governance.CacheEligibilityCommand.Tests.fsproj
```

Scenarios to expect green (cross-referenced to the spec):

| Test file | Proves | Spec |
|---|---|---|
| `ParseTests` | verb + flags parse; usage errors are values (exit 2) | C1/C2 |
| `LoopTests` | pure init/update: selection → sense → resolve → evaluate → project; `cache-eligibility.json` = `ofReport` | US1, SC-001/SC-002 |
| `UnresolvedTests` | a missing sensed fact → sidecar names exactly the missing facts; never reusable | US2, SC-003 |
| `SensedEmptyTests` | sensed-empty covered set resolves; absent command ⇒ absent command version | US2 edge, SC-005 |
| `InterpreterTests` | faked ports: written file = genuine `ofReport`; absent store ⇒ empty ⇒ `noPriorEvidence` | US1.2, FR-006 |
| `DeterminismTests` | byte-identical artifacts across input order / cwd; GateId order | US3, SC-004 |
| `FailureTests` | no/invalid catalog, not-a-git-repo, unwritable output → non-zero, no partial artifact | Edge, FR-010 |
| `ExitInformationTests` | all-must-recompute or some-unresolved ⇒ exit 0 | FR-009, SC-006 |
| `EndToEndTests` | real temp git + real catalog + `realPorts`; schema-valid + byte-identical re-run | SC-004 |
| `SurfaceDriftTests` | the committed public surface + additive-only reference scope | SC-007/SC-008 |

The `FreshnessSensor` and `git` port are **faked** in the unit/interpreter tiers (a real hash/`git` is a
non-reproducible oracle) — disclosed with the `Synthetic` token at the use site and in the PR; the real path is
proven once in `EndToEndTests`. Catalog/store/artifact I/O is real filesystem at the edge.

## 4. Re-bless the surface baseline (Tier-1)

When the public surface intentionally changes:

```bash
BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.CacheEligibilityCommand.Tests/…  # regenerates surface/FS.GG.Governance.CacheEligibilityCommand.surface.txt
```

Commit the regenerated `surface/FS.GG.Governance.CacheEligibilityCommand.surface.txt`.

## 5. Run the packed verb

```bash
# from a repo with a .fsgg catalog and some changed files
fsgg cache-eligibility --repo . --since HEAD~1 --format human
#   → writes cache-eligibility.json (resolved verdicts, fsgg.cache-eligibility/v1)
#   → writes cache-eligibility.unresolved.json (unresolved gates + named missing facts)
#   → prints the reusable / must-recompute / recompute-by-default summary; exits 0

fsgg cache-eligibility --repo . --store readiness/reuse-store --out readiness/cache-eligibility.json --format json
```

Validation checkpoints: `cache-eligibility.json` validates against `fsgg.cache-eligibility/v1` and lists
exactly the resolved selected gates in `GateId` order; `cache-eligibility.unresolved.json` (always present)
names any unresolved gates with exactly their missing facts; both are byte-identical on a re-run from a
different working directory; the command exits 0 even when every gate must recompute or some are unresolved, and
non-zero only on a genuine sensing/catalog/store/write failure (writing no partial artifact then).
