# Feature Specification: Generated-Product Capabilities

**Feature Branch**: `058-generated-product-capabilities`

**Created**: 2026-06-25

**Status**: Planned

**Input**: User description: "next item in plan" — roadmap **F23 · `023-generated-product-capabilities`**
(`docs/2026-06-18-governance-kernel-speckit-implementation-plan.md`), the first row of **Phase 10 —
Capability Catalog And Product Adapter Expansion** (`docs/initial-implementation-plan.md`): "Expand
`.fsgg/capabilities.yml` for generated products, package surfaces, docs, skills, samples, design artifacts,
release surfaces, baselines, template profiles, and evidence tags," "Add cost-tiered generated-product
gates: structural scan, restore/build, focused tests, full verify, and release validation," and "Ensure
generated products can run Governance locally without monorepo access." This feature carries the MVP
capability catalog (F014/F016) past routing-only into the full product surface vocabulary the capability
design names — so a generated product can **declare**, **route**, **classify**, and **cost-tier** its own
package, docs, skills, design, and release surfaces, and run Governance against them from outside the
monorepo. The concrete per-domain deterministic *check execution* is the next feature (F24), not this one.

Two scope decisions are confirmed for this feature (the requester selected F23 over F24 at specification
time, confirming the catalog/schema-expansion scope):

1. **This feature makes generated-product surfaces *declarable, routable, classifiable, and cost-tiered*;
   it does not implement the per-domain *deterministic checks themselves*.** F23 expands the capability
   catalog vocabulary, extends routing/classification to the new surface kinds, selects generated-product
   checks by cost tier, and lets a generated product run Governance locally. The concrete rule packs that
   actually *evaluate* each surface — package `.fsi`-baseline drift, FSI transcripts, docs links, skill
   path contracts, design tokens/contrast — are feature **F24 (`024-package-docs-skills-design-checks`)**
   and out of scope here. F23 makes the surfaces *known and routed*; F24 makes them *checked*.

2. **This feature evolves the F014 capability schema — by design.** Recent host-command rows
   (`release`/`verify`/`refresh`) were careful to reuse the frozen `.fsgg` four-file schema *verbatim*.
   This row is the opposite: it is the feature in which `.fsgg/capabilities.yml` is **expanded** past its
   MVP shape (domains / path map / surfaces / checks) into the full product-surface vocabulary. That makes
   it a versioned, contracted schema change carrying a documented **migration** from the MVP catalog —
   strict validation, deterministic ordering, and duplicate-id / unknown-field diagnostics all extend to
   the new fields. `.fsgg/project.yml`, `.fsgg/policy.yml`, and `.fsgg/tooling.yml` are untouched.

## Overview

Today a governed repository's `.fsgg/capabilities.yml` (F014) declares a **minimum-viable** catalog: a set
of capability **domains**, a glob **path map** routing changed paths to those domains, a list of classified
**surfaces** (routine / governed-root / protected-surface / generated-view / release-surface) with owner and
maturity, and a list of **checks** carrying owner, cost, environment, and maturity. That MVP answers the
three questions the capability design demands — *what changed*, *why a gate would run*, and *what governed
path is unknown* — but only for the two MVP domains. It does **not** yet let a **generated product** — a
repository created from a template that consumes Governance from the outside — declare the full set of
product surfaces it owns: its package/API surfaces and their baselines, its generated roots and the template
profile they were instantiated from, its sample apps, its docs and examples, its skills, its design
artifacts, and its release surfaces, each tagged with the evidence that proves it current.

Because those surface kinds cannot be declared, two failures follow. First, a generated product **cannot
protect its own surfaces**: a change to a package surface, a docs set, a skill, or a release surface routes
the same as any routine file, so heavy checks cannot be required against a positive match. Second — and the
capability design calls this out as a safety hole — a generated product could **hide a new protected
surface under a governed root**: drop a new public-API project, a readiness surface, or a release surface
under an already-governed directory and, with no capability metadata for it, it would pass unclassified.

