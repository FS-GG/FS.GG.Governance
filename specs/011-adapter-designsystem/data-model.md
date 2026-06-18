# Phase 1 — Data Model (F11 · 011-adapter-designsystem)

The design-system domain vocabulary, the probes, the tiered rule catalog, the high-stakes fence, and the
**laws** and invariants for the pure second-adapter layer. The authoritative shape lives in
[`contracts/DesignSystem.fsi`](./contracts/DesignSystem.fsi) and [`contracts/Catalog.fsi`](./contracts/Catalog.fsi);
this document explains them — the `.fsi` are the source of truth.

The through-line is **generality by difference**: every law below is checked against F10's shape, and the
*absences* (no `Phase`, no `whenPhase`, no merge fence, no dial, no `RequireQualifiedAccess`) are first-class —
they are what proves a domain unlike domain #1 adopts the same unchanged kernel (FR-005).

## 1. New public types

### The vocabulary — `DesignSystem` module (`DesignSystem.fsi`)

| Type | Kind | Role |
|---|---|---|
| `DesignArtifactRef` | DU, 5 cases, **plain** (no `RequireQualifiedAccess`) | The artifact kinds; mapped to `ArtifactRef` by `DesignSystem.toRef` (FR-002). No case-name collisions ⇒ no attribute (D4). |
| `DesignSystemFact` | DU, 7 cases | The domain's closed, owned fact vocabulary (FR-001) + the `DesignGov` embed case (kernel wiring, D2). **No `PhaseReached`** (D3). |
| `DesignChange` | record `{ Surfaces: Set<DesignArtifactRef> }` | The domain's own change shape the F07 fence classifies (FR-014). **No `Phase` field** (D3). |

`DesignArtifactRef` cases: `TokenDocument` · `GeneratedTokenSurface` · `RenderedCapture` ·
`InteractionStateSpec` · `PagePatternSpec` (FR-002).

`DesignSystemFact` cases (FR-001): `PolicySelected of policy` · `DesignRule of ruleId` · `SurfaceObservation
of probe * subject * met` · `MeasurementState of measurementId * EvidenceState` · `VerdictRestsOn of verdictId
* measurementId` · `ArtifactPresent of DesignArtifactRef` · `DesignGov of RuleOutcome` (wiring). The five
FR-001 categories map: policy → `PolicySelected`; design rules → `DesignRule`; deterministic verdicts →
`SurfaceObservation` (the boolean read) + `MeasurementState`/`VerdictRestsOn` (the F05 evidence pair); recorded
reviews + blockers → `DesignGov`. `MeasurementState` carries one of the five **authored** `EvidenceState`s —
`AutoSynthetic` is computed by `Evidence.effective`, never supplied (D5).

### The catalog — `Catalog` module (`Catalog.fsi`)

**No new types.** Unlike F10 there is **no `ConstitutionDial`** — the blocking set is fixed by FR-009 (D8). The
module exposes only values (the named rules, `catalog`, `tokenSurfaceFence`, `fences`, `adapter`).

### Reused types (no new declaration)
- F09: `Adapter<'fact,'artifact,'change>`, `Lift.check`/`checkRule`/`fence`, `Composition.lift`/`compose`/
  `toRules`, `Lifted`/`Composed` (the adapter is one `Adapter` value; lifting/composition is proven via these).
- F07: `Fence<'change>`, `Route`, `Route.route`/`stakesOf`, `RunMode` (`Sandbox`/`Inner`/`Gate`), `Stakes`.
- F05: `EvidenceState` (`Pending`/`Real`/`Synthetic`/`Failed`/`Skipped`/`AutoSynthetic`),
  `EvidenceGraph<'id>`, `Evidence.build`/`effective`, `GraphError` (`Cycle`/`UnknownNode`/
  `AutoSyntheticDeclared`).
- F04: `CheckRule<'fact>`, `CheckTier` (`Deterministic`/`AgentReviewed`/`HumanOnly`), `Severity`
  (`Advisory`/`Blocking`), `SpecSource`, `Bridge<'fact>`, `JudgeId`, `RuleOutcome`, `CheckRule.rule`/
  `blocking`/`asking`/`toRule`.
