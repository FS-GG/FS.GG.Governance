# Feature Specification: Profile-Bound Gate Inheritance (ADR-0049)

**Feature Branch**: `item/275-profile-gate-inheritance`

**Created**: 2026-07-19

**Status**: Draft

**Input**: Coordination board item **FS-GG/FS.GG.Governance#275** — *WI-5 of epic FS-GG/.github#1190
("Headless gameplay testing — per-FR block-on-ship teeth")*: "Profile-bound gate inheritance
(ADR-0049)." Blocked by WI-4 (FS-GG/FS.GG.SDD#584, landed 2026-07-19). Contract: governance config /
governance-handoff. Decides: **ADR-0049**.

## Why This Feature

Today every product's enforcement derives **solely from its own local `.fsgg/`**. `TemplateProfile`
— the template a `generatedProduct` root was instantiated from (e.g. `game`) — is parsed and
recorded on a `Surface`, but it is **provenance-only**: nothing reads it for any decision. This is
the "policy is purely local" invariant: there is no embedded, default, or cross-product policy that
a product inherits.

The epic needs the opposite for *gameplay* obligations. A game scaffolded on the `game` profile must
carry a per-FR gameplay gate as a **floor it cannot lower** — a product must not be able to delete or
downgrade that gate by editing its own `.fsgg/`. That requires a gate declared **once**, org-side,
that binds to a template-profile and is inherited by every product carrying that profile at
enforcement time.

This feature makes `TemplateProfile` a **lookup key**. An embedded, org-owned reference floor binds
gates to template-profiles; a product with a bound profile **inherits** those gates as a
non-lowerable floor, unioned with its local gates before the ship rollup. It **retires the
"policy is purely local" invariant** — a Tier-1 contract change, decided by ADR-0049.

**Publish-before-flip.** This feature lands the *mechanism* and binds the `game` gameplay gate at a
**non-blocking** maturity (`warn`). It therefore changes **no** product's ship verdict: a game that
was shippable stays shippable. WI-8 (FS-GG/FS.GG.Governance#276) performs the actual flip to
`block-on-ship` once WI-7's reference-game proof is green. Landing the contract first is what lets
WI-8 be a maturity change rather than a mechanism change.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A product inherits a profile-bound gate it never declared (Priority: P1)

A product whose `generatedProduct` root declares `templateProfile: game` is enforced with the
org-owned gameplay gate present in its effective gate set at ship time — **even though its own
`.fsgg/` never declared that gate**. A product with no bound template-profile (or a profile with no
binding) inherits nothing and enforces exactly as before.

**Why this priority**: This is the entire mechanism — inheritance keyed by `TemplateProfile`. Without
it, an org-owned gate cannot reach a product that did not opt in locally, which is the whole point of
a profile floor.

**Independent Test**: Roll up a ship decision for a product whose facts carry a `game` template-profile
and confirm the gameplay gate appears in the enforced items; roll up the same product with the profile
removed and confirm it does not.

**Acceptance Scenarios**:

1. **Given** a product with `templateProfile: game` and no local gameplay gate, **When** the ship
   rollup runs, **Then** the effective gate set includes the profile-bound gameplay gate.
2. **Given** a product with no template-profile, **When** the ship rollup runs, **Then** the effective
   gate set is exactly its local selected gates (byte-identical to today).
3. **Given** a product whose template-profile has no reference binding, **When** the ship rollup runs,
   **Then** it inherits nothing.

---

### User Story 2 - A product cannot remove or downgrade an inherited gate (Priority: P1)

A product that declares the same gate id locally at a **weaker** maturity than the inherited floor is
enforced at the **floor** maturity, not its local one. A product that declares it at a **stronger**
maturity keeps its stronger choice. Deleting the gate from local `.fsgg/` does not remove it — it is
re-supplied by inheritance.

**Why this priority**: A floor that a product can lower is not a floor. The non-lowerable property is
the security-relevant half of the contract: it is what makes an org gate a guarantee rather than a
suggestion.

**Independent Test**: Compose an inherited gate at `block-on-ship` with a local gate of the same id at
`warn`; confirm the effective maturity is `block-on-ship`. Repeat with the local gate at
`block-on-release`; confirm the effective maturity stays `block-on-release`.

**Acceptance Scenarios**:

1. **Given** an inherited gate at maturity M and a local gate of the same id at a **lower**-ranked
   maturity, **When** the sets are composed, **Then** the effective maturity is **M** (raised).
2. **Given** an inherited gate at maturity M and a local gate of the same id at a **higher**-ranked
   maturity L, **When** the sets are composed, **Then** the effective maturity is **L** (local can
   raise; inheritance never lowers a locally-stronger choice).
3. **Given** a product with no local gate of that id, **When** the sets are composed, **Then** the
   inherited gate is added verbatim.

---

### User Story 3 - Publish-before-flip: the mechanism ships without breaking any game (Priority: P2)

The `game` profile's gameplay gate binds at a **non-blocking** maturity (`warn`) in this feature. A
game product's ship verdict is therefore unchanged: the inherited gate is surfaced (as a warning /
passing item) but never blocks. WI-8 later raises the binding to `block-on-ship`.

**Why this priority**: The mechanism is only safe to land ahead of the reference-game proof (WI-7) if
it cannot make an existing game unshippable. This guard is what decouples WI-5's contract landing from
WI-8's teeth.