What is still missing is the catalog vocabulary, the routing that consumes it, and the cost-tiered gate
*selection* that together let a generated product **declare enough capability scope to protect its own
package, docs, skills, design, and release surfaces** — and to do so **locally, without access to the
monorepo it was generated alongside**. This feature delivers exactly that: it expands
`.fsgg/capabilities.yml` to model generated product roots, template profiles, package pins, product tests,
generated guidance, sample apps, docs/examples, skills, design artifacts, release surfaces, baselines, and
evidence tags; it extends routing and surface classification to those new kinds so that changes route to
the right capability and **no new protected surface can hide under a governed root without classification**;
it lets a repository declare generated-product checks at **cost tiers** (structural scan → restore/build →
focused tests → full verify → release validation) so routing selects the right depth for a change and
profile while keeping local authoring cheap; it ensures a generated product checked out **standalone** can
run Governance against its own catalog with no monorepo dependency; and it ships a **versioned schema with a
documented migration** from the MVP catalog so existing governed repositories evolve safely.

This feature *declares, routes, classifies, and cost-tiers* the product surfaces. It does **not** evaluate
them: whether a package baseline has actually drifted, whether an example actually runs, whether a docs link
actually resolves, whether a skill's path contract actually holds — those deterministic checks are the next
feature (F24). The two compose: F23 says *this surface exists, here is its capability, its cost tier, and
the evidence tag that would prove it current*; F24 supplies the rule pack that produces that evidence.
Product vocabulary stays in the capability catalog and the product-facing adapters, never in the Governance
kernel — the same product-neutrality discipline the existing rows hold.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Declare a generated product's full surface set and have changes routed and classified (Priority: P1)

A maintainer of a generated product — a repository created from a template that consumes Governance from
outside the monorepo — expands their `.fsgg/capabilities.yml` beyond the MVP catalog to declare the product
surfaces they own: their package/API surfaces (and the baselines that pin them), their generated roots and
the template profile each was instantiated from, their sample apps, their docs and examples, their skills,
their design artifacts, and their release surfaces — each tagged with the evidence that would prove it
current. From then on, when the maintainer changes a file under any declared surface, Governance routes the
change to the matching capability and classifies the surface (package / docs / skills / design / release /
generated-product), so heavy checks can be required against a positive match. Crucially, when a *new*
protected surface — a public-API project, a readiness surface, a release surface — is placed under a
governed root **without** a capability declaration, Governance surfaces it as an unknown-governed-path
finding rather than letting it pass unclassified.

**Why this priority**: This is the entire reason the feature exists and the only slice that delivers
end-to-end value — the first time a generated product can declare and route its own product surfaces and
the first time a new protected surface can no longer hide under a governed root. Every other story refines,
cost-tunes, or hardens this flow.

**Independent Test**: Point Governance at a generated-product fixture whose `.fsgg/capabilities.yml`
declares one surface of every new kind; change a file under each in turn and confirm each change routes to
the expected capability and surface classification; then add a new public-API surface under a governed root
with no capability declaration and confirm Governance reports it as an unknown governed path rather than
classifying it as routine.

**Acceptance Scenarios**:

1. **Given** a generated product whose catalog declares a package surface, a generated root with a template
   profile, a sample app, a docs/examples set, a skill, a design artifact, a release surface, and a
   baseline, **When** the maintainer changes a file under any one of them, **Then** Governance routes the
   change to that surface's capability and reports its surface classification.
2. **Given** a new public-API (or readiness, or release) surface added under an already-governed root with
   **no** capability metadata, **When** Governance routes the change, **Then** it emits an unknown-governed-
   path finding for that surface rather than treating it as routine or silently passing it.
3. **Given** a routine/unmanaged path that matches no declared surface or fence, **When** Governance routes
   a change to it, **Then** it stays cheap (no heavy product route) — the light-by-default contract is
   preserved.
4. **Given** a catalog that declares only some of the new surface kinds (for example package and release
   surfaces but no skills), **When** Governance routes changes, **Then** only the declared surfaces are
   classified and governed, and the undeclared kinds are simply absent (declaring a subset is not an error).