- F03: `Check<'fact>` (`Atom`/`All`/`Any`/`Not`/`Implies`/`Opaque`), `Probe<'fact>`, `ArtifactRef`,
  `ProbeArg` (`ArtifactArg`/`LiteralArg`/`NumberArg`), `Outcome` (`Met`/`Unmet`/`Unknown`), `Check.probe`/
  `allOf`/`anyOf`/`eval`/`render`/`hash`/`explain`/`reads`/`isReified`.
- F02/F01: `Verdict` (`Pass`/`Fail`/`Uncertain`), `FactSet<'fact>`, `FactAssertion<'fact>`, `FactId`,
  `RuleId`, `ProvenanceStep`, `Rule<'fact>`, `FixedPoint.evaluate`.

## 2. Functions (all pure & total)

| Function | Signature | Role |
|---|---|---|
| `DesignSystem.toRef` | `DesignArtifactRef -> ArtifactRef` | (2) artifact mapping; injective. |
| `DesignSystem.identify` | `DesignSystemFact -> FactId` | (1, identity) fact identity for the kernel fold (law L0). |
| `DesignSystem.bridge` | `JudgeId -> Bridge<DesignSystemFact>` | F04 wiring: `Embed = DesignGov`, `Project` inverse, `ArtifactHash = fun _ _ -> ""` (D2). |
| `DesignSystem.surfaceMatches` | `generated:DesignArtifactRef -> source:DesignArtifactRef -> Check<…>` | the token-drift probe (laws Pr1–Pr3). |
| `DesignSystem.contrastMeets` | `policy:string -> surface:DesignArtifactRef -> Check<…>` | the contrast probe; `policy` a `LiteralArg`. |
| `DesignSystem.surfaceObserved` | `name:string -> subject:DesignArtifactRef -> Check<…>` | the shared deterministic surface probe. |
| `DesignSystem.evidenceMeasured` | `Check<DesignSystemFact>` | the F05 taint probe (laws E1–E3). |
| `DesignSystem.probes` | `Probe<DesignSystemFact> list` | (3) declared probe vocabulary. |
| `Catalog.<rule>` | `CheckRule<DesignSystemFact>` (×15) | the named reified rules (D7). |
| `Catalog.catalog` | `CheckRule<DesignSystemFact> list` | (4) the full catalog, fixed severities. |
| `Catalog.tokenSurfaceFence` | `Fence<DesignChange>` | (5) the single surface fence; trips when `Surfaces.Contains GeneratedTokenSurface`. |
| `Catalog.fences` | `Fence<DesignChange> list` | `[ tokenSurfaceFence ]` (D8). |
| `Catalog.adapter` | `JudgeId -> Adapter<DesignSystemFact, DesignArtifactRef, DesignChange>` | (the whole adapter) the five components + bridge (laws A1–A2). **No dial argument** (D8). |

## 3. The flows (standalone adoption, and the surface fence)

### Standalone — the design-system adapter governs a design language (US1, FR-003)
```text
adapter   = Catalog.adapter judge                                            : Adapter<DesignSystemFact, _, _>
  rules     = Adapter.toRules adapter                                        = Rules |> map (CheckRule.toRule Bridge)
  result    = FixedPoint.evaluate adapter.Identify rules supplied            (F01 — inference, UNCHANGED)
  route     = Route.route adapter.Fences applicableRules mode change         (F07 — routing, UNCHANGED)
  explain   = Check.explain facts rule.Check ; render = Check.render rule.Check ; hash = Check.hash …   (F03)
```
The adapter supplies only the five components + the `Bridge`; **inference, arbitration, evidence, rendering,
hashing, explanation, severity, and run-modes are all the kernel's** — the adapter contains none (SC-001). It
exposes **no** artifact-authoring operation (FR-004) and **no** phase/lifecycle machinery (FR-005).

