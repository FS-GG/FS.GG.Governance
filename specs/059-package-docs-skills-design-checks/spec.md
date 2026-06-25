# Feature Specification: Package / Docs / Skills / Design Deterministic Checks

**Feature Branch**: `059-package-docs-skills-design-checks`

**Created**: 2026-06-25

**Status**: Planned

**Input**: User description: "next item in plan" — roadmap **F24 ·
`024-package-docs-skills-design-checks`** (`docs/2026-06-18-governance-kernel-speckit-implementation-plan.md`),
the second row of **M8 — Generated-product and surface-domain checks**, and the row the just-merged **F23
(`058-generated-product-capabilities`)** explicitly defers to: F23 made the product surfaces *declarable,
routable, classifiable, and cost-tiered* and produced, as a known non-error state, **declared evidence tags
with no check behind them** (F23 FR-016). This feature supplies those missing checks. It adds the **concrete,
deterministic adapter rule packs** that actually *evaluate* the major generated-product surface domains —
package/API, docs/examples, skills, and design/rendering — so a declared evidence tag is no longer an empty
promise but an executable check that produces real evidence. Per the roadmap: "major capability domains have
concrete deterministic checks before agent-reviewed judgement checks can influence gates."

Two scope decisions are confirmed for this feature (the requester advanced from F23 to the next row, F24,
after F23 merged — confirming the concrete-check-implementation scope over further catalog expansion):

1. **This feature implements the per-domain *deterministic checks themselves*; it does not re-open the
   catalog vocabulary or routing.** F23 already lets a generated product *declare* package surfaces and
   their baselines, generated roots and template profiles, docs/examples, skills, design artifacts, and
   release surfaces, *route* changes to them, *classify* the surface, and *select a cost tier*. This row
   consumes that already-routed, already-classified surface and adds the rule pack that evaluates it:
   `.fsi`-baseline drift, FSI transcript currency, docs link/reference currency, skill path-contract
   conformance, and design token/capture/contrast/control facts. It does **not** add new surface kinds, a new
   path map, a new cost-tier vocabulary, or a new schema version — those are F23 and frozen here.

2. **The deterministic checks are pure adapters plus host sensors; judgement-heavy agent-reviewed checks stay
   advisory.** Each check is a pure, total fact-producing adapter (the F014/F015/F017/F031 leaf precedent) fed
   by a host sensor that reads the real source (the `.fsi` file, the FSI transcript, the docs link target, the
   skill manifest, the design token/contrast catalog). Rendering and tooling dependencies stay **out of the
   Governance kernel** — they live in the host sensors and product-facing adapters, the same product-neutrality
   discipline F014–F058 hold. Checks whose verdict requires human/agent judgement (prose quality, design
   intent) are produced as **advisory** findings that inform but never block a gate, until a later row promotes
   them.

## Overview

After F23, a generated product can declare the surfaces it owns and Governance will route a change to the
right surface, classify it (package / docs / skills / design / release / generated-product), and select a cost
tier. But the surface's **evidence tag points at nothing**: F23 deliberately stops at *"this surface exists,
here is its capability, its cost tier, and the evidence tag that would prove it current"* and leaves the rule
pack that *produces* that evidence to this feature. So today a package surface can drift from its published
`.fsi` baseline, a public example in an FSI transcript can stop compiling, a docs link can rot, a skill can
violate its declared path contract, or a design token can fall out of the catalog — and Governance, lacking
the check, cannot tell. The surface is *known* but *unchecked*.

This feature closes that gap for the four deterministic surface domains the roadmap names, in priority order:

- **Package / API.** Generate and compare a committed **`.fsi` surface baseline** so an unintended public-API
  change is caught as **baseline drift**, and run **FSI transcript checks** that prove the public examples and
  package contracts a product publishes still compile and evaluate to their stated results. This is the
  highest-stakes domain — a silent public-API break is the worst failure a generated product can ship — so it
  is the P1 slice.
- **Docs / examples.** Check **FsDocs / literate scripts / public-API docs** for **link currency**,
  **reference currency**, and example freshness, so a product's documentation cannot silently rot or point at
  a moved/renamed symbol or URL.
- **Skills.** Check **product skills, task skill lists, path contracts, and optional mirrors** so a declared
  skill actually resolves the paths it claims, its task list is consistent, and any required mirror is present
  and in sync.