**Independent Test**: Roll up a game product with only the inherited (`warn`) gameplay gate selected;
confirm the verdict is `Pass` and the gate appears as a non-blocker.

**Acceptance Scenarios**:

1. **Given** a game product whose only gameplay coverage is the inherited `warn` gate, **When** the
   ship rollup runs at any mode/profile, **Then** the verdict is not `Fail` on account of the inherited
   gate.
2. **Given** the full existing test suite, **When** it runs against this change, **Then** every prior
   ship/enforcement assertion is unchanged (the enforcement core `deriveEffectiveSeverity` and
   `Ship.rollup` are used verbatim, not edited).

---

### Edge Cases

- **No template-profile declared**: the product inherits nothing; the rollup is byte-identical to
  today (the identity case, mirroring the existing F081 consume-union fold).
- **Multiple surfaces with different template-profiles**: the inherited set is the union over all
  distinct declared template-profiles, deduplicated by gate id.
- **Inherited id collides with a local id at equal maturity**: the local gate is kept (its selection
  trace is preserved); the maturity is unchanged (max of equal is equal).
- **Unknown / unbound template-profile string**: yields no inherited gates (not an error) — an
  unrecognized profile never fabricates a gate.
- **Determinism**: the effective gate set is sorted by `GateId` ordinal, so identical facts yield a
  byte-identical set regardless of input order.

## Requirements *(mandatory)*

### Change Classification

**Tier 1 (contracted change).** This feature **retires a documented invariant** ("policy is purely
local") and adds public API surface (a new `Inheritance` module; a `maturityRank` ordering on the
config model). It therefore carries the full Tier-1 obligations: `.fsi` updates, surface-area baseline
updates, spec, test evidence, documentation, and an ADR. The canonical **ADR-0049** text is authored
in `FS-GG/.github` (org ADRs live there); this repo adds the local index row in `docs/adr/README.md`
and a repo decision record.

### Functional Requirements

- **FR-001**: `TemplateProfile` MUST become a **lookup key**: an embedded, org-owned reference floor
  MUST map a `TemplateProfile` to a set of inherited gates. An unbound/unknown profile maps to the
  empty set (never an error, never a fabricated gate).
- **FR-002**: The system MUST resolve the set of `TemplateProfile`s a product declares from its
  `Capabilities.Surfaces` (`Surface.TemplateProfile`), deterministically and deduplicated.
- **FR-003**: The effective gate set MUST be the **union** of the product's local selected gates and
  its inherited gates, deduplicated by `GateId` and sorted by `GateId` ordinal.
- **FR-004**: For a gate id present in **both** sets, the effective maturity MUST be the
  **higher-ranked** of (local, inherited) per a total `maturityRank` order
  (`observe < warn < block-on-pr < block-on-ship < block-on-release`). A local gate MAY **raise** an
  inherited floor; it MUST NOT **lower** it.
- **FR-005**: A gate present **only** in the inherited set MUST be added to the effective set verbatim,
  carrying an empty selection trace (`SelectingPaths = []`) — it is present because inherited, not
  because a changed path selected it.
- **FR-006**: A product that declares **no** bound template-profile MUST produce an effective gate set
  **identical** to its local selected gates (the identity case; no re-sort that changes bytes).
- **FR-007**: Inheritance MUST feed the **existing** enforcement path unchanged: the composed gate set
  is rolled up by the existing `Ship.rollup` through the existing `deriveEffectiveSeverity`. Neither
  the ship rollup nor the enforcement core is edited (verbatim reuse).
- **FR-008**: The `game` profile MUST bind a gameplay gate at a **non-blocking** maturity (`warn`) in
  this feature (publish-before-flip). This feature MUST NOT change any product's ship **verdict**.
- **FR-009**: The inherited gates MUST be single-sourced through the same `Check → Gate` projection
  (`Gates.buildRegistry`) that produces local gates, so an inherited gate is indistinguishable in shape
  from a locally-declared one.

### Key Entities

- **InheritedGate**: a `Gate` (F018) supplied by the reference floor for a template-profile, rather
  than projected from the product's own `.fsgg/`.
- **Reference floor**: the embedded, deterministic mapping `TemplateProfile → Gate list`. The single
  in-code source of profile-bound gates; retires the "no embedded/default policy loader" invariant
  (ADR-0049).
- **Effective gate set**: the deduplicated, floored union of a product's local and inherited gates,
  the input the ship rollup enforces.

### Out of Scope

- **The flip to `block-on-ship`** (WI-8 / FS-GG/FS.GG.Governance#276): raising the `game` gameplay
  binding maturity, and aligning the reference **sample** `.fsgg/`. Owned by WI-8's touch-set
  (`samples/`, `packaging/`).
- **The `route --mode gate` handoff-gate path**: that path enforces SDD-handoff gates (spec 090), not
  the product's capability gate set. Capability-gate inheritance is folded at the `fsgg ship` **and**
  `fsgg verify` rollups (both use `Ship.rollup` over the product's selected gates) so the two do not
  diverge once WI-8 raises the binding to `block-on-ship`; the SDD-handoff `route --mode gate` path is
  unaffected.
- **A `.fsgg`-schema field for profile bindings**: this feature's binding is embedded in the
  governance runtime (ADR-0049 decision). Expressing bindings in the reference set's own `.fsgg/` is a
  possible later hardening once WI-8 lands the gameplay gate in the sample.
