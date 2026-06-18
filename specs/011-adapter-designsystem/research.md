# Phase 0 — Research (F11 · 011-adapter-designsystem)

Engineering decisions for the design-system adapter — the second concrete production adapter and domain #2 of
Milestone M3 (the adoption bar). Each resolves a design question the spec deferred to "the plan and the
curated `.fsi`" (spec Assumptions): the precise `DesignSystemFact`/`DesignArtifactRef` cases, the probe and
rule signatures, the rule ids, the fence, and the `Bridge` wiring. No NEEDS CLARIFICATION remained in the
Technical Context; these record *why* the chosen shapes. The footing is `docs/governance-design/adapters.md`
(the fixed design — "Design-system adapter") and the F09 SPI it is authored against. Throughout, the guiding
constraint is **generality by difference**: the design-system adapter must adopt the kernel *without copying
the Spec Kit adapter's shape* (FR-005), so each decision is checked against "does this reuse a kernel
facility, or does it re-introduce domain #1's machinery?"

---

## D1 — A new `FS.GG.Governance.Adapters.DesignSystem` project, pure, depending on the SPI only (never F10)

- **Decision**: Ship F11 as a **new pure library** `src/FS.GG.Governance.Adapters.DesignSystem/` (namespace
  `FS.GG.Governance.Adapters.DesignSystem`) with a single `ProjectReference` on
  `FS.GG.Governance.Adapters.Spi` and **zero `PackageReference`** (no rendering library). The kernel, the SPI,
  and the F10 Spec Kit adapter gain **no** reference to it; this adapter references **none** of them except
  the SPI (and the kernel through it).
- **Rationale**: FR-016 demands "a new component separate from the kernel, the SPI, and the Spec Kit adapter,
  depend on the SPI (and through it the kernel) without either depending on it, light footprint (BCL +
  `FSharp.Core` + SPI + kernel only — no rendering library)." FR-005/FR-014 add that it must **never reference
  F10** — the two adapters are independent siblings composed only at a root (F12). A concrete adapter is
  definitionally an *adopter* of the SPI, so it takes the SPI reference (not the kernel directly) — that is
  what lets F12 compose F10 and F11 over the SPI's `Composition` machinery. The *test* project may reference
  F10 to prove composition (D9); the shipped library may not, and the dependency-hygiene test enforces the
  asymmetry (SC-008).
- **Alternatives considered**:
  - *Reference F10 to reuse its dial / phase machinery.* Rejected outright — and it is the central anti-goal
    (FR-005): reusing domain #1's shape would make "generality" a second copy, not a real second adopter.
  - *Add a rendering-library reference to read tokens.* Rejected: reading tokens from a live design system is
    F08/F12 sensing (FR-015); this pure feature is fed fixture facts and needs no renderer (FR-010).

## D2 — The adapter supplies exactly the five components + the F04 `Bridge`; nothing cross-cutting

- **Decision**: Author the design-system adapter as a single `Adapter<DesignSystemFact, DesignArtifactRef,
  DesignChange>` value (`Catalog.adapter judge`) supplying the **five** SPI components — the closed
  `DesignSystemFact` union (named by `DesignSystem.identify`), the `DesignSystem.toRef` artifact mapping, the
  declared `DesignSystem.probes`, the `Catalog.catalog` rule list, and the `Catalog.fences` — plus the F04
  `DesignSystem.bridge judge` kernel wiring. The adapter module contains **no** inference, arbitration,
  evidence, rendering, hashing, explanation, severity, or routing code, **no** artifact-authoring operation,
  and **no** phase/lifecycle machinery.
- **Rationale**: FR-003 requires "exactly the five SPI components and nothing more; everything cross-cutting
  reused from the kernel," and FR-004 requires the adapter to be an **observer, not an author** (the anti-goal
  is structural). The F09 `Adapter` record makes the five-part contract *total* (a missing component does not
  compile), and the absence of any `System.IO`/serializer/`Model`/`Msg`/rendering-type in the `.fsi` makes the
  observer-only and purity properties checkable by inspection (SC-001/SC-003). The `DesignGov of RuleOutcome`
  case is the kernel wiring the `Bridge.Embed`/`Project` need — two one-liners, not a cross-cutting facility
  (mirrors F09's example `DocGov`/`TaskGov` and F10's `SpecKitGov`).