- **Design / rendering.** Connect **design-system facts** — **token**, **capture**, **contrast**, and
  **control** catalog sources — to their real catalogs so design surfaces can be checked deterministically,
  **while keeping rendering dependencies out of the kernel** (the host sensor reads the catalog; the kernel
  sees only facts).

Each domain is delivered as an independent, composable **adapter rule pack**: it consumes the F23-routed,
F23-classified surface and the surface's declared evidence tag, runs its deterministic check, and emits a
finding (and the produced evidence) under the existing findings/evidence machinery. The rule packs **compose**
— a single change that touches a package surface, its docs, and its skill runs all three applicable checks —
without any one depending on another. Judgement-heavy checks that cannot be made deterministic are emitted as
**advisory** findings only.

This feature does **not** change how a finding maps to a blocking verdict (the F018/F023 enforcement truth
table, reused), does **not** add or modify catalog vocabulary, routing, cost tiers, or schema version (F23,
frozen), and does **not** add network/registry dependencies to the kernel. It supplies the missing checks —
and only the deterministic ones — so the surfaces F23 made *known* finally become *checked*.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Package/API surface checks: `.fsi` baseline drift and FSI transcript currency (Priority: P1)

A maintainer of a generated product has declared a package/API surface (F23) with a baseline and an evidence
tag. They run Governance and, for the first time, that evidence tag is **backed by a real check**: Governance
generates the surface's `.fsi` baseline, compares it to the committed baseline, and reports **baseline drift**
when the public surface changed; and it runs the product's published **FSI transcripts** — the public examples
and package contracts — and reports any that no longer compile or no longer evaluate to their stated result.
An unintended public-API break, or a public example that has gone stale, is caught deterministically instead
of shipping silently.

**Why this priority**: A silent public-API break is the highest-stakes failure a generated product can ship,
and the package surface is the one F23 protects most carefully (baselines/pins). It is also the foundation the
other domains build on — the `.fsi` baseline machinery and the transcript-execution sensor are the most
reusable pieces. It delivers end-to-end value on its own: a product can detect API drift and broken examples
with no other domain implemented.

**Independent Test**: Point Governance at a package-surface fixture with a committed `.fsi` baseline; change
the public surface and confirm baseline drift is reported (and naming the changed members); revert and confirm
no drift. Add an FSI transcript fixture whose example compiles and evaluates correctly and confirm it passes;
break the example (or change its stated result) and confirm a transcript finding is reported. Confirm the
produced evidence is recorded under the surface's evidence tag.

**Acceptance Scenarios**:

1. **Given** a package surface with a committed `.fsi` baseline, **When** the public surface changes, **Then**
   Governance reports a baseline-drift finding naming what changed, and records the regenerated baseline as
   evidence; **When** the public surface is unchanged, **Then** no drift is reported.
2. **Given** a package surface with no committed baseline yet, **When** Governance runs, **Then** it produces
   the baseline (first-run generation) and reports the absence as a clear, fixable state rather than a tool
   defect.
3. **Given** a published FSI transcript (a public example or package contract), **When** the example still
   compiles and evaluates to its stated result, **Then** the transcript check passes and records evidence;
   **When** it no longer compiles or its result changed, **Then** a transcript finding is reported naming the
   failing example.
4. **Given** a change that routes to a package surface, **When** the check runs, **Then** the produced evidence
   is tied to that surface's declared evidence tag (closing the F23 "declared tag, no check" gap).

---

### User Story 2 - Docs/examples checks: link and reference currency (Priority: P2)

The same maintainer has declared a docs/examples surface (F23). Governance now checks the product's
documentation deterministically: **FsDocs / literate scripts / public-API docs** are scanned for **link
currency** (every link target resolves) and **reference currency** (every referenced symbol/anchor still
exists), and an example embedded in the docs is checked for freshness. A rotted link or a reference to a
renamed symbol is reported with the exact location, so documentation cannot silently drift out of sync with
the product it describes.

**Why this priority**: Docs rot is common and erodes trust in a generated product, but it is lower-stakes than
a public-API break and depends on the package-surface evidence machinery (Story 1) being in place. It is
independently testable once docs surfaces route.

