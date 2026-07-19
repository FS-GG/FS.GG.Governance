# ADR 0009 — Profile-bound gate inheritance: an embedded reference floor, folded before the ship rollup, non-lowerable, published before it is flipped

**Status**: Accepted · **Date**: 2026-07-19 · **Feature**: `specs/113-profile-bound-gate-inheritance`

**Implements**: org **ADR-0049** (canonical text in `FS-GG/.github`; local index row in
[`docs/adr/README.md`](../adr/README.md)). **Item**: FS-GG/FS.GG.Governance#275 (WI-5 of epic
FS-GG/.github#1190).

## Context

Until now every product's enforcement derived **solely from its own local `.fsgg/`**. `TemplateProfile`
— the template a `generatedProduct` root was instantiated from (e.g. `game`) — was parsed onto a
`Surface` but read by nothing: **provenance-only**. This is the "policy is purely local" invariant:
there is no embedded, default, or cross-product policy a product inherits, and a product's `.fsgg/` is
the whole of its policy.

Epic FS-GG/.github#1190 needs the opposite for *gameplay* obligations: a game scaffolded on the `game`
profile must carry a per-FR gameplay gate as a **floor it cannot lower** — one it cannot delete or
downgrade by editing its own `.fsgg/`. That requires a gate declared **once**, org-side, bound to a
template-profile, inherited by every product carrying it.

## Decision

Make `TemplateProfile` a **lookup key** into an org-owned reference floor, and union the inherited
gates with the product's local gates as a **non-lowerable** floor, before the ship rollup. Four
sub-decisions, each with an alternative that was rejected:

1. **The reference floor is EMBEDDED in the governance runtime**, not loaded from a file or resolved
   from the published package at the product edge. `Inheritance.referenceGatesFor : TemplateProfile ->
   Gate list` is the single in-code source. This is the decision that **retires the "no
   embedded/default policy loader" invariant** — deliberately, because a floor a product could avoid by
   omitting a file would not be a floor. *Rejected:* resolving the floor from the installed
   `FS.GG.Governance.ReferenceGateSet` package — it adds an I/O edge to a pure decision and lets a
   product with an absent/edited copy escape the floor.

2. **Inheritance is a PRE-ROLLUP fold on the `RouteResult`, not a change to the enforcement core.**
   `Inheritance.applyInheritance : TypedFacts -> RouteResult -> RouteResult` composes the effective
   gate set immediately before `Ship.rollup`, in the exact place the loop already runs the F081
   consume-union fold. `Ship.rollup` and `Enforcement.deriveEffectiveSeverity` are used **verbatim** —
   an inherited gate blocks (once flipped) through the *same* derivation as any local gate. *Rejected:*
   teaching `deriveEffectiveSeverity` to union a gate set — it is a single-finding fold pinned
   byte-identical by the whole suite, and the union belongs at gate-set scope, not per-finding.

3. **The floor is NON-LOWERABLE via `maturityRank`.** For a gate id present both locally and inherited,
   the effective maturity is the **higher-ranked** of the two (`observe < warn < block-on-pr <
   block-on-ship < block-on-release`). A product may **raise** an inherited floor; it may never lower
   one. An inherited-only gate is added with an empty selection trace (present because inherited, not
   path-selected). *Rejected:* letting a local declaration win unconditionally — that is exactly the
   downgrade the floor exists to prevent.

4. **Inherited gates are single-sourced through `Gates.buildRegistry`.** The embedded floor is a
   `Check list` run through the same `Check -> Gate` projection as a product's own checks, so an
   inherited gate is indistinguishable in shape (`GateId`, description, timeout) from a local one.
   *Rejected:* hand-authoring `Gate` records — a second projection that would drift from the real one.

## Publish-before-flip

This feature is a **Tier-1 contract change** (it retires an invariant and adds public surface), and it
lands the mechanism **without changing any product's ship verdict**: the `game` gameplay gate binds at
`warn` (non-blocking). A game that was shippable stays shippable; the inherited gate is surfaced but
never blocks. WI-8 (FS-GG/FS.GG.Governance#276) raises the binding to `block-on-ship` once WI-7's
reference-game proof is green. Landing the contract first is what lets WI-8 be a **maturity change**
rather than a mechanism change — and it keeps the reference **sample** `.fsgg/` (WI-8's touch-set)
untouched here.

## Consequences

- `TemplateProfile` is no longer provenance-only; it governs which org gates a product inherits.
- The identity case is preserved: a product with no bound template-profile inherits nothing and its
  `ship.json` is byte-identical to before (the fold mirrors the F081 absent-handoff identity).
- The canonical ADR-0049 text is authored in `FS-GG/.github` (filed as a cross-repo request); this
  repo carries the index row and this record.
- Expressing profile bindings in the reference set's own `.fsgg/` (rather than embedded F#) is a
  possible later hardening once WI-8 lands the gameplay gate in the sample.