- **Alternatives considered**:
  - *Expose a richer adapter that also formats explanations / computes routes / authors tokens.* Rejected:
    those are kernel facilities (F03 `Check.render`/`explain`, F07 `Route`) or out-of-scope authoring (FR-004);
    re-exposing them would violate FR-003/FR-004 and bloat the surface. The adapter supplies values; the kernel
    interprets them.

## D3 — NO phase guard: a flat design surface, the keystone *difference* from F10

- **Decision**: The design-system adapter has **no** `Phase`, **no** `whenPhase`, **no** lifecycle-as-facts,
  and **no** merge fence. Every rule in the catalog is unconditional — it contributes its verdict whenever its
  facts are present. The only structural gating is the single high-stakes `tokenSurfaceFence` (D7), which is a
  surface fence, not a lifecycle fence.
- **Rationale**: this is the **thesis of the feature** (FR-005, US1). F10 governs a *staged lifecycle* and so
  needed `whenPhase` (an `Implies` over a phase antecedent) and a merge fence; a design language is a *flat
  surface* with no stages, so reproducing that machinery would be copying domain #1's shape for no domain
  reason. Demonstrating that a domain which shares **none** of F10's gating still adopts the same unchanged
  kernel is exactly what makes the adoption bar real "by difference, not by a second copy." Concretely: where
  F10's catalog wraps checks in `whenPhase Phase.Plan (…)`, F11's catalog wraps nothing — the checks are the
  bare `Check`s.
- **Alternatives considered**:
  - *Introduce a "design maturity" phase to mirror F10.* Rejected: there is no staged lifecycle in a design
    language; inventing one to look like F10 would defeat the purpose and add a fact case nothing senses.
  - *Add a merge fence for parity.* Rejected: the high-stakes surface here is the **public token surface**,
    not a merge boundary — the fence is over a *surface set*, not a *lifecycle position* (D7).

## D4 — No `RequireQualifiedAccess` needed (a noted difference); `DesignArtifactRef` is a plain DU

- **Decision**: Neither `DesignArtifactRef` (`TokenDocument | GeneratedTokenSurface | RenderedCapture |
  InteractionStateSpec | PagePatternSpec`) nor `DesignSystemFact` carries `[<RequireQualifiedAccess>]`; their
  case names are globally distinct within the namespace, so callers write `TokenDocument` directly.
- **Rationale**: F10 needed `RequireQualifiedAccess` because `Constitution`/`Plan`/`Tasks` collided between its
  `Phase` and `SpecKitArtifact` unions. F11 has **no two unions with colliding case names** — the artifact
  kinds and the fact cases are disjoint — so the attribute would be ceremony. This is a small but honest
  *difference* from F10's shape (Principle III: the plainest code that compiles). Should a future case
  introduce a collision, adding the attribute is a one-line, behaviour-free change.
- **Alternatives considered**:
  - *Add `RequireQualifiedAccess` for symmetry with F10.* Rejected: symmetry with domain #1 is the very thing
    FR-005 warns against; the attribute earns its place only when names actually collide.

## D5 — The fact vocabulary: five FR-001 categories in a closed seven-case union; the F05 taint model reused