**Independent Test**: Provide a docs fixture with a valid internal link, a valid external-style reference, and
a valid symbol reference, and confirm all pass; then break each in turn (a dangling internal link, a reference
to a removed symbol/anchor) and confirm each is reported with its exact location; confirm a docs change with
no broken links/references passes cleanly.

**Acceptance Scenarios**:

1. **Given** a docs/examples surface, **When** every link target and referenced symbol/anchor resolves,
   **Then** the docs currency check passes and records evidence.
2. **Given** a docs link whose target no longer resolves, **When** the check runs, **Then** a link-currency
   finding is reported naming the file, the link, and the unresolved target.
3. **Given** a docs reference to a symbol or anchor that has been removed or renamed, **When** the check runs,
   **Then** a reference-currency finding is reported naming the stale reference.
4. **Given** a docs example that no longer matches the current product surface, **When** the check runs,
   **Then** its staleness is reported (or, when the example is judgement-heavy, an advisory finding is emitted
   — see Story 5).

---

### User Story 3 - Skill checks: path contracts, task skill lists, and mirrors (Priority: P2)

The maintainer has declared one or more skills (F23). Governance checks each declared skill deterministically:
its **path contract** holds (every path the skill claims to own/touch resolves and stays within its declared
bounds), its **task skill list** is internally consistent, and any **optional mirror** the skill declares is
present and in sync. A skill that points at a missing path, declares an inconsistent task list, or has a
drifted mirror is reported, so a product's skills cannot silently break their own contracts.

**Why this priority**: Skill correctness matters for products that ship skills, but fewer products ship skills
than ship packages or docs, so it is P2 alongside docs. It reuses the same routed-surface + evidence machinery
and is independently testable.

**Independent Test**: Provide a skill fixture whose path contract holds, whose task skill list is consistent,
and whose mirror is in sync, and confirm it passes; then introduce a path the skill claims but that does not
resolve, an inconsistent task list, and a drifted mirror, and confirm each is reported with the offending
skill and detail named.

**Acceptance Scenarios**:

1. **Given** a declared skill whose path contract holds, task list is consistent, and mirror (if any) is in
   sync, **When** the skill check runs, **Then** it passes and records evidence.
2. **Given** a skill that claims a path which does not resolve or escapes its declared bounds, **When** the
   check runs, **Then** a path-contract finding is reported naming the skill and the offending path.
3. **Given** a skill whose declared mirror is missing or out of sync, **When** the check runs, **Then** a
   mirror finding is reported; **Given** a skill that declares no mirror, **Then** the absent mirror is not an
   error.

---

### User Story 4 - Design/rendering facts: token, capture, contrast, and control (Priority: P3)

The maintainer has declared a design artifact surface (F23). Governance connects the product's **design-system
facts** — **token**, **capture**, **contrast**, and **control** — to their real catalog sources and checks
them deterministically: a token referenced by the product exists in the token catalog, a declared capture is
present, a contrast pair meets its declared threshold, and a control maps to its catalog entry. Crucially, the
**rendering dependency stays out of the Governance kernel**: the host sensor reads the design catalog and
produces plain facts; the kernel and the pure adapter never take a rendering/UI dependency.

**Why this priority**: Design checks matter for products that ship a design system, but they are the most
specialized domain and the one most at risk of dragging rendering dependencies into the kernel — so it is P3
and explicitly fenced. It is independently testable against catalog fixtures with no real rendering.

**Independent Test**: Provide design-catalog fixtures (token, capture, contrast, control) and confirm a
product referencing valid entries passes; then reference a missing token, an absent capture, a contrast pair
below threshold, and an unmapped control, and confirm each is reported; confirm by inspection that the kernel
and pure adapter carry **no** rendering/UI dependency — the catalog is read only by the host sensor.

**Acceptance Scenarios**:

1. **Given** a design surface referencing tokens/captures/contrast/control that all resolve in their catalogs,
   **When** the design check runs, **Then** it passes and records evidence.
2. **Given** a referenced token, capture, or control absent from its catalog, or a contrast pair below its
   declared threshold, **When** the check runs, **Then** a design finding is reported naming the missing or
   failing entry.