---

### User Story 2 - Cost-tiered generated-product gate selection (Priority: P2)

The same maintainer declares the checks that protect their generated-product surfaces at **cost tiers** —
from a cheap structural scan, through restore/build and focused tests, to a full verify and release
validation. When a change routes to a product surface, Governance selects the appropriate cost tier for the
change and the active profile, so local authoring stays cheap (a small docs edit need not trigger a full
build-and-test) while the protected boundary still demands the deeper tiers where the stakes require it. A
broad or expensive route is explained — the maintainer can see which tier was selected and why, and a
cheaper local alternative when one is known.

**Why this priority**: This is what keeps the expanded catalog usable day-to-day — without cost tiers, every
product-surface change would invite the most expensive gate, defeating the light-by-default stance. It
builds directly on Story 1's routing and is independently testable once the surfaces route.

**Independent Test**: Declare generated-product checks across the five cost tiers against a fixture; route a
cheap-tier change (e.g. a docs edit) and confirm only the cheap tier is selected; route a high-stakes change
(e.g. a release-surface change under a release profile) and confirm the deeper tier is selected; confirm the
route trace names the selected tier and reason, and that no cheap-tier change silently pulls in an expensive
gate without a positive match.

**Acceptance Scenarios**:

1. **Given** generated-product checks declared at distinct cost tiers, **When** a low-stakes change routes
   to a product surface, **Then** Governance selects the cheapest applicable tier and does not require a
   more expensive tier.
2. **Given** a change to a release surface under a release-oriented profile, **When** Governance routes it,
   **Then** the deeper validation tier is selected and the route explains why.
3. **Given** any product-surface route, **When** the route is explained, **Then** the explanation names the
   selected cost tier, the matched capability and surface, and (when known) a cheaper local alternative.
4. **Given** a change that matches more than one declared surface, **When** Governance selects the cost
   tier, **Then** the selection is deterministic (a documented precedence), not order-dependent.

---

### User Story 3 - Run Governance locally without monorepo access (Priority: P2)

A generated product is checked out **on its own** — without the monorepo it was generated alongside. Its
maintainer runs Governance against the product's own `.fsgg/capabilities.yml` and its declared surfaces, and
gets a routed, classified, cost-tiered result that depends only on what the product itself declares. The
product has **no build- or run-time dependency** on the monorepo or on Governance internals: removing the
tool entirely changes nothing about the product, and the product needs no shared monorepo files to be
governed.

**Why this priority**: This is the adoption story — it is what lets a generated product consume Governance
from the outside, which is the whole point of declaring product capabilities. It depends on Story 1's
catalog being self-contained and is independently testable.

**Independent Test**: Take a generated-product fixture checked out in isolation (no monorepo paths present),
run Governance against its catalog, and confirm routing and classification succeed using only the product's
own declared sources; confirm that deleting the tool leaves the product unchanged and buildable, and that
the product references no monorepo-only path to be governed.

**Acceptance Scenarios**:

1. **Given** a generated product checked out without monorepo access, **When** Governance runs against its
   catalog, **Then** routing and classification complete using only the product's own declared sources.
2. **Given** the same product, **When** the Governance tool is removed entirely, **Then** nothing about the
   product changes — it builds and runs identically and carries no residual dependency on the tool.
3. **Given** a catalog that references a path resolvable only inside the monorepo, **When** Governance runs
   the product standalone, **Then** it reports the unresolved reference as a clear input diagnostic rather
   than silently succeeding.

---

### User Story 4 - Versioned schema with a safe migration from the MVP catalog (Priority: P3)

A repository already on the MVP capability schema upgrades to the expanded catalog. The expanded schema
carries a new **version**, and there is documented **migration** guidance from the MVP shape to the new one.
Governance validates the expanded catalog strictly — it keeps deterministic ordering, and it rejects a
duplicate surface/check/capability id, an unknown field, and an unsupported schema version with a clear,
specific diagnostic — and these guarantees extend to every new field the expansion adds. A repository that
has not yet migrated is told exactly what changed and how to move forward, rather than silently mis-parsing.