### Advisory by default; the token-surface fence (US2, FR-009)
```text
non-fenced change (Surfaces ∌ GeneratedTokenSurface): Route.route fences applicable Gate change  ⇒ Blocking = []  (advisory)
fenced change      (Surfaces ∋ GeneratedTokenSurface): Route.route fences applicable Gate change  ⇒ Blocking = the deterministic/HumanOnly blocking rules
```
A requirement is a blocking gate iff `Severity = Blocking ∧ change Fenced ∧ mode = Gate` (F07). The default
posture is advisory; only when a change touches the **public token surface** does `tokenSurfaceFence` trip and
the `Blocking` rules (token-drift, contrast, token-surface-gate, evidence) bite. The host (F08/F12) chooses the
mode — the adapter ships no run-mode logic (D8). Note: there is **no merge fence and no phase** — the high-stakes
boundary is a *surface*, not a *lifecycle position* (the keystone difference from F10, D3).

## 4. Probe laws (`surfaceMatches`/`contrastMeets`/`surfaceObserved`, FR-010, SC-003)

Let a deterministic probe `pr` over key `k` (a `(name, subject)` pair) read the `SurfaceObservation (name,
subject, met)` and `ArtifactPresent subject` facts.

- **Pr1 (met ⇒ Met)** if some `SurfaceObservation (name, subject, true)` is present, `pr` reports `Met` ⇒
  `eval = Pass`.
- **Pr2 (unmet ⇒ Unmet)** if some `SurfaceObservation (name, subject, false)` is present, `pr` reports `Unmet
  reason` ⇒ `eval = Fail` — a definite failure, distinguishable from `Unknown`.
- **Pr3 (absent ⇒ Unknown)** if NO `SurfaceObservation (name, subject, _)` is present, `pr` reports `Unknown
  reason` ⇒ `eval = Uncertain` — a missing fixture is undecided, **never a silent `Met`** (edge cases "contrast
  fixture missing", "generated surface drifts vs missing"). The Principle-VI bad-input-≠-defect distinction.
- **Pr4 (render/hash distinguish args)** the `subject` `DesignArtifactRef`s appear in `Reads`/`Args` and (for
  `contrastMeets`) `policy` is a `LiteralArg`, so `Check.render`/`hash` distinguish `contrastMeets "AntAA" s`
  from `contrastMeets "WCAGAAA" s` and `surfaceMatches g c` from `surfaceMatches c g` — the probe shape is part
  of the rule's contract and cache key (SC-004/SC-005).

## 5. Catalog & tier laws (FR-006/FR-007/FR-008/FR-009)

- **C1 (every rule reified-or-typed)** each `Catalog` rule is built with `CheckRule.rule`/`asking`, so a
  `Deterministic` rule's check is reified (the F04 guardrail refuses an `Opaque` check at `Deterministic`,
  FR-008); the judgement rules are `Opaque` and therefore `AgentReviewed`; `adopt-new-policy` is `Opaque`/
  `HumanOnly`. The catalog never smuggles an opaque check into `Deterministic` (SC-002).
- **C2 (every rule renders & explains)** for every `r ∈ catalog`, `Check.render r.Check` is a non-empty
  sentence and `Check.explain facts r.Check` has top verdict `= Check.eval facts r.Check` (F03 SC-004) — the
  published `Statement` equals `Check.render` of the value `eval` ran, advertised = enforced (SC-004).
- **C3 (the blocking set is short and fixed)** in `catalog`, the `Blocking` rules are exactly `token-drift`,
  `contrast-policy`, `token-surface-gate`, `evidence-measured` (Deterministic) and `adopt-new-policy`
  (HumanOnly); every other rule is `Advisory` (FR-009). There is no dial to vary this (D8) — the fixed,
  deliberately short list avoids the over-fencing smell (FR-009). `HumanOnly` escalates regardless of severity
  (F04).
