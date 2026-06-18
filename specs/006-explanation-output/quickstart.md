# Quickstart — Explanation Output, Contract & Freshness (F06 · 006-explanation-output)

A validation/run guide: an FSI sketch of the public surface (the Principle I design pass) and
the runnable scenarios (V31–V39) that prove the feature works end-to-end. Implementation
bodies belong in `tasks.md`/the `.fs` files, not here. Surface details are in
[`contracts/`](./contracts/); behaviour in [`data-model.md`](./data-model.md).

## Prerequisites
- .NET SDK with `net10.0` (per `Directory.Build.props`).
- The kernel builds: `dotnet build src/FS.GG.Governance.Kernel`.
- F03 (`Explanation`/`Check.render`/`Check.eval`), F04 (`CheckRule`), F05
  (`EvidenceState`/`effective`) already merged.

## FSI design pass (extend `scripts/prelude.fsx`)

Exercise the intended surface interactively BEFORE writing the `.fs` bodies (Principle I):

```fsharp
open FS.GG.Governance.Kernel
open FS.GG.Governance.Kernel.Check   // operators .&, .|, ==>

// ── JSON explanation ──────────────────────────────────────────────
let met name = Check.probe name [] [] (fun _ -> Met)
let chk = (met "has-tests") .& (met "has-docs")
let expl = Check.explain [] chk
let j = Json.ofExplanation expl
// j mirrors the tree; root verdict = Check.eval [] chk
Json.toExplanation j = expl          // round-trip: true
Json.ofExplanation expl = Json.ofExplanation expl   // deterministic: true

// ── Drift-proof contract ──────────────────────────────────────────
let spec = { Document = "constitution.md"; Section = "V" }
let r =
    CheckRule.rule (RuleId "tests-present") Deterministic spec chk
    |> Result.map CheckRule.blocking
match r with
| Ok rule ->
    let contract = Contract.ofRules [ rule ]
    // each entry.Statement = Check.render rule.Check  (cannot drift)
    (List.head contract).Statement = Check.render rule.Check    // true
    printfn "%s" (Contract.render contract)
    Json.toContract (Json.ofContract contract) = contract       // round-trip: true
| Error e -> eprintfn "%A" e

// ── Evidence freshness (pure over supplied instants) ──────────────
Freshness.decide 10 [ 9 ]        // Fresh   (recorded after change)
Freshness.decide 10 [ 10 ]       // Fresh   (inclusive tie)
Freshness.decide 10 [ 11 ]       // Stale   (artifact changed after)
Freshness.decide 10 []           // Fresh   (covers nothing)
Freshness.decide 10 [ 3; 10; 7 ] // Fresh   (>= max covered)

// ── Evidence states in the same report ────────────────────────────
Json.ofEvidenceState AutoSynthetic                 // "autoSynthetic"
let g = Evidence.build [ "a", Real; "b", Synthetic ] [ "a", "b" ]
        |> function Ok g -> g | Error e -> failwithf "%A" e   // a tainted -> AutoSynthetic
let eff = Evidence.effective (g)
Json.ofEffective id eff                              // {"a":"autoSynthetic","b":"synthetic"}
```

## Validation scenarios

Run with `dotnet test` (semantic tests exercise the PUBLIC surface through the built library /
prelude — Principle I). Each maps to spec acceptance criteria + success criteria.

### JSON explanation (US1)
- **V31 — mirror shape & root verdict.** Build a check of each shape (`Atom`, `All`, `Any`,
  `Not`, `Implies`, `Opaque`), `explain` then `ofExplanation`; assert the JSON mirrors the
  tree, each atomic node records its probe name + met/unmet/unknown outcome, every node carries
  a verdict, and the root verdict equals `Check.eval`. (SC-001, FR-001)
- **V32 — determinism.** Serialize the same explanation twice; assert byte-for-byte identical
  output. (SC-002, FR-003)
- **V33 — round-trip (FsCheck).** For arbitrary generated explanations,
  `toExplanation (ofExplanation e) = e`. (SC-003, FR-004)
- **V34 — opaque, no probe.** An explanation with an `OpaqueExplained` node serializes by name
  + recorded outcome only; assert no probe `Eval` is invoked during serialization (a probe
  whose `Eval` throws still serializes fine). (SC-004, FR-002)

### Drift-proof contract (US2)
- **V35 — one entry per rule, drift-proof.** Fold a small catalog; assert each entry carries
  id/severity/spec and `Statement = Check.render`; mutate a rule's check and assert its entry
  changes; reorder the catalog and assert each rule's own entry is unchanged. (SC-005, FR-005/006)
- **V36 — total + round-trip.** `Contract.ofRules [] = []`; `ofRules` is deterministic; the
  contract JSON round-trips (`toContract (ofContract c) = c`). (SC-006, FR-007)

### Evidence freshness (US3)
- **V37 — inclusive boundary & multi-artifact.** `decide T [T-1] = Fresh`; `decide T [T+1] =
  Stale`; `decide T [T] = Fresh`; `decide T [] = Fresh`; multi-artifact fresh iff `recorded ≥`
  the latest covered instant. (SC-007, FR-008/009)
- **V38 — purity (FsCheck).** For arbitrary `recorded`/`covered`, `decide` is a pure function
  of the instants — equal inputs give equal results; `isFresh a b = (decide a b = Fresh)`.
  (SC-008, FR-010)

### Evidence states in the report (US4)
- **V39 — six tokens + effective map round-trip.** Each `EvidenceState` serializes to a
  distinct stable token that round-trips; serialize an effective-state map over a tainted graph
  (F05) with a projection and assert every node (incl. `AutoSynthetic`) is present and the JSON
  round-trips to the equal projected map. (FR-011, SC-003)

### Surface & dependency hygiene (cross-cutting)
- **V11 (re-blessed).** After the F06 types/modules land, regenerate the baseline with
  `BLESS_SURFACE=1 dotnet test`; the committed `surface/FS.GG.Governance.Kernel.surface.txt`
  now includes `Freshness`, `ContractEntry`/`Contract`, and `Json`. (FR-014)
- **V12 (unchanged).** Kernel still references only BCL/`System.*` + FSharp.Core —
  `System.Text.Json` satisfies it; zero `PackageReference` added. (SC-009)

## M1 exit (tasks-level)
Completing F06 completes **Milestone M1**. The exit action — packing
`FS.GG.Governance.Kernel` to `~/.local/share/nuget-local/` — is a `tasks.md` step (research
D8), not part of this feature's code surface.

## Expected outcome
`dotnet test` green (existing 55 + the new F06 tests); `surface.txt` re-blessed; zero new
dependency; the kernel emits round-trippable JSON explanations, a drift-proof contract, and
freshness verdicts — the first useful product.
