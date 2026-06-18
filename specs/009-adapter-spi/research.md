# Phase 0 — Research (F09 · 009-adapter-spi)

Engineering decisions for the adapter SPI and composition root — the pure adoption-bar layer. Each
resolves a design question the spec deferred to "the plan and the curated `.fsi`" (spec
Assumptions). No NEEDS CLARIFICATION remained in the Technical Context; these record *why* the
chosen shapes. The footing is `docs/governance-design/theory-and-composition.md` (Data Types à la
Carte, kept closed) and `docs/governance-design/adapters.md` (what an adapter supplies).

---

## D1 — A new `FS.GG.Governance.Adapters.Spi` project, pure, separate from the kernel

- **Decision**: Ship F09 as a **new pure library** `src/FS.GG.Governance.Adapters.Spi/`
  (namespace `FS.GG.Governance.Adapters.Spi`) with a single `ProjectReference` on
  `FS.GG.Governance.Kernel` and **zero `PackageReference`**. The kernel gains **no** reference to
  it.
- **Rationale**: FR-015 demands "a new pure component separate from the kernel, depend on the
  kernel (F01–F07) without the kernel depending on it, light footprint (BCL + `FSharp.Core` +
  kernel only)." The roadmap (§3) names exactly this project:
  `FS.GG.Governance.Adapters.Spi  pure; depends on Kernel  (F09)`, with `…SpecKit` (F10) and
  `…DesignSystem` (F11) depending on **Spi**. Keeping it a separate library is what lets the
  concrete adapters and the CLI take a dependency on the *SPI*, and preserves the kernel's
  dependency direction (governance may inspect a project; a project must never require governance).
- **Alternatives considered**:
  - *Add the SPI to the kernel assembly (as F02–F07 did).* Rejected: it would make every kernel
    consumer carry the composition machinery, and would invert the intended layering (adapters
    depend on the SPI which depends on the kernel). The SPI is definitionally the adopter-facing
    layer above the kernel.
  - *Name it `FS.GG.Governance.Adapters` (no `.Spi`).* Rejected: the roadmap fixed `.Spi`, and it
    leaves room for the per-domain adapter assemblies (`.SpecKit`, `.DesignSystem`) as siblings.

## D2 — The SPI is one total `record`, so a missing component does not compile

- **Decision**: Model the adapter as a single record
  `Adapter<'fact,'artifact,'change> = { Identify; ToRef; Probes; Rules; Fences; Bridge }`. The
  **five** adoption components are the closed fact union (the `'fact` parameter, named by
  `Identify: 'fact -> FactId`), the artifact mapping (`ToRef: 'artifact -> ArtifactRef`), the
  declared `Probes: Probe<'fact> list`, the `Rules: CheckRule<'fact> list` catalog, and the
  `Fences: Fence<'change> list`. `Bridge: Bridge<'fact>` is the F04-defined **kernel wiring** the
  domain instantiates.
- **Rationale**: FR-001 wants "exactly five domain-specific components and nothing more"; FR-014
  wants "an adapter that omits a component to surface as a typed/explicit error at the boundary,
  not a silent partial adoption." An F# record is **total** — you cannot construct an `Adapter`
  with a field missing; the compiler rejects it. That is the strongest possible boundary error,
  with zero runtime validation code. `Identify` is part of *defining* the closed fact union (a fact
  vocabulary without its identity is not a complete contribution — Hazard 4 in the theory doc
  requires an injective `identify`), and `Bridge` (Embed/Project of the domain-neutral
  `RuleOutcome`, ArtifactHash, JudgeId) is the kernel's **own** F04 contract, not a
  re-implementation of any cross-cutting facility FR-002 forbids — embedding a `RuleOutcome` into
  `'fact` is two one-liners the domain alone can write. So the record is "the five + the kernel
  wiring," and the wiring is not new domain *logic*.