- **C4 (judgement rules carry the question, never resolve)** each `AgentReviewed` rule is `Opaque (name, fun _
  -> Unknown …)` + `asking question`, so `Check.eval` is `Uncertain` (never `Pass`/`Fail`) and `toRule` routes
  it to a review whose prompt is the `Question` (SC-002, FR-008). `adopt-new-policy` (`HumanOnly`) makes
  `toRule` emit `Escalated` — a person decides, never the engine (SC-002, edge case).

## 6. Evidence/taint laws (the F05 reuse, FR-013-analogue, SC-003 taint)

- **E1 (kernel taint, not adapter code)** `evidence-measured`'s probe builds the graph `Evidence.build [ (m,s)
  for MeasurementState (m,s) ] [ (v,m) for VerdictRestsOn (v,m) ]`, and on `Ok graph` reports `Unmet` iff some
  node's `Evidence.effective` state is `Synthetic`/`AutoSynthetic`, else `Met`. The `AutoSynthetic` taint
  propagates down the `VerdictRestsOn` chain by the kernel's least fixed point — the adapter ships **no** graph
  engine (SC-003 taint). A deterministic verdict resting on a synthetic/unmeasured input is therefore a
  blocking `Fail` at a fenced change in `Gate`.
- **E2 (malformed ≠ tainted)** on `Error e` from `Evidence.build` (a `Cycle` or `UnknownNode`), the probe
  reports `Unmet` with the `GraphError` — a definite well-formedness failure, distinguishable from a
  synthetic-taint failure and from an undecided `Unknown` (Principle VI).
- **E3 (no flag flips it)** `evidence-measured` is `Blocking` and the adapter exposes no override that weakens
  it; disclosure does not buy a pass (honesty non-negotiable, mirrors F10). At a token-surface-fenced change in
  `Gate` it is a blocking failure for any synthetic-tainted deterministic verdict.

## 7. No-leak laws (FR-011, SC-003 — the kernel stays domain-neutral)

- **N1 (no rendering vocabulary in the kernel/SPI)** `DesignArtifactRef`, the token/colour/layout concepts, and
  every design-specific name live ONLY in this adapter's closed `DesignSystemFact`/`DesignArtifactRef`; the
  kernel and SPI public surfaces carry **zero** rendering/token/colour/layout vocabulary. Removing the adapter
  removes the design vocabulary entirely (the kernel stays deletable-down-to-neutral). Guarded by the
  surface-drift test inspecting the kernel/SPI baselines (SC-003).
- **N2 (no renderer on the path)** the full catalog evaluates and explains over fixture-drawn facts with **no**
  rendering library referenced (FR-010/FR-016) — the dependency-hygiene test asserts the DesignSystem deps are
  `⊆ {BCL, FSharp.Core, Spi, Kernel}` (SC-008).

## 8. Faithful-lift laws (FR-014, SC-006/SC-007 — the milestone proof)

Let `p = (|Design|_|) : ProjectFact -> DesignSystemFact option`, `inj = Design`, and the project `Identify`
agree with `DesignSystem.identify` on injected facts (`projIdentify (Design f) = DesignSystem.identify f`).

- **L0 (identity)** `DesignSystem.identify` is injective on value-bearing facts (Hazard 4); `PolicySelected`/
  `MeasurementState`/`SurfaceObservation` key by entity (so a later fact supersedes — dedup), the rest by full
  value. The project `Identify` delegating per case is the precondition L2 needs.
- **L1 (render/hash/reads invariance)** for every `r ∈ catalog`, `Check.render (Lift.check p r.Check) =
  Check.render r.Check`, `Check.hash … = Check.hash …`, and `Check.reads … = Check.reads …` — the lift touches
  only the `Eval` channel, so the agent-review cache key does not move (F09 law L1). A lifted `Opaque` stays
  opaque (the judgement rules stay `AgentReviewed` under composition).
- **L2 (verdict + provenance faithfulness)** evaluating the lifted catalog over coproduct-wrapped facts yields,
  for 100% of the rules, the **identical** `(verdict, provenance)` as the standalone catalog over the projected
  facts (F09 laws L2/L3). Proven in `LiftTests.fs` by composing the design-system adapter alongside the **real
  F10 Spec Kit adapter** (D9) — the two real, unrelated domains at one root.
- **L3 (independence / adoption bar)** the adapter references only the SPI and the kernel, never F10; dropping
  it from a `compose` list removes it cleanly, and a cross-domain rule naming an absent design domain goes inert
  (F09 R2/R3). Two unrelated domains (Spec Kit, design-system) adopt the same unchanged kernel, each with its
  own vocabulary and shape, neither shaped like the other — the adoption bar is met (SC-007).

## 9. Commutative-hash law (FR-013, SC-005)

- **H1 (commutative canonicalization)** a deterministic rule whose check combines sub-checks under `allOf`/
  `anyOf` hashes identically under any re-ordering of those members (the kernel's `Check.hash` canonicalizes
  `All`/`Any`, F03), while positional nodes (`Implies`, a probe's ordered `Args`/`Reads`) stay positional. So
  the F04 agent-review cache key for the judgement rules is stable across cosmetic re-orderings — no spurious
  re-review (SC-005). Two structurally-equal `Opaque` judgement rules produce the same cache key (their `Opaque`
  name + the bridge judge + the prompt — F04 `cacheKey`).

## 10. Edge cases (from spec → behaviour)

| Edge case | Behaviour |
|---|---|
| Generated surface drifts from source | `surfaceMatches GeneratedTokenSurface TokenDocument` reports `Unmet` ⇒ `token-drift` `Fail` (Blocking) (Pr2). |
| Contrast fixture missing/unreadable | `contrastMeets` over an absent surface ⇒ `Unknown` (undecided), never a silent `Pass` (Pr3). |
| Judgement rule, no agent available | an `Opaque`/`AgentReviewed` rule stays `Unknown`/advisory; routing/sensing the review is F08/F12, not this feature (C4). |
| Adopting a new design policy | `adopt-new-policy` (`HumanOnly`) escalates — routes to a person, never resolves by engine or agent (C4). |
| Synthetic / unmeasured input | a deterministic verdict resting on a `Synthetic` `MeasurementState` is `AutoSynthetic` via `Evidence.effective`; `evidence-measured` is a blocking `Fail` at a fenced change (E1/E3). |
| Commutative re-ordering | re-ordering an `allOf`/`anyOf` member does not change the hash ⇒ no spurious cache miss / re-review (H1). |
| Rendering vocabulary leak | any token/colour/layout type in the kernel/SPI surface is a defect; the surface-drift test guards it (N1). |
| Adapter alone vs composed | governs a design language standalone; lifts unchanged into a coproduct alongside F10 — standalone == lifted `(verdict, provenance)` (L1–L3, SC-006). |
| Default posture is advisory | only token-drift, contrast-policy, token-surface-gate, evidence-measured, and adopt-new-policy block; the rest are advisory (C3, FR-009). |

## 11. Dependencies & ordering
New project `FS.GG.Governance.Adapters.DesignSystem` with a single `ProjectReference` →
`FS.GG.Governance.Adapters.Spi` and **zero `PackageReference`**, **no rendering library** (D1). Compile order:
`DesignSystem.fsi`/`DesignSystem.fs` then `Catalog.fsi`/`Catalog.fs` (`Catalog` references `DesignSystem`'s
vocabulary/`bridge`/`probes` and the F09 `Adapter<…>`). No I/O, no serializer — pure values and folds. A new
surface baseline `surface/FS.GG.Governance.Adapters.DesignSystem.surface.txt` and a DesignSystem-side
drift/hygiene test (DesignSystem → BCL/FSharp.Core/Spi/Kernel only, **not F10**). The concrete `ProjectFact`
coproduct carrying both this adapter and the **real F10 adapter** (for the faithful-lift / adoption-bar proof)
lives in the **test project** — the only place F11 references F10 (the shipped adapter never does, SC-008). The
fixture token tree (a few JSON/RON files) lives under the test project's `fixtures/` and is lifted to facts by
the tests (sensing is F08/F12, FR-015).
