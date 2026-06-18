# Quickstart & Validation: CheckTier & Rule Bridge (F04 · `004-checktier-rule-bridge`)

A run/validation guide that proves the bridge works end-to-end **through its public
surface alone** (SC-007). It references the contract and data model rather than restating
them:
- Public surface: [`contracts/CheckRule.fsi`](./contracts/CheckRule.fsi)
- Constructor/`cacheKey`/`toRule` rules & invariants: [`data-model.md`](./data-model.md)
- Engineering decisions: [`research.md`](./research.md)

Implementation code (the `CheckRule.fs` body, full test suites) belongs in `tasks.md` and
the implement phase, not here.

## Prerequisites

- .NET SDK `net10.0` (`dotnet --version` → `10.x`).
- **No new dependencies** (SC-009). The kernel assembly stays BCL+FSharp.Core (`cacheKey`
  reuses the same `System.Security.Cryptography.SHA256` F03 already uses); the test project
  reuses Expecto + FsCheck already pinned at F01. `toRule` reuses the in-assembly F03
  `Check` interpreters and F01 `Rule`/`FactSet`.

## What this feature changes

```text
src/FS.GG.Governance.Kernel/
  FS.GG.Governance.Kernel.fsproj   # add CheckRule.fsi + CheckRule.fs to Compile (AFTER Check.*)
  CheckRule.fsi                    # NEW — the curated contract (from contracts/CheckRule.fsi)
  CheckRule.fs                     # NEW — implementation against the stable signature
tests/FS.GG.Governance.Kernel.Tests/
  FS.GG.Governance.Kernel.Tests.fsproj   # add CheckRuleTests.fs to Compile (before Main.fs)
  CheckRuleTests.fs                # NEW — V13–V20
scripts/prelude.fsx                # extend with a short CheckRule/toRule sketch
surface/FS.GG.Governance.Kernel.surface.txt   # RE-BLESSED to include the F04 types + CheckRule module
```

## FSI sketch (Principle I — do this first, before `CheckRule.fs`)

Exercise the contract interactively via `scripts/prelude.fsx` (it `#r`s the built kernel
and `open`s `FS.GG.Governance.Kernel`). Reuse the F03 `contrast`/`tone` probes already in
the prelude, and define a tiny in-test adapter `'fact` so the `Bridge` is real. A
representative session:

1. **A toy adapter fact + bridge.** Let the fact be either a governance outcome or an
   artifact-content fact:
   `type Gov = | GovOut of RuleOutcome | Art of kind: string * key: string * hash: string`.
   Build a real `Bridge<Gov>`:
   - `Judge = { ModelId = "claude-opus-4-8"; Version = "2026-06" }`
   - `ArtifactHash = fun facts ref -> facts |> List.tryPick (fun f -> match f.Value with Art (k, key, h) when k = ref.Kind && key = ref.Key -> Some h | _ -> None) |> Option.defaultValue ""`
   - `Embed = GovOut`
   - `Project = fun f -> match f with GovOut o -> Some o | _ -> None`
2. **Author rules and see the guardrail.** A reified check authors as `Deterministic`:
   `CheckRule.rule (RuleId "contrast") Deterministic { Document = "wcag"; Section = "1.4.3" } contrast`
   → `Ok …`. The SAME tier over an `Opaque` check is refused:
   `CheckRule.rule (RuleId "judge") Deterministic spec (Opaque ("tone", fun _ -> Met))`
   → `Error (OpaqueCannotBeDeterministic (RuleId "judge"))`. Author it as an agent rule
   instead: `CheckRule.rule (RuleId "judge") AgentReviewed spec opaqueCheck |> Result.map (CheckRule.asking "Is the tone professional?")` → `Ok …`.
3. **Cache key — decision #1.** `CheckRule.cacheKey bridge.Judge (Check.hash chk) (Check.reads chk |> List.map (bridge.ArtifactHash facts)) (Some "prompt")` returns a hex string;
   recompute with the same ingredients → identical; bump `Version` to `"2026-07"` → the key
   changes (re-review). Permuting the `artifactHashes` list → unchanged (order-independent).
4. **Bridge to a kernel rule and run it.** `let kr = CheckRule.toRule bridge agentRule`.
   `kr.Description = Check.render agentRule.Check` → `true` (no drift). Apply it with NO
   recorded review present: `kr.Apply facts` emits one `GovOut (NeedsReview { … Key = key })`
   (cache miss). Now add the fact the F08 edge would have written — a recorded review for
   that key: `GovOut (Reviewed { Rule = id; Key = key; Verdict = Pass })` (your `Project`
   surfaces it as `Reviewed`). Re-apply: `kr.Apply facts` now emits `GovOut (Decided (id,
   Pass))` and **no** `NeedsReview` (cache hit).
