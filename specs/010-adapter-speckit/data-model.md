# Phase 1 — Data Model (F10 · 010-adapter-speckit)

The Spec Kit domain vocabulary, the phase guard, the reified rule catalog, the constitution dial, the
merge fence, and the **laws** and invariants for the pure first-adapter layer. The authoritative shape
lives in [`contracts/SpecKit.fsi`](./contracts/SpecKit.fsi) and
[`contracts/Catalog.fsi`](./contracts/Catalog.fsi); this document explains them — the `.fsi` are the
source of truth.

## 1. New public types

### The vocabulary — `SpecKit` module (`SpecKit.fsi`)

| Type | Kind | Role |
|---|---|---|
| `Phase` | DU, 8 cases, `RequireQualifiedAccess`, **ordered** | The lifecycle stages, supplied as a `PhaseReached` fact (US2). Order = lifecycle order; `Phase.rank`/`reached` read it for "at or after". |
| `SpecKitArtifact` | DU, 9 cases, `RequireQualifiedAccess` | The artifact kinds; mapped to `ArtifactRef` by `SpecKit.toRef` (FR-002). |
| `SpecKitFact` | DU, 7 cases | The domain's closed, owned fact vocabulary (FR-001) + the `SpecKitGov` embed case (kernel wiring, D2). |
| `SpecKitChange` | record `{ Phase; Surfaces }` | The domain's own change shape the F07 fence classifies (FR-014). |

`SpecKitFact` cases (FR-001): `PhaseReached of Phase` · `ArtifactPresent of SpecKitArtifact` · `TaskState
of taskId * EvidenceState` · `TaskDependsOn of taskId * dep` · `SkillBound of taskId * skillId` ·
`ConstitutionArea of area * filled` · `SpecKitGov of RuleOutcome` (wiring). `TaskState` carries one of the
five **authored** `EvidenceState`s — `AutoSynthetic` is computed by `Evidence.effective`, never supplied
(D5).

### The catalog & dial — `Catalog` module (`Catalog.fsi`)

| Type | Kind | Role |
|---|---|---|
| `ConstitutionDial` | record `{ BlockingAtMerge: Set<RuleId>; EarlyFences: (string * Phase) list }` | The constitution-authored "enforcement ↔ light" dial as data (US4, D8). |

### Reused types (no new declaration)
- F09: `Adapter<'fact,'artifact,'change>`, `Lift.check`/`checkRule`/`fence`, `Composition.lift`/`compose`/
  `toRules` (the adapter is one `Adapter` value; lifting/composition is proven via these).
- F07: `Fence<'change>`, `Route`, `Route.route`/`stakesOf`, `RunMode` (`Sandbox`/`Inner`/`Gate`), `Stakes`.
- F05: `EvidenceState` (`Pending`/`Real`/`Synthetic`/`Failed`/`Skipped`/`AutoSynthetic`),
  `EvidenceGraph<'id>`, `Evidence.build`/`effective`, `GraphError` (`Cycle`/`UnknownNode`/
  `AutoSyntheticDeclared`).
- F04: `CheckRule<'fact>`, `CheckTier` (`Deterministic`/`AgentReviewed`/`HumanOnly`), `Severity`
  (`Advisory`/`Blocking`), `SpecSource`, `Bridge<'fact>`, `JudgeId`, `RuleOutcome`, `CheckRule.rule`/
  `blocking`/`asking`/`toRule`.
- F03: `Check<'fact>` (`Atom`/`All`/`Any`/`Not`/`Implies`/`Opaque`), `Probe<'fact>`, `ArtifactRef`,
  `ProbeArg` (`LiteralArg`/…), `Outcome` (`Met`/`Unmet`/`Unknown`), `Check.probe`/`allOf`/`(==>)`/`eval`/
  `render`/`hash`/`explain`/`reads`/`isReified`.
- F02/F01: `Verdict` (`Pass`/`Fail`/`Uncertain`), `FactSet<'fact>`, `FactAssertion<'fact>`, `FactId`,
  `RuleId`, `ProvenanceStep`, `Rule<'fact>`, `FixedPoint.evaluate`.

## 2. Functions (all pure & total)