- **Decision**: `DesignSystemFact` is a closed union of seven cases mapping the five FR-001 categories plus the
  facts the probes and the taint closure read:
  - `PolicySelected of policy: string` — (1) the selected design policy (e.g. `"AntDesign"`).
  - `DesignRule of ruleId: string` — (2) a declared design rule / intent of the domain.
  - `SurfaceObservation of probe: string * subject: DesignArtifactRef * met: bool` — (3) a sensed deterministic
    observation read from the surface (token-match, contrast, spacing scale, control-height, intent-consumed,
    visual-state), keyed by `(probe, subject)`. One parametric case collapses what would otherwise be six
    boolean cases (Principle III).
  - `MeasurementState of measurementId: string * state: EvidenceState` — (3, evidence) a sensed measurement's
    authored `EvidenceState` (`Pending`/`Real`/`Synthetic`/`Failed`/`Skipped`) — the F05 node. `AutoSynthetic`
    is computed by `Evidence.effective`, never supplied.
  - `VerdictRestsOn of verdictId: string * measurementId: string` — (3, evidence) a deterministic verdict rests
    on a measurement — the F05 dependency edge; the taint flows down it.
  - `ArtifactPresent of DesignArtifactRef` — whether a fixture artifact was observed, so a probe can report
    `Unknown` (absent) distinct from `Unmet` (present but failing) (edge cases).
  - `DesignGov of RuleOutcome` — (4 & 5) recorded reviews and blockers: the F04 governance-outcome embed case
    (kernel wiring), exactly as F10's `SpecKitGov` / F09's `DocGov`.
