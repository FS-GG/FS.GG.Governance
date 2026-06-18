// Curated public signature contract for the governance kernel core (F01).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution
// Principle II). The matching Kernel.fs carries NO `private`/`internal`/`public`
// modifiers on top-level bindings — visibility is presence/absence here.
//
// Design-first artifact: this contract is drafted and exercised in
// scripts/prelude.fsx / FSI before any Kernel.fs body exists (Principle I).
// The shapes mirror docs/governance-design/kernel.md (the authoritative model).

namespace FS.GG.Governance.Kernel

/// A stable identity for a fact, produced by the caller's `identify` function.
/// Used for deduplication and for naming inputs inside a justification.
type FactId = FactId of string

/// The identity of a rule, recorded in the provenance of every fact it derives.
type RuleId = RuleId of string

/// One justification step: the rule that fired and the input facts it consumed.
/// `Note` is a short human-/agent-readable description of the inference.
type ProvenanceStep =
    { Rule: RuleId
      Inputs: FactId list
      Note: string }

/// A fact together with its identity and justification.
/// `Provenance` is EMPTY for supplied (asserted) facts and non-empty for derived facts.
type FactAssertion<'fact> =
    { Id: FactId
      Value: 'fact
      Provenance: ProvenanceStep list }

/// The working set of facts. Deduplicated by `FactId` in any value the kernel returns.
type FactSet<'fact> = FactAssertion<'fact> list

/// A monotonic (add-only) rule. `Apply` maps the current fact set to zero or more
/// newly asserted facts, each carrying the `ProvenanceStep` that justifies it.
/// Rules are ordinary typed F# functions — there is no external rule language.
type Rule<'fact> =
    { Id: RuleId
      Description: string
      Apply: FactSet<'fact> -> FactAssertion<'fact> list }

/// The outcome of evaluation: the complete, deduplicated fact set (supplied + derived)
/// and the number of rounds that produced at least one new fact before quiescence.
type EvaluationResult<'fact> =
    { Facts: FactSet<'fact>
      Rounds: int }

module FixedPoint =

    /// Forward-chain `rules` over the `supplied` facts to the least fixed point.
    ///
    /// `identify` is the SOLE authority on fact identity: the kernel applies it to
    /// every fact (supplied and derived) to assign `FactId`, deduplicate, and name
    /// inputs in provenance. Two facts with the same id collapse to one entry.
    ///
    /// Evaluation is synchronous (each round applies every rule to the same snapshot),
    /// which makes both the final fact set AND each fact's provenance independent of
    /// rule order. Terminates for any monotonic rule set over a bounded fact space.
    ///
    /// Precondition (documented, not runtime-enforced): rules are monotonic (add-only).
    /// Negated, aggregated, or recursively-negated facts are SUPPLIED from a lower
    /// stratum, never derived within this fixed point.
    val evaluate:
        identify: ('fact -> FactId) ->
        rules: Rule<'fact> list ->
        supplied: FactSet<'fact> ->
        EvaluationResult<'fact>