**Why this priority**: Schema evolution must be safe, but it depends on the expanded vocabulary (Stories
1–2) already existing. It is the hardening that lets existing governed repositories adopt the expansion
without breakage.

**Independent Test**: Load an MVP-version catalog and confirm it is handled per the documented migration
(accepted under compatibility rules or rejected with migration guidance, per the decision); load an
expanded catalog with a duplicate id, with an unknown field, and with an unsupported version, and confirm
each fails with a specific diagnostic; confirm two loads of the same valid expanded catalog order their
contents identically.

**Acceptance Scenarios**:

1. **Given** a catalog declaring the new product surfaces, **When** it is validated, **Then** deterministic
   ordering, duplicate-id rejection, and unknown-field rejection apply to the new fields exactly as they do
   to the MVP fields.
2. **Given** a catalog with an unsupported schema version, **When** Governance loads it, **Then** it is
   rejected with a diagnostic that names the version and points at the migration guidance.
3. **Given** an MVP-version catalog, **When** Governance loads it under the expanded schema, **Then** it is
   handled per the documented migration path (not silently mis-parsed), and the migration guidance is
   discoverable.

---

### Edge Cases

- **New protected surface hidden under a governed root**: a new package/API, readiness, or release surface
  is placed under an already-governed root with no capability declaration ⇒ an unknown-governed-path finding,
  **never** a silent pass and never a routine classification (the capability design's central safety
  property).
- **Subset declaration**: a catalog declares only some of the new surface kinds ⇒ only the declared kinds
  are governed; the absent kinds are not an error and do not force a default classification.
- **Duplicate id across the new surface kinds**: two declared surfaces/checks/capabilities share an id ⇒ a
  duplicate-id error with both locations named.
- **Unknown field in the expanded catalog**: a field not in the expanded schema ⇒ an unknown-field
  diagnostic naming the field (strict validation, as today).
- **Unsupported / unknown schema version**: a version the tool does not support ⇒ rejected with a diagnostic
  naming the version and the migration guidance; never best-effort mis-parsed.
- **Catalog referencing a monorepo-only path while run standalone**: a declared source resolvable only inside
  the monorepo, run without monorepo access ⇒ a clear input diagnostic for the unresolved reference, not a
  fabricated success.
- **Change matching multiple declared surfaces**: a path under two declared surfaces ⇒ deterministic
  precedence selects the classification and cost tier (documented, order-independent).
- **Cheap change under a heavy profile**: a low-stakes change under a strict/release profile ⇒ the cost-tier
  selection still respects the change's stakes and the route explains the selected tier; no expensive gate is
  pulled in without a positive match.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The capability catalog (`.fsgg/capabilities.yml`) MUST let a generated product **declare** the
  full set of product surfaces it owns: package/API surfaces, generated roots, template profiles, package
  pins, product tests, generated guidance, sample apps, docs/examples, skills, design artifacts, release
  surfaces, baselines, and evidence tags.
- **FR-002**: For each declared product surface, Governance MUST **route** a changed path under it to the
  matching capability and **classify** the surface (package / docs / skills / design / release /
  generated-product / generated-view), so heavy checks can be required against a positive match.
- **FR-003**: Governance MUST emit an **unknown-governed-path** finding for a new protected surface
  (package/API, readiness, or release) placed under a governed root **without** capability metadata — it MUST
  NOT classify such a surface as routine and MUST NOT silently pass it.
- **FR-004**: Governance MUST preserve the **light-by-default** contract: a routine/unmanaged path that
  matches no declared surface or fence stays cheap and does not trigger a heavy product route.
- **FR-005**: The catalog MUST let a repository declare generated-product checks at **cost tiers** spanning at
  least: structural scan, restore/build, focused tests, full verify, and release validation.
- **FR-006**: Given a change and an active profile, Governance MUST **select the appropriate cost tier** for
  each routed product surface — keeping local authoring cheap while demanding deeper tiers where the surface's
  stakes and the profile require — and MUST NOT pull in a more expensive tier without a positive match.