- **Rationale**: FR-001 lists five categories (selected policy, design rules, deterministic verdicts, recorded
  reviews, blockers). `PolicySelected`/`DesignRule` cover the first two; `SurfaceObservation` carries the
  deterministic verdicts the probes read; `MeasurementState`/`VerdictRestsOn` are the **exact F05 node/edge
  pair** F10 used (`TaskState`/`TaskDependsOn`), so the synthetic-taint closure is the kernel's, reused
  unchanged (FR-013, the milestone's "reuses 100% of kernel facilities" applied to domain #2); `DesignGov`
  carries recorded reviews and blockers (`RuleOutcome.Reviewed`/`Escalated`). The vocabulary is **owned by
  this adapter and shares nothing with F10** (FR-001).
- **Alternatives considered**:
  - *Six separate boolean fact cases (one per deterministic probe).* Rejected: case-explosion; the
    `(probe, subject, met)` parametric case is plainer and the probes filter by the `probe` key.
  - *Author `AutoSynthetic` directly on a measurement.* Rejected: `Evidence.build` refuses an
    `AutoSynthetic`-declared node (F05); the taint is computed, never authored (mirrors F10 D5).

## D6 — Probes read the fixture token tree as data; `surfaceMatches`/`contrastMeets` report three-valued

- **Decision**: The probes (`DesignSystem.surfaceMatches generated source`, `DesignSystem.contrastMeets policy
  surface`, and the spacing/control-height/intent/visual-state probes) are `Check.probe` values that read the
  supplied `SurfaceObservation`/`ArtifactPresent` facts and report `Outcome`: `Met` when the matching
  observation is `met = true`; `Unmet reason` when it is `met = false`; `Unknown reason` when no observation is
  present (the fixture artifact is absent/unreadable). Each names the `DesignArtifactRef`s it reads (via
  `toRef`) in its `Reads`, and carries the policy/subject as `ProbeArg`s so they render and hash. The probes
  **never render, capture, or author** — they read facts.
- **Rationale**: FR-010 requires the adapter to run "entirely against a fixture token tree … with no rendering
  dependency; the probes read those fixtures as data and report a three-valued `Outcome` (`Met`/`Unmet`/
  `Unknown`)." Because this feature is pure (FR-015), the fixtures are presented to the catalog **as facts**
  (the F08/F12 sensing layer is what reads JSON/RON token files into those facts); the tests carry a small
  fixture token tree (`tests/.../fixtures/`) and assert the lift to facts directly. The three-valued report is
  the Principle-VI distinction the edge cases demand: a *missing* input is `Unknown` (undecided), never a
  silent `Met`; a *present, failing* input is `Unmet`.
- **Alternatives considered**:
  - *Have the probe parse JSON/RON itself.* Rejected: that is I/O / sensing (F08/F12), forbidden in this pure
    layer (FR-015); and it would pull a serializer dependency the constraint forbids.
  - *Two-valued probes (Met/Unmet only).* Rejected: it would collapse "absent fixture" into a false `Met` or
    `Unmet`, violating the edge case and Principle VI.

## D7 — The catalog: reified `CheckRule`s built with the F04 smart constructors; ids and tiers fixed here

- **Decision**: The catalog is the design doc's fourteen rules plus one evidence-honesty rule, each built with
  `CheckRule.rule id tier spec check` then `|> blocking` / `|> asking`:
  - **Deterministic, Blocking**: `token-drift` (`surfaceMatches GeneratedTokenSurface TokenDocument`),
    `contrast-policy` (`contrastMeets policy GeneratedTokenSurface`), `token-surface-gate` (the public token
    surface is the blessed one).
  - **Deterministic, Advisory**: `spacing-scale`, `control-height-defaults`, `intent-coverage` (a declared
    `DesignRule` intent is consumed), `visual-state-resolution`.
  - **Deterministic, Blocking (evidence honesty, the F05 realization)**: `evidence-measured` — builds the
    kernel `EvidenceGraph` from the `MeasurementState` nodes and `VerdictRestsOn` edges via `Evidence.build`,
    runs `Evidence.effective`, and reports `Unmet` iff any deterministic verdict's effective state is
    `Synthetic`/`AutoSynthetic` (a verdict resting on a synthetic/unmeasured input). `Blocking` regardless —
    honesty about evidence is non-negotiable (FR-013-analogue; mirrors F10's `evidence-not-synthetic`).
  - **AgentReviewed, Advisory** (`Opaque` + `asking`): `rendered-matches-intent`, `four-values`,
    `page-pattern-correct`, `colour-informational`, `motion-restraint`, `elevation-layering`. Each is an
    `Opaque (name, fun _ -> Unknown "requires visual judgement")` carrying a `Question` that becomes the
    agent's prompt.
  - **HumanOnly, Blocking**: `adopt-new-policy` (an `Opaque` check; `HumanOnly` so `toRule` escalates and never
    decides).
- **Rationale**: FR-006/FR-007 require a *tiered catalog of reified rules*, each with a tier and severity, each
  rendering to a sentence. Building with the F04 constructors gets the `Deterministic`-reified-ness guardrail
  for free (an `Opaque` check **cannot** be `Deterministic` — the judgement rules are forced to `AgentReviewed`/
  `HumanOnly`, FR-008), and every rule's `Check.render` becomes its contract statement (F06) and route line
  (F07) with no drift (SC-004). The rule ids are stable handles tests assert over. The fourteen rules are the
  design doc's table verbatim; `evidence-measured` is the additional F05-taint realization the spec's taint
  edge case and milestone "evidence/taint via the kernel's F05" require — documented here as the design-system
  analogue of F10's evidence gate.
- **Alternatives considered**:
  - *Fold the taint into `token-drift`/`contrast-policy`.* Rejected: the taint closure is computed by
    `Evidence.effective` over the fact graph, separate from `Check.eval` of a boolean observation; a dedicated
    `evidence-measured` probe that builds and folds the graph is the clean reuse and keeps the boolean
    deterministic checks simple.
  - *Make the judgement rules `Deterministic` with a heuristic.* Rejected: FR-008 forbids it — a deterministic
    engine must not pretend to answer a visual-judgement question; the `Opaque` hatch is the whole point.

## D8 — No constitution dial: the blocking set is fixed by FR-009 (another deliberate difference from F10)

- **Decision**: F11 has **no** `ConstitutionDial`. `Catalog.adapter` takes only a `JudgeId`. The blocking set
  is fixed by the catalog's default severities per FR-009: `token-drift`, `contrast-policy`,
  `token-surface-gate`, and `evidence-measured` (Deterministic Blocking) plus `adopt-new-policy` (HumanOnly
  Blocking); everything else is `Advisory`.
- **Rationale**: F10 needed a dial because its constitution authors *which* lifecycle rules block at merge —
  an "enforcement ↔ light" decision that varies per repo. A design language's blocking set is **not a per-repo
  dial**: token currency, contrast, and the public token surface are exact contracts that always block, and
  visual judgement is always advisory (FR-009). Hard-coding the short, fixed blocking set is therefore correct
  here, and *not* having a dial is another honest *difference* from domain #1's shape (FR-005). FR-009 also
  warns that "a long blocking list would be the design smell of over-fencing" — the fixed list is deliberately
  short (four deterministic + one human).
- **Alternatives considered**:
  - *Add a dial for parity with F10.* Rejected: it would copy domain #1's shape for no domain reason (FR-005)
    and imply the blocking set is a per-repo policy when FR-009 fixes it.
  - *Make every deterministic rule blocking.* Rejected: FR-009 makes the default posture **advisory**; only
    the contract-bearing few block.

## D9 — Faithful lift proven against the REAL F10 adapter (a stronger composition proof than F10's)

- **Decision**: The design-system adapter references only the SPI and the kernel (never F10). The
  faithful-lift guarantee (FR-014) is proven in the **test project** by authoring a closed `ProjectFact =
  Design of DesignSystemFact | SpecKit of SpecKitFact | Gov of RuleOutcome` coproduct (with its single-case
  active patterns / `inject` / project `Identify`/`Bridge`) and asserting that for 100% of the design-system
  catalog the lifted rule's `(verdict, provenance)`, `Check.render`/`hash`/`reads`/`isReified` over
  coproduct-wrapped facts are byte-for-byte identical to the standalone original (`Composition.lift
  (|Design|_|) narrowDesign (Catalog.adapter judge)` → `Composition.compose [designLifted; specKitLifted]
  crossDomain`). The two unrelated domains coexist; dropping either `Lifted` removes it cleanly.
- **Rationale**: FR-014/SC-006/SC-007 require the standalone and lifted `(verdict, provenance)` to be
  byte-for-byte identical (the F09 faithful-lift guarantee — `Lift.check` keeps each probe's `Name`/`Reads`/
  `Args` and the combinator structure, so `render`/`hash` are invariant and the cache key does not move) AND
  the adoption bar (two unrelated domains at one root). Composing against the **real Spec Kit adapter** rather
  than a synthetic toy (as F10 did) is the *literal* milestone test — two real, unrelated production domains
  coexisting — and is a strictly stronger proof. The test project is the **only** place F11 sees F10; the
  shipped library never does (D1, SC-008). The project `Identify` delegating per case (`identify (Design f) =
  DesignSystem.identify f`) is the F09 law L3 that makes provenance ids survive the lift.
- **Alternatives considered**:
  - *Prove the lift against a synthetic toy domain (as F10 did).* Rejected as the *primary* proof: now that a
    real second domain (F10) exists, composing the two real domains is the milestone's actual claim. (A toy
    domain remains acceptable for isolated unit lifts, but the keystone test uses F10.)
  - *Have the shipped adapter reference F10 to compose.* Rejected: violates FR-005/FR-016; composition is the
    root's job (F12 / the test root), not the adapter's.

---

### Build order (consequence of D1/D5)
The new project compiles `DesignSystem.fsi`/`DesignSystem.fs` **then** `Catalog.fsi`/`Catalog.fs` (`Catalog`
references `DesignSystem`'s vocabulary/`bridge`/`probes` and the F09 `Adapter<…>`). The project references only
`FS.GG.Governance.Adapters.Spi`; **zero new `PackageReference`**, **no rendering library** (D1). The test
project references DesignSystem **and** SpecKit (Expecto + FsCheck, already pinned) and authors the concrete
`ProjectFact` coproduct that carries both real adapters for the faithful-lift / adoption-bar proof (D9). The
fixture token tree (a few JSON/RON files) lives under the test project's `fixtures/` and is lifted to
`DesignSystemFact`s by the tests (sensing of a live design system is F08/F12, FR-015).