3. **Given** the design check implementation, **When** the kernel and pure adapter are inspected, **Then**
   they carry no rendering/UI/registry dependency — the design catalog is read solely by the host sensor
   (product-neutrality preserved).

---

### User Story 5 - Judgement-heavy checks stay advisory (Priority: P3)

Some surface checks cannot be made fully deterministic — prose quality of docs, design intent, the
"reasonableness" of an example beyond compile-and-evaluate. Governance emits these as **advisory** findings:
they appear in the result, are clearly labelled advisory, and **inform but never block** a gate. Until a later
row promotes them, an advisory finding never changes a pass/fail verdict, so introducing them is safe.

**Why this priority**: Keeping agent-reviewed judgement checks advisory is the safety rule that lets the
deterministic checks land first without judgement ambiguity leaking into blocking verdicts. It is a
cross-cutting guarantee rather than a standalone surface, hence P3, but it is independently testable.

**Independent Test**: Produce an advisory finding from a judgement-heavy check on a fixture; confirm it appears
in the result labelled advisory; confirm that under every profile/mode the advisory finding does **not** change
the blocking verdict (a run with only advisory findings still passes the gate).

**Acceptance Scenarios**:

1. **Given** a judgement-heavy check, **When** it produces a finding, **Then** the finding is labelled advisory
   and is distinguishable from a deterministic finding.
2. **Given** a run whose only findings are advisory, **When** the gate verdict is computed under any profile or
   mode, **Then** the verdict is not blocked by the advisory findings.
3. **Given** a mix of advisory and deterministic findings, **When** the verdict is computed, **Then** only the
   deterministic findings can affect the blocking verdict; the advisory ones inform only.

---

### Edge Cases

- **Declared evidence tag now backed by a check**: a surface F23 declared with an evidence tag but no check ⇒
  the matching rule pack now runs and produces real evidence; a surface whose domain still has no check in this
  row remains the F23 known, non-error "declared tag, no check" state (no regression).
- **First-run baseline absent**: a package surface with no committed `.fsi` baseline ⇒ the baseline is
  generated and its absence is a clear, fixable input state, never a tool defect and never a silent pass.
- **Check input unreadable / source missing**: a transcript that cannot be located, a docs file that cannot be
  read, a design catalog that is absent ⇒ a clear input diagnostic naming the source, distinct from a tool
  defect, never a fabricated pass.
- **Subset of domains declared**: a product declares only some surface domains (package but no design) ⇒ only
  the declared/routed domains run their checks; absent domains are not an error.
- **Composition**: a single change touching a package surface, its docs, and its skill ⇒ all three applicable
  rule packs run and each emits its own finding/evidence, deterministically and order-independently; no rule
  pack depends on another.
- **Non-determinism risk**: a check that could vary by environment, ordering, timestamp, or path ⇒ the check
  is made deterministic (stable ordering, normalized paths, no wall-clock) or, if it cannot be, it is emitted
  advisory rather than as a deterministic blocking finding.
- **Rendering dependency leak**: any attempt to read a design surface inside the kernel/pure adapter ⇒ a
  product-neutrality violation; the rendering/catalog read must live in the host sensor only.
- **Judgement check mislabelled deterministic**: a finding whose verdict actually requires human/agent
  judgement ⇒ it MUST be advisory, never a deterministic blocking finding.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: For a **package/API** surface routed and classified by F23, Governance MUST generate the
  surface's **`.fsi` baseline**, compare it to the committed baseline, and report **baseline drift** when the
  public surface has changed, naming what changed; when unchanged, it MUST report no drift.
- **FR-002**: When a package surface has **no committed baseline**, Governance MUST generate it (first-run) and
  report the absence as a clear, fixable input state — never a tool defect and never a silent pass.
- **FR-003**: Governance MUST run a generated product's published **FSI transcript** checks (public examples
  and package contracts) and report any transcript that no longer **compiles** or no longer **evaluates to its
  stated result**, naming the failing example.
- **FR-004**: For a **docs/examples** surface, Governance MUST check **link currency** (every link target
  resolves) and **reference currency** (every referenced symbol/anchor still exists) across FsDocs / literate
  scripts / public-API docs, reporting each broken link/reference with its exact location.
