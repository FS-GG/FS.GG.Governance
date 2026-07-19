# Implementation Plan: Profile-Bound Gate Inheritance (ADR-0049)

**Spec**: [spec.md](./spec.md) · **Item**: FS-GG/FS.GG.Governance#275 (WI-5) · **Tier**: 1

## Technology & Structure

- F# / .NET `net10.0`, standard Spec Kit. Pure leaf library + one CLI wiring point. No new
  third-party dependency (FSharp.Core only).
- Design-first (Constitution I/II): `.fsi` is the sole public surface; regenerate surface baselines
  with `BLESS_SURFACE=1 dotnet test`.

## Design (matches the current enforcement flow)

The gate union already happens in the `ShipCommand` loop as an **F081 consume-union fold** over
`RouteResult.SelectedGates`, immediately before `Ship.rollup`. Inheritance is a second fold in the
same place. `Ship.rollup` and `Enforcement.deriveEffectiveSeverity` are **not touched** (FR-007).

1. **`Config.Model.maturityRank : Maturity -> int`** — the total floor order
   (`observe 1 < warn 2 < block-on-pr 3 < block-on-ship 4 < block-on-release 5`), sibling of
   `costRank`. The floor comparison primitive (FR-004).

2. **New leaf project `FS.GG.Governance.Inheritance`** (deps: Config, Gates, Route). Public surface:
   - `referenceGatesFor : TemplateProfile -> Gate list` — the embedded reference floor. `game` binds
     one gameplay gate at `warn` (FR-001, FR-008), built through `Gates.buildRegistry` on a synthesized
     `TypedFacts` so the gate shape is identical to a local one (FR-009). Unknown profile → `[]`.
   - `productTemplateProfiles : TypedFacts -> TemplateProfile list` — distinct, sorted template-profiles
     off `Capabilities.Surfaces` (FR-002).
   - `inheritedGatesFor : TypedFacts -> Gate list` — union of `referenceGatesFor` over the product's
     profiles, deduped by `GateId`.
   - `composeEffectiveGates : inherited:Gate list -> local:Gate list -> Gate list` — union with the
     non-lowerable floor: shared id → local gate at `max(maturityRank)`; inherited-only → added;
     local-only → kept; sorted by `GateId` (FR-003/004/005). `inherited = []` ⇒ `local` unchanged
     (FR-006).
   - `applyInheritance : TypedFacts -> RouteResult -> RouteResult` — rebuilds `SelectedGates` from the
     composed set, preserving each existing `SelectingPaths` trace and giving inherited-only gates an
     empty trace (FR-005). Identity when the product declares no bound profile (FR-006).

3. **Wiring** — `ShipCommand/Loop.fs`, one line after the F081 fold:
   `let result = Inheritance.applyInheritance facts result` (before `Ship.rollup`).

## Verification

- Unit: `composeEffectiveGates` floor matrix (raise / can't-lower / inherited-only / identity);
  `referenceGatesFor game`; `productTemplateProfiles`; `applyInheritance` identity for a non-game
  product; `maturityRank` order.
- Surface-drift tests for the new project + regenerated Config baseline.
- Full Expecto suite (Debug + Release) green — proves `Ship.rollup` / `deriveEffectiveSeverity`
  verbatim (FR-007).

## Deferrals (explicit)

- The flip to `block-on-ship` and the reference-**sample** binding: WI-8 (FS-GG/FS.GG.Governance#276).
- Canonical **ADR-0049** text: authored in `FS-GG/.github` (filed as a cross-repo request); this repo
  carries the index row + a decision record.