- **FR-007**: When a product-surface route is explained, the explanation MUST name the **matched capability,
  the surface classification, the selected cost tier**, and (when known) a cheaper local alternative.
- **FR-008**: When a change matches more than one declared surface, the surface classification and cost-tier
  selection MUST be **deterministic** (a documented precedence), not dependent on declaration or evaluation
  order.
- **FR-009**: A generated product MUST be governable **standalone — without access to the monorepo** it was
  generated alongside: routing and classification MUST depend only on the product's own declared sources, and
  the product MUST carry no build- or run-time dependency on the Governance tool (removing the tool changes
  nothing for the product).
- **FR-010**: When a standalone run encounters a declared source resolvable only inside the monorepo,
  Governance MUST report a clear **input diagnostic** for the unresolved reference rather than fabricating
  success.
- **FR-011**: The expanded catalog MUST carry a **versioned schema identifier**, and Governance MUST reject an
  **unsupported/unknown** schema version with a diagnostic that names the version and points at the migration
  guidance.
- **FR-012**: Strict validation MUST extend to every new field: **deterministic ordering**, **duplicate-id
  rejection** (across the new surface/check/capability ids), and **unknown-field rejection** MUST apply to the
  expansion exactly as they do to the MVP catalog.
- **FR-013**: The feature MUST ship documented **migration guidance** from the MVP catalog schema to the
  expanded schema, and an MVP-version catalog MUST be handled per that guidance (accepted under documented
  compatibility rules, or rejected with the guidance) — never silently mis-parsed.
- **FR-014**: Product vocabulary MUST stay in the **capability catalog and product-facing adapters**, never in
  the Governance kernel — the same product-neutrality discipline the existing rows hold: no product identity,
  surface identity, path, template profile, or generator is hardcoded in core.
- **FR-015**: Diagnostics MUST distinguish a missing/malformed **input** (absent/invalid catalog, unresolved
  declared source, unsupported version) from a **tool defect**, and MUST name the offending surface/field so a
  maintainer can fix it; no swallowed errors and no fabricated classification.
- **FR-016**: This feature MUST make product surfaces **declarable, routable, classifiable, and cost-tiered**;
  it MUST NOT be presented as implementing the per-domain deterministic **checks themselves** (package
  baseline drift, FSI transcripts, docs links, skill path contracts, design tokens) — those are the next
  feature (F24) and a declared evidence tag without its check is a known, non-error state this row produces.

### Key Entities *(include if data involved)*

- **Capability catalog (expanded)**: the versioned `.fsgg/capabilities.yml` declaring a generated product's
  domains, path map, surfaces, checks, and now the full product-surface vocabulary and evidence tags.
- **Product surface**: a declared region a generated product owns and protects — one of package/API,
  generated root, sample app, docs/examples, skill, design artifact, or release surface — with an id, a
  classification, the paths it covers, an owner, and a maturity.
- **Template profile**: the declared template a generated root was instantiated from, recorded so the
  generated root can be governed against its profile.
- **Package pin / baseline**: the declared pin or baseline that fixes a package/API surface so future drift
  can be detected (the drift *check* is F24).
- **Cost tier**: the declared depth of a generated-product check — structural scan, restore/build, focused
  tests, full verify, or release validation — used to select how much work a routed change warrants.
