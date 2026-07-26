# ADR 0010 — Production-journey evidence is an inherited, non-lowerable game-profile floor

**Status**: Accepted · **Date**: 2026-07-26 · **Feature**: `specs/116-production-journey`

**Producer contracts**: [FS.GG.Game#507](https://github.com/FS-GG/FS.GG.Game/issues/507) and
[FS.GG.SDD#709](https://github.com/FS-GG/FS.GG.SDD/issues/709). **Consumer item**:
[FS.GG.Governance#324](https://github.com/FS-GG/FS.GG.Governance/issues/324).

## Context

`gameplay:fr-covered` proves that ordinary gameplay requirements have observed verification. It
does not distinguish a test that enters through the shipped boot/input/update composition from a
helper call over a constructed state. Game now issues machine-bound production-journey receipts,
and SDD 0.30 validates their route, scenario, script, trace, terminal predicate, report identity,
digest, and authored provenance before projecting merge-boundary readiness.

Governance must enforce that stronger promise without duplicating the Game receipt validator or
letting a product locally opt out. It must also remain compatible with pre-0.30 SDD handoffs long
enough to diagnose and migrate them honestly.

## Decision

1. **SDD owns receipt validation; Governance owns the floor.** Governance consumes the published
   v2 `governance-handoff.json` boundary using its existing `FS.GG.Contracts` dependency and adds no
   SDD or Game assembly/project reference. It preserves
   `readiness.counts.journeyObligationsUnmet`, blocking diagnostic ids, and related
   requirement/scenario ids as typed facts. It does not infer production provenance from test
   names, paths, green TRX files, or prose.

2. **The producer line determines requiredness.** Handoffs from
   `FS.GG.SDD.Artifacts/0.30.0` and later compatible producer lines must contain the journey count.
   Its absence is malformed, never zero. Older producer lines remain in an explicit compatibility
   window: absence yields no journey-readiness fact, so operators can distinguish “not promised”
   from “promised and zero.”

3. **Contradictions fail closed.** Negative counts, a ship-ready disposition paired with a non-zero
   unmet count, and zero unmet paired with a canonical journey-receipt failure diagnostic reject the
   handoff. Only SDD's canonical `evidence.productionJourneyReceiptInvalid` and
   `evidence.productionJourneyReceiptStale` ids classify journey provenance; unrelated readiness
   diagnostics cannot mint a journey disposition. Unsupported contract majors and
   unreadable/malformed JSON continue through the existing integrity-blocking path. A non-zero
   valid count produces a `block-on-ship` journey evidence gate carrying the journey diagnostics
   and affected ids.

4. **Every `game` profile inherits `gameplay:production-journey`.** The embedded reference floor
   and published reference gate set both add the command-free check at `block-on-ship`.
   `composeEffectiveGates` applies the existing maturity maximum, so a product may strengthen the
   gate but cannot lower or remove it. Non-game profiles and `gameplay:fr-covered` are unchanged.

5. **The static floor and evidence verdict have separate identities.** The inherited gate is
   `gameplay:production-journey`. Each handoff projects
   `gameplay:production-journey:<work-id>` with advisory maturity at zero unmet and blocking
   maturity otherwise. This keeps the organization policy stable while retaining per-work evidence
   provenance and avoids pretending that a static declaration itself is a receipt.

## Rollout

The order is Game receipt producer, SDD 0.30 validator/handoff producer, then this Governance
consumer and reference-set 1.5.0. The final maturity flip is part of this decision because the Game
reference proof and a real Rogue-shaped adoption path existed before the floor was added. Exact-pin
reference-set consumers must deliberately adopt 1.5.0.

## Consequences

- Helper/component evidence remains valid for `gameplay:fr-covered`; it cannot satisfy a declared
  production journey.
- Missing, stale, forged, simulation-origin, exhausted, or otherwise invalid receipts remain
  producer diagnostics and block through a typed Governance gate.
- The consumer trusts SDD's versioned validation result. Re-validating Game receipts here would
  create a second, drifting authority and is deliberately rejected.
- A future handoff contract may add total journey-obligation counts or richer per-receipt facts.
  Additive v2 fields remain forward-compatible; Governance must preserve them explicitly before it
  relies on them.

## Alternatives considered

- **Treat generic real evidence as a journey.** Rejected: it reproduces the helper-only false green.
- **Validate Game receipts directly in Governance.** Rejected: it duplicates SDD's classification,
  report-byte, and provenance authority and would require a source/assembly dependency.
- **Let local game configuration opt in.** Rejected: a product could remove the organization promise
  the floor exists to protect.
- **Interpret an absent count as zero.** Rejected: after SDD 0.30 promises the field, absence is
  malformed evidence, not proof that no journey exists.
