# Phase 1 — Data Model (F09 · 009-adapter-spi)

The SPI record, the lift combinators, the composition-root folds, and the lifting/composition
**laws** and invariants for the pure adoption-bar layer. The authoritative shape lives in
[`contracts/Adapter.fsi`](./contracts/Adapter.fsi) and
[`contracts/Composition.fsi`](./contracts/Composition.fsi); this document explains them — the
`.fsi` are the source of truth.

## 1. New public types

### The SPI — `Adapter` (`Adapter.fsi`)

| Type | Kind | Role |
|---|---|---|
| `Adapter<'fact,'artifact,'change>` | record (6 fields) | The five-part contract a domain supplies + the F04 `Bridge` wiring; TOTAL (a missing field does not compile, FR-014). |

The six fields map to the spec's five components + kernel wiring:

| Field | Component | Type |
|---|---|---|
| `Identify` | (1) the closed `'fact` union (names it) | `'fact -> FactId` |
| `ToRef` | (2) artifact mapping | `'artifact -> ArtifactRef` |
| `Probes` | (3) declared probes | `Probe<'fact> list` |
| `Rules` | (4) rule catalog | `CheckRule<'fact> list` |
| `Fences` | (5) fences | `Fence<'change> list` |
| `Bridge` | F04 wiring (not new code) | `Bridge<'fact>` |

### The composition root — `Composition` (`Composition.fsi`)

| Type | Kind | Role |
|---|---|---|
| `Lifted<'project,'change>` | record `{ Rules; Fences }` | One adapter's catalog+fences lifted into the coproduct (produced by `lift`). |
| `Composed<'project,'change>` | record `{ Catalog; Fences }` | The assembled project catalog + deduped fence union (produced by `compose`). |

### Reused types (no new declaration)
- F07: `Fence<'change>`, `Route`, `Route.route`, `Route.stakesOf`, `RunMode`, `Stakes`.
- F06: `Contract.ofRules`, `Json.*`, `Check.render` (the composed catalog feeds these unchanged).
- F04: `CheckRule<'fact>`, `Bridge<'fact>`, `CheckRule.toRule`, `CheckRule.cacheKey`,
  `RuleOutcome` (`Decided`/`NeedsReview`/`Reviewed`/`Escalated`), `RecordedReview`, `JudgeId`,
  `CheckTier`, `Severity`, `SpecSource`.
- F03: `Check<'fact>` (`Atom`/`All`/`Any`/`Not`/`Implies`/`Opaque`), `Probe<'fact>`, `ArtifactRef`,
  `ProbeArg`, `Outcome` (`Met`/`Unmet`/`Unknown`), `Check.eval`/`render`/`hash`/`reads`/
  `isReified`/`explain`.
- F02/F01: `Verdict`, `FactSet<'fact>`, `FactAssertion<'fact>`, `FactId`, `RuleId`,
  `ProvenanceStep`, `Rule<'fact>`, `FixedPoint.evaluate`, `EvaluationResult<'fact>`.

## 2. Functions (all pure & total)

