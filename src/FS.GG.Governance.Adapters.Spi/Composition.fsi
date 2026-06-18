// Curated public signature contract for the composition root machinery (F09).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution
// Principle II). The matching Composition.fs carries NO `private`/`internal`/`public`
// modifiers on top-level bindings — visibility is presence/absence here.
//
// Design-first artifact: this contract is drafted and exercised in FSI
// (scripts/prelude.fsx) before any Composition.fs body exists (Principle I). The shapes
// mirror docs/governance-design/adapters.md ("Composing several adapters in one project") and
// docs/governance-design/theory-and-composition.md (the closed coproduct; cross-domain coupling
// as an explicit, deterministic, order-independent combinator). PURE — Principle IV is N/A.
//
// This is the GENERIC composition-root machinery: it lifts independent adapters into one closed
// coproduct `'project` and assembles their catalogs + fences into a single value the UNCHANGED
// kernel evaluates and routes. It adds NO evaluation or precedence logic of its own — confluence
// is the kernel's least fixed point (the Datalog guarantee, F01) and cross-domain precedence is
// F07's forbid-trumps-permit `Route` (a blocking result always wins; default allow-unless-fenced)
// (FR-005/FR-007/FR-008). The CONCRETE `'project` coproduct (with its own `Governance of
// RuleOutcome` case), its single-case active patterns, its `inject` helpers, and the project
// `Identify`/`Bridge` are authored by the CONSUMER at the one root (F12 for a real project; the
// test example adapters here) — F09 ships the machinery, not a fixed coproduct (research D8).
// Reuses F09 `Adapter`/`Lift`, F04 `CheckRule`/`Bridge`, F07 `Fence`, F01 `Rule`; zero new deps.

namespace FS.GG.Governance.Adapters.Spi

open FS.GG.Governance.Kernel

/// One adapter's contribution to a project, already lifted into the coproduct `'project` and the
/// project change `'change`: its rule catalog as `CheckRule<'project>` (via `Lift.checkRule`) and
/// its fences as `Fence<'change>` (via `Lift.fence`). Produced by `lift`; consumed by `compose`.
/// An adapter stays DUMB and INDEPENDENT — this value is the only thing the root holds about it,
/// and dropping it from the `compose` list removes the domain cleanly (the boundary test, FR-009).
type Lifted<'project, 'change> =
    { Rules: CheckRule<'project> list
      Fences: Fence<'change> list }

/// The assembled project: the single catalog the kernel evaluates and routes (every adapter's
/// lifted rules followed by the small, named cross-domain rule set) and the deduped-by-name union
/// of all fences. Non-generic over the domains — it carries only coproduct `CheckRule<'project>`
/// and `Fence<'change>`, so the kernel folds it with NO adapter-specific code (FR-005).
type Composed<'project, 'change> =
    { Catalog: CheckRule<'project> list
      Fences: Fence<'change> list }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Composition =

    /// Lift one adapter into the project: lift each rule's check via `Lift.checkRule project`
    /// (preserving tier/severity/spec/question and render/hash — SC-002) and contramap each fence
    /// via `Lift.fence narrow` (FR-004). `project` is the single-case prism `'project -> 'dom
    /// option` (the coproduct's active pattern, e.g. `(\|Design\|_\|)`); `narrow` re-targets the
    /// project change onto the domain's change. The lifting boilerplate the closed coproduct costs
    /// is confined HERE plus the consumer's one-line `inject`/active-pattern per domain (FR-006,
    /// theory "Taming lifting boilerplate"). Total; pure.
    val lift:
        project: ('project -> 'dom option) ->
        narrow: ('change -> 'domChange) ->
        adapter: Adapter<'dom, 'artifact, 'domChange> ->
            Lifted<'project, 'change>

    /// Assemble the composition root: concatenate every lifted adapter's catalog with the
    /// `crossDomain` rules (the small, NAMED, one-place set authored as `Implies` over the
    /// coproduct's facts, FR-012) into one `Catalog`, and union all fences DEDUPED BY NAME into
    /// one `Fences` (FR-011 — duplicate surfaces are not double-counted; route gate sets are
    /// unions). It adds NO precedence or merge logic: the merged verdict's determinism and
    /// order-independence come from the kernel's confluent fixed point (F01) and F07's `Route`
    /// (research D7). Because the catalog is a CONCATENATION and the fence union is a SET, the
    /// `Composed` value — and therefore the least fixed point and the merged route over it — is
    /// identical under every permutation of adapter-composition order and rule order (FR-008,
    /// SC-003/SC-007). A `crossDomain` rule whose antecedent domain is ABSENT is inert (its
    /// antecedent probe reports `Unmet`, so the `Implies` is vacuously satisfied — never an error,
    /// FR-009, research D9). Total; pure.
    val compose:
        lifted: Lifted<'project, 'change> list ->
        crossDomain: CheckRule<'project> list ->
            Composed<'project, 'change>

    /// Translate the composed catalog into the kernel's executable `Rule<'project>` list via the
    /// UNCHANGED `CheckRule.toRule bridge` (FR-005). `bridge` is the project `Bridge<'project>`
    /// the consumer authors at the root (with the coproduct's own `Governance of RuleOutcome`
    /// case as `Embed`/`Project`, so a cross-domain cache-hit lookup is uniform across domains —
    /// research D8). The kernel then runs `FixedPoint.evaluate identify (toRules bridge composed)
    /// supplied` and routes with `Route.route composed.Fences composed.Catalog mode change` — the
    /// composed project governs itself through the kernel with NO new evaluation logic (SC-006).
    /// Total; performs no I/O and no agent call.
    val toRules:
        bridge: Bridge<'project> -> composed: Composed<'project, 'change> -> Rule<'project> list