- **Evidence tag**: the declared label tying a surface to the evidence that would prove it current (the
  evidence's *production* is F24).
- **Route/classification result**: per changed path — the matched capability, the surface classification, the
  selected cost tier, the explanation, and any unknown-governed-path finding.
- **Schema version & migration**: the catalog's version identifier and the documented path from the MVP
  schema to the expanded schema.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A generated product can declare a surface of **every** new kind (package/API, generated root +
  template profile, sample app, docs/examples, skill, design artifact, release surface, baseline) and a change
  under each routes to the expected capability and surface classification — verified by a fixture covering
  every kind.
- **SC-002**: A new protected surface (package/API, readiness, or release) placed under a governed root with
  no capability metadata is flagged as an unknown governed path **100% of the time** — it is never classified
  routine and never silently passes.
- **SC-003**: Cost-tier selection keeps light authoring cheap: a low-stakes change selects the cheapest
  applicable tier and **never** pulls in a more expensive generated-product gate without a positive match,
  while a high-stakes change under the matching profile selects the deeper tier — both with an explanation
  naming the selected tier.
- **SC-004**: A generated product runs Governance **standalone** (no monorepo access) using only its own
  declared sources, and removing the tool leaves the product byte-for-byte unchanged and buildable (zero
  residual dependency).
- **SC-005**: Strict validation holds across the expansion: a duplicate id, an unknown field, and an
  unsupported schema version each fail with a specific diagnostic, and the same valid catalog orders its
  contents identically on repeated loads.
- **SC-006**: An MVP-version catalog is handled per documented migration guidance (never silently
  mis-parsed), and the migration guidance is discoverable from the diagnostic.
- **SC-007**: No product identity, surface identity, path, template profile, or generator is hardcoded in the
  Governance kernel — the product vocabulary lives only in the catalog and the product-facing adapters
  (verifiable by inspection).

## Assumptions

- **Next-item resolution**: "next item in plan" is roadmap **F23 · `023-generated-product-capabilities`**,
  the first row of Phase 10 (Capability Catalog And Product Adapter Expansion). The requester selected F23
  over F24 (`024-package-docs-skills-design-checks`) at specification time, confirming the
  catalog/schema-expansion scope rather than the concrete-check-implementation scope.
- **F23 vs F24 boundary**: this feature *declares, routes, classifies, and cost-tiers* the product surfaces;
  the per-domain deterministic **checks themselves** (package `.fsi`-baseline drift, FSI transcripts, docs
  links, skill path contracts, design tokens/contrast) are F24 and out of scope here. A declared evidence tag
  whose check does not yet exist is an expected, non-error state this row produces (FR-016).
- **Schema evolution is in scope (and bounded to `capabilities.yml`)**: unlike the recent host-command rows
  that reuse the frozen `.fsgg` schema verbatim, this row **expands** `.fsgg/capabilities.yml` with a new
  schema version and documented migration. `.fsgg/project.yml`, `.fsgg/policy.yml`, and `.fsgg/tooling.yml`
  are untouched.
- **MVP migration policy**: the precise compatibility rule (whether an MVP-version catalog is accepted under a
  compatibility shim or requires an explicit bump with migration guidance) is a planning decision deferred to
  `/speckit-plan`; either way an MVP-version catalog is handled deterministically and is never silently
  mis-parsed (FR-013).
- **Cost-tier vocabulary**: the five tiers named by the roadmap (structural scan, restore/build, focused
  tests, full verify, release validation) are the declarable depths; their exact tokens and the selection
  precedence are planning decisions, but the cheap-by-default and explained-route properties are fixed here
  (FR-006, FR-007).
- **Standalone governance**: a generated product is governable from outside the monorepo using only its own
  declared catalog and sources; the product takes no dependency on the Governance tool (the org "external
  customer from outside / advisory by default" posture). The precise standalone-run entry point reuses the
  existing host/CLI edge and is a planning decision.
- **Product-neutrality preserved**: all product vocabulary lives in the catalog and product-facing adapters,
  never in the Governance kernel — the same discipline F014–F057 hold; this row adds no hardcoded product
  identity or path to core.
- **Routing reuse, not a new router**: this row extends the existing capability routing and surface
  classification (F016) and the F014 catalog parser/validator to the new surface kinds; it composes the
  existing machinery rather than introducing a parallel routing mechanism. The precise project layout and the
  reuse of the existing parse/route modules are planning decisions deferred to `/speckit-plan`.
- **Enforcement unchanged**: this row makes product surfaces *route and classify* and selects their cost
  tiers; how a classified finding maps to a blocking verdict under each profile/mode is the existing
  enforcement machinery (F018/F023), reused — this row does not re-open the enforcement truth table.