- **FR-005**: For a **skill** surface, Governance MUST check the skill's **path contract** (every claimed path
  resolves and stays within declared bounds), its **task skill list** consistency, and any declared **mirror**
  (present and in sync), reporting each violation with the offending skill named; a skill declaring no mirror
  MUST NOT be an error.
- **FR-006**: For a **design** surface, Governance MUST connect **token**, **capture**, **contrast**, and
  **control** facts to their real catalog sources and report a missing/failing entry (absent token/capture/
  control, contrast below threshold), naming it.
- **FR-007**: The design check (and every check) MUST keep **rendering / UI / registry dependencies out of the
  Governance kernel and the pure adapters** — the real source is read only by a host sensor that produces plain
  facts; no product identity, path, or rendering dependency is hardcoded in core (product-neutrality, as
  F014–F058).
- **FR-008**: Each domain check MUST be an **independent, composable adapter rule pack**: a single change
  touching multiple declared surfaces MUST run every applicable rule pack, each emitting its own finding/
  evidence, with **no rule pack depending on another**.
- **FR-009**: Each check MUST produce its result tied to the surface's **declared evidence tag** (F23), so a
  declared tag is backed by real produced evidence — closing the F23 "declared tag, no check" gap for the
  domains this row covers.
- **FR-010**: Every deterministic check MUST be **deterministic** — stable ordering, normalized paths, no
  wall-clock/username/environment dependence — so identical input yields byte-identical findings and evidence.
- **FR-011**: A check whose verdict requires human/agent **judgement** MUST be emitted as an **advisory**
  finding that is clearly labelled and **informs but never blocks** a gate verdict under any profile or mode,
  until a later row promotes it.
- **FR-012**: Checks MUST distinguish a missing/malformed **input** (absent baseline, unlocatable transcript,
  unreadable docs source, absent design catalog) from a **tool defect**, naming the offending source, with no
  swallowed errors and no fabricated pass (safe-failure, as F014–F058).
- **FR-013**: This feature MUST **reuse** F23's catalog vocabulary, path map, surface classification, cost
  tiers, and schema version unchanged — it MUST NOT add new surface kinds, a new path map, a new cost-tier
  vocabulary, or a new schema version.
- **FR-014**: This feature MUST **reuse** the existing enforcement machinery (F018/F023) for how a
  deterministic finding maps to a blocking verdict — it MUST NOT re-open the enforcement truth table; it only
  supplies new findings/evidence.
- **FR-015**: A surface domain whose check this row does **not** implement MUST remain the F23 known, non-error
  "declared evidence tag, no check yet" state — no regression and no silent pass introduced for uncovered
  domains.
- **FR-016**: A generated product MUST be able to run these checks **standalone** — without monorepo access —
  using only its own declared sources and the local host sensors, consistent with F23's standalone-governance
  guarantee; no check may require a monorepo-only path to succeed.

### Key Entities *(include if data involved)*

- **Adapter rule pack**: a pure, total, deterministic check for one surface domain (package / docs / skill /
  design) that consumes the F23-routed/classified surface and its evidence tag and emits a finding plus
  produced evidence; composable and independent of the other packs.
- **Host sensor**: the edge component that reads the real source for a check (the `.fsi` file, the FSI
  transcript, the docs link target, the skill manifest, the design catalog) and produces plain facts — the
  only place a rendering/registry/filesystem dependency lives.
- **`.fsi` surface baseline**: the committed snapshot of a package/API surface against which drift is detected;
  generated on first run when absent.
- **FSI transcript fact**: whether a published public example / package contract still compiles and evaluates
  to its stated result.
- **Docs currency fact**: link currency and reference currency for a docs/examples surface — whether every
  link target and referenced symbol/anchor resolves.
- **Skill contract fact**: whether a skill's path contract holds, its task skill list is consistent, and its
  declared mirror (if any) is present and in sync.
- **Design fact**: token / capture / contrast / control resolution against the real design catalog.
- **Advisory finding**: a judgement-heavy finding that informs but never blocks a gate verdict.
- **Produced evidence**: the deterministic output a check records under a surface's declared evidence tag,
  making the F23 tag concrete.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A package surface's unintended public-API change is caught as **baseline drift 100% of the
  time** (and an unchanged surface reports zero drift), and a published FSI example that stops compiling or
  changes result is reported — verified by fixtures for both drift and transcript failure.