- **Alternatives considered**:
  - *An interface (`abstract class`/object expression).* Rejected: heavier, invites inheritance
    (Principle III prefers records over hierarchies), and gives no stronger totality than a record.
  - *Five separate arguments to every composition function.* Rejected: it scatters the contract,
    loses the "one value that IS the adapter" framing, and makes FR-001's "exactly five" implicit
    instead of a single inspectable type.
  - *Drop `Probes` (they live inside the rules' checks).* Rejected: the spec enumerates probes as
    a distinct component (FR-001), and a declared `Probes` list is the adapter's atomic-predicate
    vocabulary — useful for testing and the contract. It is documented as the declared set the
    catalog draws from, not a second source of truth for evaluation (the checks are authoritative).

## D3 — The lift is contravariant at the `Check`/`CheckRule` level (the cache key does not move)

- **Decision**: The primary lift combinators take **only a prism** `project: 'big -> 'small option`
  and are **contravariant**:
  - `Lift.check  : ('big -> 'small option) -> Check<'small> -> Check<'big>`
  - `Lift.checkRule : ('big -> 'small option) -> CheckRule<'small> -> CheckRule<'big>`

  `Lift.check` maps each `Atom`/`Opaque` probe's `Eval: FactSet<'small> -> Outcome` to
  `FactSet<'big> -> Outcome` by projecting the fact set (`List.choose` over the prism, **keeping
  each `FactAssertion`'s `Id` and `Provenance`**, mapping only `Value`); it leaves `Name`, `Reads`,
  `Args`, and the combinator structure (`All`/`Any`/`Not`/`Implies`) **untouched**.
  `Lift.checkRule` preserves `Id`/`Tier`/`Spec`/`Severity`/`Question` and lifts only `Check`.
- **Rationale**: a `Check` only **reads** facts (every interpreter but `eval`/`explain` ignores
  facts entirely), so it is genuinely contravariant in `'fact` — it needs the projection direction
  only, never the injection. This makes the lift match the design docs' one-argument ergonomics
  (`Rule.contramapFacts (function Design f -> Some f | _ -> None)`). Crucially, because the lift
  touches **only** the `Eval` channel, the four execution-free interpreters are **invariant**:
  `Check.render (Lift.check p c) = Check.render c`, `Check.hash (Lift.check p c) = Check.hash c`,
  `Check.reads … = Check.reads …`, and `Check.isReified … = Check.isReified …`. So the **F04
  agent-review cache key is stable across lifting** (FR-004, SC-002) — a verdict frozen for a rule
  standalone is found again after the rule is lifted, with no spurious re-review. The lifted
  `Opaque` stays opaque, so `isReified` is still false and the rule stays out of `Deterministic`,
  routing to review exactly as un-lifted (US2 acceptance 3).
- **Alternatives considered**:
  - *Lift only the executable `Rule` (the roadmap's `Rule.contramapFacts`).* Insufficient alone:
    `Route.route` and `Contract.ofRules` (F06/F07) consume `CheckRule`, not the bridged `Rule`, so
    routing and the published contract over a composed project need the **CheckRule** lifted. The
    CheckRule lift is also where render/hash invariance lives. We keep `Lift.rule` too (D4) but the
    CheckRule lift is primary.

## D4 — The executable `Rule` lift is invariant (prism + injection), preserving provenance

- **Decision**: `Lift.rule : inject:('small -> 'big) -> project:('big -> 'small option) ->
  Rule<'small> -> Rule<'big>` lifts an already-bridged executable rule. It projects the input fact
  set (keeping `Id`/`Provenance`), runs the inner `Apply`, and injects each produced
  `FactAssertion`'s `Value` back to `'big` while **keeping its `Id` and `Provenance` step
  verbatim**.
- **Rationale**: an executable `Rule` both **consumes and produces** facts, so it is invariant in
  `'fact` — it needs both directions (the injection is the coproduct constructor, e.g. `Design`).
  Keeping each derived assertion's `Id` and `ProvenanceStep` (`Rule`/`Inputs`/`Note`) verbatim is
  what makes lifting **provenance-preserving**: evaluating the lifted rule over coproduct-wrapped
  facts yields the **identical** `(verdict, provenance)` as the standalone original (FR-004,
  SC-002). The provenance `Inputs` (`FactId list`) are preserved because the prism keeps the
  consumed assertions' ids; the produced ids are preserved because the project `Identify` agrees
  with the domain's on injected facts (D5). This is the lower-level combinator the docs name
  `Rule.contramapFacts`; in our compose path we usually lift the **CheckRule** (D3) and bridge once
  at the root, but `Lift.rule` is exposed for the faithful-lift proof (it is the cleanest place to
  state provenance preservation) and for a consumer who has already bridged.
- **Alternatives considered**:
  - *Recompute provenance during the lift.* Rejected: it would let the lift change a fact's
    justification, breaking SC-002's "byte-for-byte identical provenance." The lift must be a pure
    re-targeting of the fact channel and nothing else.

## D5 — Module naming: a single `Lift` module (avoids shadowing the kernel's companion modules)

- **Decision**: All four lift combinators live in **one `Lift` module**: `Lift.check`,
  `Lift.checkRule`, `Lift.rule`, `Lift.fence`. The design docs' name `Rule.contramapFacts` is
  realized by `Lift.rule`.
- **Rationale**: the kernel already ships `Check` and `CheckRule` **companion modules**
  (`[<CompilationRepresentation(ModuleSuffix)>]`). Defining same-named modules in the Spi namespace
  would shadow or collide with them whenever both namespaces are open (the common case for an
  adapter author who `open`s the kernel and the SPI). A single neutral `Lift` module sidesteps the
  clash entirely and reads well at the use site (`r |> Lift.checkRule isMine`). `Lift.rule` is
  documented as the docs' `Rule.contramapFacts`. (`Fence` has no kernel companion module, so
  `Lift.fence` is also clash-free and kept in the same module for consistency.)
- **Alternatives considered**:
  - *`module Rule` with `contramapFacts` (matching the docs verbatim).* The kernel has no `Rule`
    *module* (only the `Rule<'fact>` type), so this one would not clash — but `Check`/`CheckRule`
    lifts would still need a home, and splitting the combinators across `Rule` + something-else is
    less coherent than one `Lift` module. Chosen against for consistency; the doc-name mapping is
    recorded instead.

## D6 — `Fence` lifts contravariantly on the change channel

- **Decision**: `Lift.fence : narrow:('big -> 'small) -> Fence<'small> -> Fence<'big>` re-targets a
  fence's `Trips: 'small -> bool` to `'big -> bool` via `Trips << narrow`, keeping its `Name`.
- **Rationale**: a `Fence`'s `Trips` is a pure predicate that only **consumes** the change, so it is
  contravariant in `'change` — composing adapters whose domain change types differ (design files vs
  Spec Kit phases) requires re-targeting each adapter's fences onto the project's change type,
  which is a total `narrow: 'projectChange -> 'domChange`. Keeping `Name` is what lets the composed
  fence set dedup by name (D8). This mirrors `Lift.check`'s contravariance — both read, neither
  produces.
- **Alternatives considered**:
  - *Require all adapters to share one `'change` type.* Rejected: it would force a domain to know
    the project's change shape, violating FR-006's independence; `Lift.fence` keeps each adapter's
    fences authored over its **own** change type and re-targets them only at the root.

## D7 — Composition reuses the kernel unchanged; it adds no evaluation or precedence code

- **Decision**: `Composition` ships **only** assembly folds — `lift` (per-adapter: lift its
  `CheckRule` catalog via `Lift.checkRule` and contramap its fences via `Lift.fence`, returning a
  `Lifted<'project,'change>`), `compose` (concatenate the lifted catalogs with the cross-domain
  `CheckRule<'project>` set and union the fences deduped by name, returning a
  `Composed<'project,'change>`), and the thin reuse `toRules: Bridge<'project> -> Composed<…> ->
  Rule<'project> list` (= `Catalog |> List.map (CheckRule.toRule bridge)`). Evaluation is the
  **unchanged** `FixedPoint.evaluate identify (toRules bridge composed) supplied`; routing is the
  **unchanged** `Route.route composed.Fences composed.Catalog mode change`.
- **Rationale**: FR-005 requires the composed catalog to "evaluate through the unchanged kernel …
  adding no new evaluation logic; the kernel gains no adapter-specific code." FR-007/FR-008 require
  cross-domain precedence to be deterministic and order-independent. Both fall out of the kernel
  **for free**: the least fixed point is forward chaining with monotonic rules, so it has a unique
  result regardless of rule order (the Datalog guarantee, theory doc §"Confluent by construction"),
  and the merge precedence — a blocking result always wins, default allow-unless-fenced — is F07's
  `Route` (`stakesOf`/`route` are already proven order-independent: gate sets are deduped unions,
  the partition depends on stakes/mode not fence order). So the composition root **does not
  reimplement** confluence or precedence; it reuses them. The "deterministic combinator" of FR-007
  is therefore the pair (kernel LFP + F07 route), and a cross-domain rule is just an `Implies`
  `CheckRule<'project>` placed in `compose`'s `crossDomain` argument (D9).
- **Alternatives considered**:
  - *A bespoke `merge`/precedence function in `Composition`.* Rejected: it would duplicate F07's
    `route` and risk re-introducing the positional, first-match-wins hazard (theory Hazard 5) the
    kernel already forbids. Reuse is both less code and provably confluent.

## D8 — The concrete `ProjectFact` coproduct is authored at the root by the consumer

- **Decision**: F09 ships the **generic** machinery (`lift`/`compose` over a type parameter
  `'project`); the **concrete** `ProjectFact = Design of … | SpecKit of … | … | Governance of
  RuleOutcome` coproduct, its single-case active patterns, its `inject` helpers, and the project
  `Identify: 'project -> FactId` and `Bridge<'project>` are **authored by the consumer** at the one
  composition root (in the test example adapters here; by F12 for a real project). The project
  carries its **own** `Governance of RuleOutcome` case so that `Bridge<'project>.Embed = Governance`
  and `Project = (function Governance o -> Some o | _ -> None)` are uniform across all domains — a
  cross-domain cache-hit lookup finds a recorded verdict regardless of which domain authored the
  rule.
- **Rationale**: FR-003/FR-012 require the coproduct to be "a single closed coproduct" assembled at
  "the one place," and the spec Edge Case "Adding a new domain is a central edit to the coproduct
  at the root (the closed-union trade), not an open third-party plug-in." A closed sum cannot be
  shipped generically — its cases *are* the participating domains, which differ per project. So the
  shipped library is parameterized over `'project` and the consumer writes the (short, single-case)
  coproduct. The dedicated `Governance` case keeps the project `Bridge` total and uniform (research
  on F04's `Embed`/`Project`): without it, a governance outcome would have to be embedded into some
  arbitrary domain's slot and the cross-domain `Project` would be ambiguous. The project `Identify`
  delegating per case (`identify (inject f) = domainIdentify f`) is the **lifting law** that makes
  provenance ids survive the lift (D4) — stated as a documented law (Hazard 4: injective
  `identify`).
- **Alternatives considered**:
  - *Ship a fixed `ProjectFact` in the Spi.* Rejected: it would hard-code the participating domains
    into the SPI, defeating "the kernel is a library, not a platform" — the whole point is that the
    set of domains is the consumer's choice, made at one reviewable root.
  - *An open coproduct (true Data Types à la Carte with `:<:` injection).* Rejected per the theory
    doc: we **deliberately keep the coproduct closed** (a single reviewable root) at the cost of
    hand-written lifting boilerplate, tamed by small `inject` helpers + single-case active patterns.
    Open third-party plug-in extensibility is explicitly out of scope.

## D9 — Cross-domain coupling is an `Implies` `CheckRule` over the coproduct; absent ⇒ inert via `Unmet`

- **Decision**: A cross-domain rule is an ordinary `CheckRule<'project>` whose `Check` is an
  `Implies` over the coproduct's facts (`taskTouchesDesign ==> hasRecordedReview`), passed to
  `compose`'s `crossDomain` argument — a **small, named, one-place** set. When a cross-domain rule's
  antecedent domain is **absent** from a composition, its antecedent probe reads project facts that
  are never present and must report **`Unmet`** ("this is definitely not a design task"), not
  `Unknown` — so `Implies(a,b) = Any[Not a; b]` evaluates with `Not (Unmet) = Pass`, making the
  rule **vacuously satisfied / inert** (never an error, never a silent fail).
- **Rationale**: FR-007 forbids ad-hoc glue and positional rules; an `Implies` over the coproduct
  is the theory doc's sanctioned form (1: in the AST via `Implies`; 2: at combine time via the
  fixed precedence — which is the reused kernel, D7). FR-009 requires a cross-domain rule naming an
  absent domain to be **inert**, not throw. The F03 `Outcome` three-valued distinction is exactly
  what makes this work: `Unmet` (a definite negative — "no design fact present, so this is not a
  design task") flips through `Not` to `Pass`, whereas `Unknown` (undecided) would leave the
  `Implies` `Unknown` and could block. So the **probe-authoring guideline for cross-domain
  antecedents is: report `Unmet` when the domain's facts are absent, reserving `Unknown` for a
  genuinely undecided present check.** This is documented in the data model and exercised by the
  removal/boundary test (SC-004).
- **Alternatives considered**:
  - *Make an absent antecedent `Unknown`.* Rejected: it would leave cross-domain rules in an
    undecided state after a domain is removed, surfacing as routing noise rather than the clean
    "inert" the boundary test demands.
  - *Detect absent domains and prune cross-domain rules at compose time.* Rejected: it would add
    composition logic (and a notion of "which domains are present") the kernel does not need — the
    `Unmet`-⇒-inert behaviour is automatic and requires no pruning.

---

### Build order (consequence of D1/D7)
The new project compiles `Adapter.fsi`/`Adapter.fs` **then** `Composition.fsi`/`Composition.fs`
(`Composition` references `Adapter`'s `Adapter<…>`/`Lift` and the kernel). The project references
only `FS.GG.Governance.Kernel`; **zero new `PackageReference`** (D1). The test project references
Spi (Expecto + FsCheck, already pinned) and authors the concrete `ProjectFact` coproduct + two
unrelated example adapters (D8), disclosed as synthetic example domains (Principle V).