5. **Deterministic + HumanOnly.** `(CheckRule.toRule bridge detRule).Apply facts` emits
   `Decided (id, Check.eval facts detRule.Check)` (verbatim, `Uncertain` preserved). A
   `HumanOnly` rule emits `Escalated id` whether its `Severity` is `Advisory` or after
   `CheckRule.blocking` — severity does not change the escalation.
6. **End-to-end through the kernel.** `FixedPoint.evaluate identify [ kr ] facts` derives the
   governance fact with a `ProvenanceStep` naming the rule and `Note = Check.render` — the
   bridge participates in normal fixed-point evaluation.

If the shape is awkward in FSI, fix `CheckRule.fsi` before writing `CheckRule.fs` (FSI is
the honest audience).

## Validation scenarios (each maps to spec acceptance criteria)

Run with `dotnet test` (or `dotnet run` for the Expecto runner). Expected outcomes:

| # | Scenario | Asserts | Spec ref |
|---|----------|---------|----------|
| V13 | `rule` over an `Opaque` check at each tier | `Deterministic` ⇒ `Error (OpaqueCannotBeDeterministic id)`; `AgentReviewed`/`HumanOnly` ⇒ `Ok`; reified `Deterministic` ⇒ `Ok` | US3 AS1–3, FR-006, SC-001 |
| V14 | **Property** (FsCheck): `cacheKey` over fixed ingredients vs each ingredient varied | identical ⇒ identical key; any of {model id, version, check hash, an artifact hash, prompt} changed ⇒ different key | US2 AS1/2, FR-011/012, SC-002 |
| V15 | `cacheKey` with permuted / duplicated `artifactHashes` | key unchanged (de-dup + ordinal sort) | US2 edge, FR-012 |
| V16 | `AgentReviewed` `Apply` with a matching recorded review present | emits `Decided`, zero `NeedsReview` (cache hit, no agent call) | US2 AS3, FR-009, SC-003 |
| V17 | `AgentReviewed` `Apply` with no matching recorded review | emits exactly one `NeedsReview` carrying the key (cache miss) | US2 AS4, FR-009, SC-003 |
| V18 | recorded review under one `JudgeId`/prompt, then judge/prompt changed | old verdict no longer matches ⇒ fresh `NeedsReview` (re-review on judge change) | US2 AS5, FR-013, SC-004 |
| V19 | `Deterministic` `Apply` over checks evaluating to pass/fail/uncertain; `Description` of every bridged rule | `Decided (id, eval …)` verbatim (`Uncertain` preserved); `Description = Check.render` | US1 AS1/2, FR-007/008, SC-005/006 |
| V20 | `HumanOnly` Advisory vs Blocking; `blocking` modifier; `toRule`/`Apply` over every tier, empty facts, unknown artifact | `Escalated` regardless of severity; severity ⟂ tier; nothing throws (totality) | US1 AS3, US4 AS1–3, FR-002/010/017, SC-007/008 |

V13–V20 use **real** `CheckRule`/`Bridge` values and a real in-test adapter `'fact`
(Principle V) — no synthetic fixtures are required for F04. The existing **V11
surface-drift** test re-blesses to include the F04 surface, and the existing **V12
dependency-hygiene** test re-confirms the kernel still references only the BCL +
FSharp.Core after `CheckRule.*` is added (SHA-256 is `System.*`).

## Done-when (feature exit criteria — roadmap §F04)

- [ ] `dotnet build` clean; `dotnet test` green: F04's new validation scenarios plus the
      inherited surface-drift and dependency-hygiene tests.
- [ ] `CheckRule.fsi` matches `contracts/CheckRule.fsi`; `CheckRule.fs` has no
      `private`/`internal`/`public` on top-level bindings.
- [ ] `surface/FS.GG.Governance.Kernel.surface.txt` re-blessed (`BLESS_SURFACE=1 dotnet
      test`) to include the F04 types + the `CheckRule` module, and committed.
- [ ] Kernel assembly still carries zero heavy dependencies (V12 / SC-009).
- [ ] The reified-ness refusal pinned (SC-001); `cacheKey` reproducibility + per-ingredient
      sensitivity pinned (SC-002); cache hit/miss + re-review-on-judge-change pinned
      (SC-003/SC-004); `Description = Check.render` pinned (SC-006); tier ⟂ severity and
      totality pinned (SC-007/008). **Locks decision #1**; **notes decision #2** for F08.
- [ ] No agent call / I/O in the kernel (FR-015) — `NeedsReview` is emitted as data only.
- [ ] No packing yet — the kernel still packs at F06.