- **SC-002**: A docs/examples surface with a dangling link or a stale symbol/anchor reference is reported with
  its exact location, and a clean docs surface passes with zero false positives — verified by link- and
  reference-currency fixtures.
- **SC-003**: A skill whose path contract is violated, whose task list is inconsistent, or whose mirror has
  drifted is reported naming the offending skill, and a conformant skill (including one with no mirror) passes
  — verified by skill-contract fixtures.
- **SC-004**: A design surface referencing a missing token/capture/control or a sub-threshold contrast is
  reported, and the Governance **kernel and pure adapters carry zero rendering/UI/registry dependency**
  (verifiable by inspection) — the catalog is read only by the host sensor.
- **SC-005**: Every deterministic check is **deterministic**: the same input yields byte-identical findings
  and evidence on repeated runs (no timestamp/path/order variance) — verified by a determinism test per
  domain.
- **SC-006**: An advisory (judgement-heavy) finding **never** changes a blocking verdict: a run whose only
  findings are advisory passes the gate under every profile and mode — verified across the enforcement matrix.
- **SC-007**: Each check ties its produced evidence to the surface's declared evidence tag, so for every domain
  this row covers there are **zero** declared-tag-with-no-check states remaining (the F23 gap is closed for the
  covered domains); uncovered domains remain the known non-error state with no regression.
- **SC-008**: The rule packs **compose**: a single change touching a package surface, its docs, and its skill
  runs all three checks and emits three independent findings/evidence sets deterministically — verified by an
  adapter-composition test.

## Assumptions

- **Next-item resolution**: "next item in plan" is roadmap **F24 ·
  `024-package-docs-skills-design-checks`**, the second row of M8 and the feature F23
  (`058-generated-product-capabilities`) explicitly defers its checks to. F23 merged on 2026-06-25; this row is
  the next unimplemented roadmap item.
- **F23 ↔ F24 boundary**: F23 makes product surfaces declarable/routable/classifiable/cost-tiered and produces
  declared-but-unchecked evidence tags; this row supplies the deterministic checks that back those tags. This
  row reuses F23's catalog vocabulary, routing, classification, cost tiers, and schema version **unchanged**
  (FR-013) and adds only new checks/evidence.
- **Deterministic-first, advisory-fenced**: only the *deterministic* domain checks (package `.fsi`-baseline
  drift, FSI transcripts, docs link/reference currency, skill path contracts, design token/capture/contrast/
  control) are implemented as blocking-capable findings; judgement-heavy checks are emitted advisory and never
  block until a later row promotes them (FR-011). Which specific checks are deterministic vs advisory at the
  margins is a planning decision deferred to `/speckit-plan`.
- **Pure adapter + host sensor split**: each check is a pure, total fact-producing adapter (the
  F014/F015/F017/F031 leaf precedent) fed by a host sensor that performs the real source read; rendering /
  registry / filesystem dependencies live only in the sensor, never in the kernel or pure adapters (FR-007).
  The exact project layout and which existing modules are reused are planning decisions.
- **Evidence/enforcement reuse**: produced evidence is recorded under the existing evidence machinery and tied
  to F23 evidence tags; how a deterministic finding maps to a blocking verdict is the existing F018/F023
  enforcement truth table, reused unchanged (FR-014). This row does not re-open enforcement.
- **Standalone preserved**: these checks run against a generated product checked out standalone, using only its
  own declared sources and local host sensors, consistent with F23's standalone guarantee (FR-016). No check
  requires monorepo-only access or a network/registry call from the kernel.
- **Determinism is mandatory**: every deterministic check normalizes ordering and paths and avoids
  wall-clock/username/environment dependence so identical input yields byte-identical output (FR-010, SC-005),
  matching the byte-identical discipline F042–F058 hold.
- **Scope of "FsDocs / literate scripts / public-API docs"**: the docs currency check covers link currency and
  reference/symbol currency and example freshness; deeper FsDocs build orchestration (full site generation) is
  not in scope here — the check verifies currency of the declared docs sources, not a full docs build.
- **Design catalog sources are inputs, not implemented here**: the design check connects to *existing* token /
  capture / contrast / control catalog sources as inputs; producing or rendering those catalogs is out of
  scope and stays out of the kernel (FR-006, FR-007).
