// Curated public signature contract for the adapter SPI & lift combinators (F09).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution
// Principle II). The matching Adapter.fs carries NO `private`/`internal`/`public`
// modifiers on top-level bindings — visibility is presence/absence here.
//
// Design-first artifact: this contract is drafted and exercised in FSI
// (scripts/prelude.fsx) before any Adapter.fs body exists (Principle I). The shapes
// mirror docs/governance-design/adapters.md ("What an adapter supplies") and
// docs/governance-design/theory-and-composition.md ("Taming lifting boilerplate" — the
// closed-coproduct specialization of Data Types à la Carte). It STARTS Milestone M3 (the
// adoption bar). It is PURE — values and total folds, no I/O, no Model/Msg/Effect, no
// interpreter (Constitution Principle IV is N/A).
//
// This is the thin, total contract a domain hands the kernel to be governed — and the
// semantics-preserving lift that re-targets a rule's fact channel onto a project coproduct.
// It reuses F04 `CheckRule`/`Bridge`, F03 `Check`/`Probe`/`ArtifactRef`/`Outcome`, F07
// `Fence`, and F01 `Rule`/`FactSet`/`FactId`/`ProvenanceStep`; zero new dependencies. An
// adapter re-implements NONE of inference, arbitration, evidence, rendering, hashing,
// explanation, severity, or routing — those are all reused from the kernel (FR-002).

namespace FS.GG.Governance.Adapters.Spi

open FS.GG.Governance.Kernel