| Function | Signature | Role |
|---|---|---|
| `Adapter.toRules` | `Adapter<'fact,'a,'c> -> Rule<'fact> list` | Standalone bridge of a single adapter's catalog (US1) — `Rules \|> map (CheckRule.toRule Bridge)`. |
| `Lift.check` | `('big -> 'small option) -> Check<'small> -> Check<'big>` | Contravariant check lift — render/hash/reads/isReified INVARIANT (SC-002). |
| `Lift.checkRule` | `('big -> 'small option) -> CheckRule<'small> -> CheckRule<'big>` | Lift a rule's check, preserving tier/severity/spec/question. |
| `Lift.rule` | `('small -> 'big) -> ('big -> 'small option) -> Rule<'small> -> Rule<'big>` | Invariant executable-rule lift — provenance-preserving (the docs' `Rule.contramapFacts`). |
| `Lift.fence` | `('big -> 'small) -> Fence<'small> -> Fence<'big>` | Contravariant fence lift — keeps `Name`. |
| `Composition.lift` | `('project -> 'dom option) -> ('change -> 'domChange) -> Adapter<'dom,'a,'domChange> -> Lifted<'project,'change>` | Lift one adapter into the project. |
| `Composition.compose` | `Lifted<'project,'change> list -> CheckRule<'project> list -> Composed<'project,'change>` | Assemble catalog (concat) + fences (deduped union). |
| `Composition.toRules` | `Bridge<'project> -> Composed<'project,'change> -> Rule<'project> list` | Bridge the composed catalog via UNCHANGED `CheckRule.toRule` (SC-006). |

## 3. The two flows (standalone adoption, and composition at one root)

### Standalone — one adapter governs itself (US1, FR-002)
```text
adapter : Adapter<'fact,'artifact,'change>
  rules    = Adapter.toRules adapter                                   = Rules |> map (CheckRule.toRule Bridge)
  result   = FixedPoint.evaluate adapter.Identify rules supplied       (F01 — inference, UNCHANGED)
  route    = Route.route adapter.Fences adapter.Rules mode change      (F07 — routing, UNCHANGED)
  explain  = Check.explain facts rule.Check ; render = Check.render … ; hash = Check.hash …   (F03)
```
The adapter supplies only the five components; **inference, arbitration, evidence, rendering,
hashing, explanation, severity, and run-modes are all the kernel's** — the adapter contains none
(SC-001).

### Composition — several adapters at one root (US2/US3, FR-003/004/005)
The consumer authors, at the **one root**, the closed coproduct and its wiring (short, single-case):
```text
type ProjectFact =                                  // the closed coproduct (consumer-authored, D8)
    | Design   of DesignFact
    | SpecKit  of SpecKitFact
    | Governance of RuleOutcome                      // the project's OWN governance case (D8)
let (|Design|_|)  = function Design f  -> Some f | _ -> None     // single-case active patterns
let (|SpecKit|_|) = function SpecKit f -> Some f | _ -> None
let identify : ProjectFact -> FactId = …             // delegates per case (law L3)
let bridge   : Bridge<ProjectFact>   = { Judge=…; Embed=Governance;
                                         Project=(function Governance o -> Some o | _ -> None);
                                         ArtifactHash=… }
```
then assembles with the **generic** machinery:
```text
liftedDesign  = Composition.lift (|Design|_|)  narrowDesign  designAdapter
liftedSpecKit = Composition.lift (|SpecKit|_|) narrowSpecKit specKitAdapter
composed      = Composition.compose [ liftedDesign; liftedSpecKit ] crossDomainRules
  Catalog = liftedDesign.Rules @ liftedSpecKit.Rules @ crossDomainRules
  Fences  = dedup-by-Name (liftedDesign.Fences @ liftedSpecKit.Fences)
  result  = FixedPoint.evaluate identify (Composition.toRules bridge composed) supplied   (UNCHANGED)
  route   = Route.route composed.Fences composed.Catalog mode change                       (UNCHANGED)
```
`crossDomainRules : CheckRule<ProjectFact> list` is the small, named set of `Implies` rules over
the coproduct (FR-007/FR-012). No new evaluation or precedence code is added (FR-005, SC-006).

## 4. Lifting laws (FR-004, SC-002)

Let `p : 'big -> 'small option` be a single-case prism and `inj : 'small -> 'big` its constructor,
forming a lawful single-case sum (`p (inj x) = Some x`; `p y = None` for any `y` not from `inj`).

- **L1 (render/hash invariance)** `Check.render (Lift.check p c) = Check.render c` and
  `Check.hash (Lift.check p c) = Check.hash c` — the lift touches only the `Eval` channel, so the
  execution-free interpreters and therefore the F04 cache key are **byte-for-byte identical** (the
  cache does not move under lifting). Likewise `Check.reads` and `Check.isReified` are invariant
  (a lifted `Opaque` stays opaque → stays out of `Deterministic` → routes to review, US2-3).
- **L2 (eval/explain faithfulness)** for any `bigFacts` and the projected `smallFacts =
  bigFacts |> choose (fun fa -> p fa.Value |> Option.map (fun v -> { fa with Value = v }))`:
  `Check.eval bigFacts (Lift.check p c) = Check.eval smallFacts c`, and the explanations are equal.
- **L3 (provenance preservation)** if the project `Identify` agrees with the domain's on injected
  facts — `projectIdentify (inj f) = domainIdentify f` — then evaluating `Lift.rule inj p r` over
  `bigFacts` yields, for every produced assertion, the **identical** `(Value-up-to-inj, Id,
  Provenance)` as evaluating `r` over `smallFacts`: same `FactId`, same `ProvenanceStep`
  (`Rule`/`Inputs`/`Note`), same `Verdict`. The lift adds no behaviour; it re-targets the channel.
- **L4 (CheckRule preservation)** `Lift.checkRule p r` preserves `Id`/`Tier`/`Spec`/`Severity`/
  `Question`; only its `Check` is lifted (by L1/L2). So a lifted catalog routes (F07) and publishes
  its contract (F06) identically to standalone, modulo the fact channel.
- **L5 (fence preservation)** `Lift.fence narrow f` preserves `f.Name`; `(Lift.fence narrow f).Trips
  = f.Trips << narrow`. So `Route.stakesOf` over lifted fences trips on exactly the narrowed change.

## 5. Composition laws & precedence (FR-005/007/008)

- **C1 (no new logic)** `Composition.toRules bridge composed = composed.Catalog |> List.map
  (CheckRule.toRule bridge)` — the executable rules are the kernel's, unchanged. Evaluation is
  `FixedPoint.evaluate`; routing is `Route.route`. The kernel gains no adapter-specific code, and
  the dependency direction is adapters → kernel (SC-006/SC-008).
- **C2 (order-independence — inherited, not added)** the least fixed point is forward chaining over
  monotonic rules ⇒ unique least fixed point regardless of rule order (the Datalog guarantee).
  `Route.stakesOf`/`route` are already order-independent (gate sets are deduped unions; the
  partition depends on stakes/mode, not fence order). Therefore the merged verdict and the route
  over `composed` are **identical under every permutation** of adapter-composition order and rule
  order (FR-008, SC-003/SC-007) — `compose` is a concatenation + a set union, both of whose
  *downstream* folds are order-free.
- **C3 (precedence is F07)** cross-domain coupling's "fixed precedence — a blocking result always
  wins; default allow-unless-fenced" IS F07's `Route` (forbid-trumps-permit), not new code. A
  blocking result from any adapter wins regardless of position because `route` partitions
  `Blocking` by `Severity ∧ Fenced ∧ Gate`, independent of order (FR-007, SC-003/SC-007).
- **C4 (cross-domain rule = `Implies` over the coproduct)** the only sanctioned cross-domain forms
  are an `Implies` `CheckRule<'project>` (in the AST) plus C3's precedence (at combine time) —
  never ad-hoc glue or a positional first-match rule (FR-007, theory Hazard 5).
- **C5 (fence union)** `composed.Fences` is the input fences deduped by `Name` (first occurrence
  under a total order kept), so two adapters naming the same surface are counted once (FR-011).

## 6. Removal / boundary & inertness (FR-009, SC-004 — the milestone proof)

- **R1 (independent removal)** dropping a `Lifted` from `compose`'s list removes exactly that
  domain's rules and fences; nothing in the kernel or the other `Lifted` values references it
  (adapters reference only the SPI + kernel, FR-006). The remaining catalog evaluates unchanged.
- **R2 (inert absent antecedent)** a `crossDomain` `Implies` rule whose antecedent domain is gone
  reads project facts that are never present. Its antecedent probe MUST report **`Unmet`** ("not a
  design task — there is no design fact"), so `Implies(a,b) = Any[Not a; b]` has `Not (Unmet) =
  Pass` ⇒ the rule is **vacuously satisfied / inert** — never an error, never a silent fail.
- **R3 (probe-authoring guideline)** therefore a cross-domain antecedent probe distinguishes
  **`Unmet`** (a definite negative: the domain's facts are absent ⇒ not applicable ⇒ inert) from
  **`Unknown`** (genuinely undecided: the domain is present but the check has not been ruled). Using
  `Unknown` for absence would leave the rule undecided after removal instead of inert (research D9).

## 7. Edge cases (from spec → behaviour)

| Edge case | Behaviour |
|---|---|
| Single adapter, no cross-domain rules | `compose [one] []` ⇒ the trivial coproduct evaluates exactly as the adapter standalone (composition adds nothing). |
| Two non-interacting adapters | no `crossDomain` rule ⇒ neither sees the other's facts; each evaluates as if alone. |
| Cross-domain rule, absent antecedent domain | inert via `Unmet` (R2/R3) — not an error, not a silent fail. |
| Conflicting verdicts across domains | F07 precedence resolves deterministically (a blocking result wins), order-independent (C3). |
| Duplicate fences from two adapters | deduped by `Name` in `composed.Fences` (C5), not double-counted. |
| Lifted `Opaque`/`AgentReviewed` rule | stays out of `Deterministic`, routes to review; tier/opacity preserved (L1/L4). |
| Composition order permuted | identical least fixed point and merged route (C2). |
| Minimal adapter (empty rules/fences) | valid — `Rules`/`Fences` may be empty; `compose` over empty contributions is well-formed. |
| Adding a new domain | a central edit to the consumer's coproduct + one `lift` call at the root (the closed-union trade, D8) — not an open plug-in. |
| Negation-across-domains | a cross-domain rule must not negate-check a fact another adapter could DERIVE in the same fixed point; such facts are supplied in a lower stratum (theory Hazard 1 carries across the coproduct). |

## 8. Dependencies & ordering
New project `FS.GG.Governance.Adapters.Spi` with a single `ProjectReference` →
`FS.GG.Governance.Kernel` and **zero `PackageReference`** (research D1). Compile order:
`Adapter.fsi`/`Adapter.fs` then `Composition.fsi`/`Composition.fs` (`Composition` references
`Adapter`'s `Adapter<…>`/`Lift` and the kernel). No I/O, no serializer — pure values and folds. A
new surface baseline `surface/FS.GG.Governance.Adapters.Spi.surface.txt` and a Spi-side
drift/hygiene test (Spi → BCL/FSharp.Core/Kernel only). The concrete `ProjectFact` coproduct and
the two unrelated example adapters live in the **test project** (synthetic example domains,
disclosed per Principle V), not the shipped library (D8).