| Function | Signature | Role |
|---|---|---|
| `Phase.rank` | `Phase -> int` | The lifecycle position (`Constitution = 0 … Merge = 7`). |
| `Phase.reached` | `current:Phase -> required:Phase -> bool` | `rank current ≥ rank required` — the phase guard's "at or after". |
| `SpecKit.toRef` | `SpecKitArtifact -> ArtifactRef` | (2) artifact mapping; injective. |
| `SpecKit.identify` | `SpecKitFact -> FactId` | (1, identity) fact identity for the kernel fold (law L0). |
| `SpecKit.bridge` | `JudgeId -> Bridge<SpecKitFact>` | F04 wiring: `Embed = SpecKitGov`, `Project` inverse, `ArtifactHash = fun _ _ -> ""` (D2). |
| `SpecKit.whenPhase` | `Phase -> Check<SpecKitFact> -> Check<SpecKitFact>` | (the phase guard) `Implies (phaseAtLeast required, check)` (laws P1–P3). |
| `SpecKit.probes` | `Probe<SpecKitFact> list` | (3) declared probe vocabulary. |
| `Catalog.<rule>` | `CheckRule<SpecKitFact>` (×8) | the named reified rules (D6). |
| `Catalog.catalog` | `CheckRule<SpecKitFact> list` | (4) the full catalog, default severities. |
| `Catalog.mergeFence` | `Fence<SpecKitChange>` | (5) the single merge fence; `Trips = fun c -> c.Phase = Phase.Merge`. |
| `Catalog.defaultDial` | `ConstitutionDial` | the recommended dial (D8). |
| `Catalog.fences` | `ConstitutionDial -> Fence<SpecKitChange> list` | `mergeFence :: earlyFences dial`. |
| `Catalog.adapter` | `JudgeId -> ConstitutionDial -> Adapter<SpecKitFact, SpecKitArtifact, SpecKitChange>` | (the whole adapter) the five components + bridge, dial applied (laws A1–A2). |

## 3. The flows (standalone adoption, and the merge boundary)

### Standalone — the Spec Kit adapter governs this repo (US1, FR-003)
```text
adapter   = Catalog.adapter judge dial                                       : Adapter<SpecKitFact, _, _>
  rules     = Adapter.toRules adapter                                        = Rules |> map (CheckRule.toRule Bridge)
  result    = FixedPoint.evaluate adapter.Identify rules supplied            (F01 — inference, UNCHANGED)
  route     = Route.route adapter.Fences applicableRules mode change         (F07 — routing, UNCHANGED)
  explain   = Check.explain facts rule.Check ; render = Check.render rule.Check ; hash = Check.hash …   (F03)
```
The adapter supplies only the five components + the `Bridge`; **inference, arbitration, evidence,
rendering, hashing, explanation, severity, and run-modes are all the kernel's** — the adapter contains none
(SC-001). It exposes **no** artifact-authoring operation (FR-004).

### The inner loop vs. the merge fence (US3, FR-008/FR-009)
```text
inner loop (constitution … implement):  Route.route fences applicable Inner   change   ⇒ Blocking = []  (advisory)
merge:                                   Route.route fences applicable Gate  {Phase=Merge; …}            ⇒ Blocking = the dial's blocking rules
```
A requirement is a blocking gate iff `Severity = Blocking ∧ change Fenced ∧ mode = Gate` (F07). The inner
loop is always advisory (a failing deterministic check reports, never blocks); only at `Phase.Merge` does
`mergeFence` trip and the `Blocking` rules bite. "Recompute from base" is the host (F08) evaluating the
merge route over base-branch facts — not adapter logic (D7).

## 4. Phase-guard laws (`whenPhase`, FR-005, SC-002)

Let `g = SpecKit.whenPhase required check = Implies (Atom phaseAtLeast, check)`.

- **P1 (inert before the phase)** for any `facts` with no `PhaseReached q` where `Phase.reached q
  required` (including facts with **no** `PhaseReached` at all): `phaseAtLeast` reports `Unmet`, so
  `Check.eval facts g = Pass` — a definite **not-applicable** (vacuously satisfied), **never `Fail` or
  `Uncertain`**. (`Implies(a,b) = Any[Not a; b]`; `Unmet → Fail`; `negate (Fail) = Pass`; `Any[Pass;_] =
  Pass`.) The F09 inertness mechanism, applied to a phase antecedent.
- **P2 (transparent at/after the phase)** for any `facts` with some `PhaseReached q` where `Phase.reached
  q required`: `phaseAtLeast` reports `Met`, so `Check.eval facts g = Check.eval facts check`. (`Met →
  Pass`; `negate (Pass) = Fail ""`; `Any[Fail "";  b] = b`.) The guard adds nothing once the phase holds.