/// The five-part contract a domain supplies to be governed — and NOTHING more (FR-001). It is
/// the typed shape of "what a domain must hand the kernel"; everything cross-cutting is reused
/// (FR-002). Because it is a RECORD it is TOTAL: an adapter that omits a component does not
/// compile — adoption is never silently partial (FR-014, a typed boundary error). The three
/// type parameters are the domain's own vocabulary: `'fact` the closed fact union, `'artifact`
/// the domain's artifact kinds, `'change` the domain's change shape. None is shared with or
/// copied from another domain (FR-006/FR-010).
///
/// The FIVE adoption components are: (1) the closed `'fact` union — named by `Identify`;
/// (2) `ToRef` — the artifact mapping; (3) `Probes` — the declared atomic predicates;
/// (4) `Rules` — the catalog; (5) `Fences` — the high-stakes surfaces. `Bridge` is F04 KERNEL
/// WIRING the domain instantiates (how the domain-neutral `RuleOutcome` embeds in `'fact`, the
/// judge identity, the artifact-content hash read from facts) — not a re-implementation of any
/// cross-cutting facility (research D2).
type Adapter<'fact, 'artifact, 'change> =
    { /// (1) Identity of the closed `'fact` union — the SOLE authority on fact identity the
      /// kernel folds with (F01 `FixedPoint.evaluate`). MUST be injective on value-bearing
      /// facts (theory Hazard 4), and at a composition root the project `Identify` MUST agree
      /// with this on injected facts, so provenance ids survive the lift (data-model law L3).
      Identify: 'fact -> FactId
      /// (2) The artifact mapping: the domain's own artifact vocabulary onto the kernel's
      /// structural `ArtifactRef` (F03) — the single point where domain artifacts meet the
      /// otherwise domain-neutral algebra.
      ToRef: 'artifact -> ArtifactRef
      /// (3) The declared atomic predicates the catalog composes (F03 `Probe`). The DECLARED
      /// probe vocabulary, carried for the contract and for testing; the `Rules`' checks are
      /// authoritative for evaluation (research D2).
      Probes: Probe<'fact> list
      /// (4) The rule catalog: each rule a `Check` + `CheckTier` + `Severity` (+ `Spec`/
      /// `Question`) — an F04 `CheckRule<'fact>`. MAY be empty where a domain has no rules.
      Rules: CheckRule<'fact> list
      /// (5) The high-stakes surfaces for routing (F07 `Fence`). MAY be empty.
      Fences: Fence<'change> list
      /// F04 kernel wiring: how the domain-neutral `RuleOutcome` embeds into and projects out of
      /// `'fact`, the judge identity folded into the agent-review cache key, and the
      /// artifact-content hash read FROM the facts (no live I/O). NOT new cross-cutting code.
      Bridge: Bridge<'fact> }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Adapter =

    /// Translate an adapter's authored `CheckRule<'fact>` catalog into the kernel's executable
    /// `Rule<'fact>` list (F01) via the UNCHANGED `CheckRule.toRule adapter.Bridge` — the thin
    /// reuse that lets a single adapter govern itself standalone (US1): assert sensed facts, run
    /// `FixedPoint.evaluate adapter.Identify (toRules adapter) supplied`, route with
    /// `Route.route adapter.Fences adapter.Rules mode change`, render/hash/explain with `Check.*`.
    /// The adapter contains NONE of those facilities — they are all the kernel's (FR-002, SC-001).
    /// Total; performs no I/O and no agent call.
    val toRules: adapter: Adapter<'fact, 'artifact, 'change> -> Rule<'fact> list

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Lift =

    // ── The semantics-preserving lift: re-target a rule's fact channel onto a coproduct ──
    // (FR-004). A `Check`/`Fence` only READS its channel, so its lift is CONTRAVARIANT (the
    // projection direction only — one argument, matching the design docs' ergonomics). The
    // executable `Rule` both reads AND produces facts, so its lift is INVARIANT (prism +
    // injection). The lift adds NO behaviour: it re-targets the channel and nothing else.

    /// Lift a `Check<'small>` to `Check<'big>` by projecting the fact set each atomic/opaque
    /// probe `Eval` consumes (`project`, a single-case prism), KEEPING every `FactAssertion`'s
    /// `Id` and `Provenance` and re-mapping only its `Value`. The probe's declared `Name`/
    /// `Reads`/`Args` and the combinator structure (`All`/`Any`/`Not`/`Implies`/`Opaque`) are
    /// UNTOUCHED, so the four execution-free interpreters are INVARIANT under lifting:
    /// `Check.render`, `Check.hash`, `Check.reads`, and `Check.isReified` are byte-for-byte
    /// identical to the original (research D3) — the F04 agent-review cache key DOES NOT MOVE
    /// (SC-002), and a lifted `Opaque` stays opaque (out of `Deterministic`, routes to review).
    /// `eval`/`explain` over big facts equal the original over the projected facts. Total.
    val check: project: ('big -> 'small option) -> check: Check<'small> -> Check<'big>

    /// Lift a `CheckRule<'small>` to `CheckRule<'big>` — preserving `Id`/`Tier`/`Spec`/
    /// `Severity`/`Question` and lifting ONLY its `Check` via `check project` (FR-004). This is
    /// the primary lift for composition: the lifted catalog feeds BOTH evaluation
    /// (`CheckRule.toRule` → `FixedPoint`) AND routing/contract (F07 `Route.route`, F06
    /// `Contract.ofRules`), which consume `CheckRule`, not the bridged `Rule`. Total.
    val checkRule: project: ('big -> 'small option) -> rule: CheckRule<'small> -> CheckRule<'big>

    /// Lift an EXECUTABLE `Rule<'small>` to `Rule<'big>` (the design docs' `Rule.contramapFacts`):
    /// project the input fact set (`project`, keeping each assertion's `Id`/`Provenance`), run
    /// the inner `Apply`, then `inject` each produced assertion's `Value` back to `'big` while
    /// keeping its `Id` and `ProvenanceStep` VERBATIM. INVARIANT (both directions) because a rule
    /// consumes and produces facts. Provenance-preserving: evaluating the lifted rule over
    /// coproduct-wrapped facts yields the IDENTICAL `(verdict, provenance)` as the standalone
    /// original, provided the project `Identify` agrees with the domain's on injected facts
    /// (data-model law L3) (FR-004, SC-002). Total.
    val rule:
        inject: ('small -> 'big) -> project: ('big -> 'small option) -> rule: Rule<'small> ->
            Rule<'big>

    /// Lift a `Fence<'small>` to `Fence<'big>` by re-targeting its `Trips` predicate via
    /// `narrow` (`Trips << narrow`), KEEPING its `Name` (so the composed fence set dedups by
    /// name, FR-011). CONTRAVARIANT on the change channel — a fence only reads the change. Lets
    /// adapters whose domain change types differ compose at one root, each authoring its fences
    /// over its OWN `'change` (FR-006). Total.
    val fence: narrow: ('big -> 'small) -> fence: Fence<'small> -> Fence<'big>