- **P3 (reified-ness preserving)** `Check.isReified g = Check.isReified check` (the antecedent is an `Atom`,
  always reified, so the conjunction's reified-ness is the consequent's). Hence a guarded reified check
  stays `Deterministic`-eligible and a guarded `Opaque` stays non-reified → forced `AgentReviewed`/
  `HumanOnly` (FR-006).
- **P4 (render/hash distinguish the phase)** `required` is a `LiteralArg` on `phaseAtLeast`, so
  `Check.render`/`Check.hash` of `whenPhase Plan c` differ from `whenPhase Tasks c` — the guard is part of
  the rule's contract and cache key, not invisible.

## 5. Catalog & dial laws (FR-006/FR-007/FR-010/FR-011)

- **C1 (every rule reified-or-typed)** each `Catalog` rule is built with `CheckRule.rule`, so a
  `Deterministic` rule's check is reified (the F04 guardrail); the judgement rules are `Opaque` and
  therefore `AgentReviewed`/`HumanOnly`. The catalog never smuggles an opaque check into `Deterministic`.
- **C2 (every rule renders & explains)** for every `r ∈ catalog`, `Check.render r.Check` is a non-empty
  sentence and `Check.explain facts r.Check` has top verdict `= Check.eval facts r.Check` (F03 SC-004) —
  the monolithic `analyze` becomes self-describing rules (SC-006).
- **C3 (default-advisory; evidence default-blocking)** in `catalog`, every rule is `Advisory` **except**
  `evidence-not-synthetic`, which is `Blocking` (FR-013). No rule blocks in the inner loop regardless
  (run-mode `Inner`, D7).
- **D1 (dial promotes the named set)** `Catalog.adapter judge dial` produces an adapter whose `Rules` are
  `catalog` with each rule whose `Id ∈ dial.BlockingAtMerge` set to `CheckRule.blocking`, and
  `evidence-not-synthetic` blocking regardless of the dial (FR-013). Therefore the merge fence's blocking
  *set* is a function of the **dial**, not a fixed list — varying `dial.BlockingAtMerge` varies which rules
  bite at merge (SC-005).
- **D2 (dial assembles the fences)** `(Catalog.adapter judge dial).Fences = Catalog.fences dial = mergeFence
  :: [ { Name; Trips = fun c -> c.Phase = p } for (Name, p) ∈ dial.EarlyFences ]` — the opt-in earlier
  hard-stop (FR-010) is one `EarlyFences` entry, no kernel change.

## 6. Evidence/dependency-graph laws (US5, FR-012/FR-013)

- **E1 (kernel taint, not adapter code)** `evidence-not-synthetic`'s probe builds the graph
  `Evidence.build [ (t, s) for TaskState (t,s) ] [ (t,d) for TaskDependsOn (t,d) ]`, and on `Ok graph`
  reports `Unmet` iff some node's `Evidence.effective graph` state is `Synthetic`/`AutoSynthetic`, else
  `Met`. The `AutoSynthetic` taint propagates down the `TaskDependsOn` chain by the kernel's least
  fixed point — the adapter ships **no** graph engine (SC-004).
- **E2 (malformed ≠ tainted)** on `Error e` from `Evidence.build` (a `Cycle` or `UnknownNode`), the probe
  reports `Unmet` with the `GraphError` — a definite well-formedness failure, distinguishable from a
  synthetic-taint failure and from an undecided `Unknown` (Principle VI).
- **E3 (no flag flips it)** `evidence-not-synthetic` is `Blocking` and the adapter exposes no override that
  weakens it; disclosure does not buy a pass (FR-013). At `Phase.Merge` in `Gate` it is a blocking failure
  for any synthetic-tainted evidence.
- **E4 (graph well-formedness advisory inner, blockable at merge)** `tasks-graph`'s `acyclic`/`depsResolve`
  sub-checks read the same `Evidence.build` result; the rule is `Advisory` by default and bites at merge
  only if the dial promotes it (FR-007, US5 acceptance 2).

## 7. Faithful-lift laws (FR-014, SC-007 — the milestone proof)

Let `p = (|SpecKit|_|) : ProjectFact -> SpecKitFact option`, `inj = SpecKit`, and the project `Identify`
agree with `SpecKit.identify` on injected facts (`projIdentify (SpecKit f) = SpecKit.identify f`).

- **L0 (identity)** `SpecKit.identify` is injective on value-bearing facts (Hazard 4); `TaskState`/
  `ConstitutionArea` key by entity (task id / area), so a later fact supersedes (dedup); the rest key by
  full value. The project `Identify` delegating per case is the precondition L1' below needs.
- **L1 (render/hash invariance)** for every `r ∈ catalog`, `Check.render (Lift.check p r.Check) =
  Check.render r.Check` and `Check.hash … = Check.hash …` — the lift touches only the `Eval` channel, so
  the agent-review cache key does not move (F09 law L1). A lifted `Opaque` stays opaque (US2-3 carries).
- **L2 (verdict + provenance faithfulness)** evaluating the lifted catalog over coproduct-wrapped facts
  yields, for 100% of the rules, the **identical** `(verdict, provenance)` as the standalone catalog over
  the projected facts (F09 laws L2/L3). Proven in `LiftTests.fs` by composing the Spec Kit adapter with a
  second synthetic toy domain (D9).
- **L3 (independence)** the adapter references only the SPI and the kernel, never another adapter; dropping
  it from a `compose` list removes it cleanly, and a cross-domain rule naming an absent Spec Kit goes inert
  (F09 R2/R3). The standalone Spec Kit verdict and the lifted one are identical (SC-007).

## 8. Edge cases (from spec → behaviour)

| Edge case | Behaviour |
|---|---|
| Phase before an artifact exists | `whenPhase Plan` rule before `Plan` ⇒ vacuous `Pass` (P1) — definite not-applicable, never `Fail`/`Uncertain`. |
| Phase fact absent entirely | every phase-guarded rule inert (`Pass`, P1) — no `PhaseReached` ⇒ no antecedent ⇒ vacuous; a missing phase is not a silent default to `Merge`. |
| Inner-loop deterministic failure | a failing deterministic check in `Inner` only **reports** — `Route.route … Inner` leaves `Blocking = []` (D7). |
| Merge recompute from base | the host (F08) evaluates the merge route over base-branch facts; a passing inner-loop run does not exempt the change (D7). |
| Cyclic task graph | `Evidence.build` returns `Cycle` ⇒ `acyclic`/`evidence-not-synthetic` probes report `Unmet` (E2); advisory inner, blockable at merge only if the dial opts that phase in (FR-010). |
| Unresolved dep / skill id | `depsResolve`/`skillIdsResolve` report a definite `Unmet` (E2), distinguishable from `Unknown`. |
| Constitution area placeholder | `constitution-complete` reports `Unmet`; advisory inner, **blocking at merge** when the dial promotes it (D1/SC-005). |
| Synthetic evidence at merge | `evidence-not-synthetic` is a blocking `Unmet` at `Phase.Merge`/`Gate`; no flag flips it (E3, FR-013). |
| `AgentReviewed` advisory check | `plan-satisfies-spec`/`tasks-complete-ordered` are `Opaque`/`AgentReviewed`, report via their `Question`, never block the inner loop (FR-007). |
| `HumanOnly` check | `feature-in-scope` is `HumanOnly` — `toRule` escalates (emits `Escalated`), never decides (FR-007). |
| Adapter alone vs. composed | governs this repo standalone; lifts unchanged into a coproduct — standalone == lifted `(verdict, provenance)` (L1–L3, SC-007). |

## 9. Dependencies & ordering
New project `FS.GG.Governance.Adapters.SpecKit` with a single `ProjectReference` →
`FS.GG.Governance.Adapters.Spi` and **zero `PackageReference`** (D1). Compile order: `SpecKit.fsi`/
`SpecKit.fs` then `Catalog.fsi`/`Catalog.fs` (`Catalog` references `SpecKit`'s vocabulary/`whenPhase`/
`bridge`/`probes` and the F09 `Adapter<…>`). No I/O, no serializer — pure values and folds. A new surface
baseline `surface/FS.GG.Governance.Adapters.SpecKit.surface.txt` and a SpecKit-side drift/hygiene test
(SpecKit → BCL/FSharp.Core/Spi/Kernel only). The concrete `ProjectFact` coproduct and the second unrelated
example domain (for the faithful-lift proof) live in the **test project** (the second domain is a synthetic
example domain, disclosed per Principle V; the Spec Kit adapter itself is the real adopter under test).
