# Feature Specification: Release-Facts Sensing for the Repository Boundary

**Feature Branch**: `054-release-facts-sensing`

**Created**: 2026-06-24

**Status**: Draft

**Input**: User description: "next item in the plan." — resolved against the just-merged release-gate thread.
The pure release-gate readiness core (F053) landed `evaluate`/`rollup`/`evaluateRelease`, but it deliberately
took the release facts it governs as **provided typed input** and **did not sense them** (F053 spec,
"Out of Scope": *"Sensing real release facts from a governed repository (version, package metadata, template
pins, publish plan, trusted-publishing configuration, provenance/attestations) — the **next** row."*). This
feature is that next row: it senses, from a real governed repository, the current state of each release rule
family and produces the **typed release-facts value the F053 core consumes** — the `ReleaseFacts` map of
per-family `FactState` (`Met` / `Unmet` / `Unrecoverable`) — plus a typed snapshot of the observed evidence
behind each fact. The `fsgg release` **host command** that wires sensing → F053 → exit code and the additive
`release.json` **projection** remain following rows, out of scope here — exactly the cadence the snapshot/route
thread followed (sense a real repository boundary → pure core consumes it → host wiring → projection).

## Context

F053 answered "given declared release rules and the facts they govern, which release expectations are met and
what is the verdict?" — but it took the facts as a caller-supplied `ReleaseFacts` value and sensed nothing.
The F053 research (D3) explicitly anticipated this row: *"the richer per-kind fact shapes — the actual version
string, the metadata field list, the resolved pin set — are what the **sensing** row will inspect to produce a
`FactState`."* Nothing has yet turned a real repository into those facts, so the F053 core cannot run against
an actual project; it can only run against hand-built fixtures.

This feature is that missing half. Given a governed repository and the caller's declared per-family
expectations (the criteria that define "met"), it recovers each family's governing evidence, derives exactly
one `FactState` per recognized family, and assembles the `ReleaseFacts` value the F053 core consumes — handing
it straight to `evaluate` with no adaptation. Alongside the bare tri-state it surfaces a typed **release
snapshot** of the observed evidence (the version it read and the baseline it compared against, which required
metadata fields were present and which missing, the resolved versus expected pins, the publishing posture, and
the provenance evidence) so a later finding or projection can name concrete specifics rather than only
"satisfied / violated."

Sensing a repository is **impure**: it reads local files and repository state. Following the same effects-
boundary discipline the git/CI snapshot sensing (F016) and the Host interpreter (F08) use, that impurity is
isolated behind a single injected port, so the fact derivation over recovered evidence is pure and the sensing
runs against a **real fixture repository** in tests and reaches **no live network, package registry, or
hosting/publishing-provider API**. The facts value it yields is, by contrast, **pure deterministic data**:
identical repository state and identical declared expectations yield a structurally identical facts value and
snapshot (every collection in a fixed order, so the two senses compare equal).
It fails safe — a missing or unreadable evidence source becomes `Unrecoverable` (⇒ a violated release finding
downstream), never a fabricated `Met`.

This feature stops at the sensed facts and snapshot. It does **not** evaluate rules into findings, derive
effective severity, roll up a release verdict (that is F053), run the `fsgg release` host command, or emit the
`release.json` document. Those are F053 and later rows that consume this sensing.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Sense the per-family release fact state from a governed repository (Priority: P1)

A release author (or the future `fsgg release` host) points the sensing at a governed repository and supplies
the declared per-family expectations that define "met." The sensing reads the repository's real release state
and returns a typed release-facts value — exactly one fact state (`Met` / `Unmet` / `Unrecoverable`) for each
of the six release families — that can be handed straight to the F053 release-gate core.

**Why this priority**: This is the irreducible core of the row. Without a real repository turned into the
typed facts the F053 core consumes, the release gate can only run against hand-built fixtures and never against
an actual project. A sensed facts value is the MVP and is independently valuable as the answer to "what is the
current release state of this repository?"

**Independent Test**: Build a fixture repository whose six families are in a known state (some satisfying their
expectation, some violating it), sense the facts against the matching declared expectations, and assert the
result is a facts value with exactly one fact state per family, each classification correct, and that the value
is accepted by the F053 `evaluate` with no further adaptation.

**Acceptance Scenarios**:

1. **Given** a fixture repository whose version, package metadata, template pins, publish plan, trusted-
   publishing configuration, and provenance evidence all satisfy the declared expectations, **When** the facts
   are sensed, **Then** every one of the six families resolves to `Met` and the result is exactly six fact
   states.
2. **Given** a fixture repository whose version is **not** bumped past the declared baseline (but whose other
   families are satisfied), **When** the facts are sensed, **Then** the version-bump family resolves to `Unmet`
   (distinct from `Unrecoverable`) and the other five resolve to `Met`.
3. **Given** the sensed facts value for any fixture, **When** it is passed to the F053 release-gate core,
   **Then** the core accepts it unchanged and produces one finding per declared rule — confirming the sensing
   output is exactly the F053 input shape.

---

### User Story 2 - Surface the observed evidence behind each fact (Priority: P2)

A maintainer reviewing a release decision needs more than "version bump: violated" — they need to see the
concrete evidence the gate read: the version it found and the baseline it compared against, which required
metadata fields were present and which were missing, the resolved pins versus the expected pins, the publishing
posture it observed, and the provenance evidence it found. The sensing surfaces a typed snapshot of that
observed evidence alongside each fact state.

**Why this priority**: The bare tri-state is enough for the F053 core to roll up a verdict, but a self-
explaining release report needs the specifics. Surfacing the observed evidence makes the later findings and
projection concrete and auditable. It layers on top of the P1 fact derivation and consumes the same recovered
evidence, so it is valuable but not the irreducible MVP.

**Independent Test**: Sense a fixture whose package metadata is missing two required fields and whose template
pins have drifted from the expected set; assert the snapshot reports the specific present/missing metadata
fields and the specific resolved-versus-expected pins, and that those specifics correspond to the `Unmet` fact
states for those two families.

**Acceptance Scenarios**:

1. **Given** a fixture whose package manifest is missing a required field, **When** the facts are sensed,
   **Then** the snapshot's package-metadata evidence names the present and the missing required fields, and the
   package-metadata family resolves to `Unmet`.
2. **Given** a fixture whose declared version equals the supplied baseline, **When** the facts are sensed,
   **Then** the snapshot's version evidence reports both the observed version and the baseline it was compared
   against, and the version-bump family resolves to `Unmet`.

---

### User Story 3 - Fail-safe, deterministic, network-free sensing (Priority: P3)

A maintainer auditing the gate must trust that the sensing hides nothing, invents nothing, never reaches the
outside world, and is reproducible: a family whose governing evidence is absent or unreadable resolves to
`Unrecoverable` (never a fabricated `Met`), every one of the six families always gets exactly one fact state,
two senses of identical repository state produce structurally identical output (the two `SensedRelease` values
compare equal, every collection in the same fixed order), and no run contacts a live network or
publishing/registry API.

**Why this priority**: These are the integrity guarantees that make the release gate safe to enforce, but they
are properties of the P1/P2 behavior rather than a separable feature.

**Independent Test**: Sense a fixture from which one family's governing evidence has been removed and another's
has been corrupted to be unparseable; assert both resolve to `Unrecoverable` (not `Met`, not a crash). Sense
the identical fixture twice and assert structurally identical facts and snapshot. Assert the run performs no
network access.

**Acceptance Scenarios**:

1. **Given** a fixture missing the publish-plan evidence entirely and whose provenance evidence is present but
   malformed, **When** the facts are sensed, **Then** both the publish-plan and provenance families resolve to
   `Unrecoverable` — never `Met`, never a thrown error — and the run still returns all six fact states.
2. **Given** the same fixture repository state sensed twice, **When** the two results are compared, **Then**
   they are structurally identical (the two `SensedRelease` values compare equal), including the ordering of
   every collection in the snapshot.
3. **Given** a fixture with no network available, **When** the facts are sensed, **Then** sensing completes
   successfully and makes zero calls to any package registry, publishing provider, or other network endpoint.

---

### Edge Cases

- **Repository missing every governing source.** All six families resolve to `Unrecoverable` and the sense is
  a successful all-unrecoverable result, not a sensing failure.
- **A governing source present but malformed / unparseable.** That family resolves to `Unrecoverable` (not
  `Unmet`, not a crash); `Unmet` is reserved for evidence that was recovered and genuinely did not meet the
  expectation.
- **A caller-supplied expectation absent for a family.** "Met" for that family cannot be decided, so it
  resolves to `Unrecoverable` (fail-safe), never an assumed `Met`.
- **Multiple candidate evidence sources for one family.** Resolution is deterministic — the family yields one
  fact state regardless of source ordering on disk. The sensing port exposes exactly one read function per
  family, so any multi-source resolution is encapsulated inside that single read and surfaces as one
  deterministic `RecoveredEvidence` value; the pure derivation never sees competing sources.
- **Extra repository artifacts unrelated to the six families.** Ignored; no fact for an unrecognized family is
  ever fabricated.
- **Evidence recovered but unsatisfied vs. evidence absent.** Kept distinct (`Unmet` vs. `Unrecoverable`) so a
  later finding reason can say "expected … was not met" versus "no recoverable evidence for …."

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST sense, from a governed repository, the current state of each of the six release
  rule families — version bump, package metadata, template pins, publish plan, trusted publishing, and
  provenance — and produce a typed release-facts value mapping each recognized family to exactly one fact state
  (`Met` / `Unmet` / `Unrecoverable`).
- **FR-002**: The produced release-facts value MUST be exactly the typed input shape the F053 release-gate core
  consumes, so the sensing output can be handed straight to the F053 evaluation with no adaptation or
  reshaping.
- **FR-003**: For each family, the system MUST classify the fact as `Met` only when the recovered evidence
  satisfies the declared expectation for that family, `Unmet` when evidence is recovered but the expectation is
  not satisfied, and `Unrecoverable` when the governing evidence is absent or cannot be read.
- **FR-004**: An absent, missing, or unreadable governing evidence source MUST yield `Unrecoverable` for that
  family — never a fabricated `Met`, never a swallowed error, never a thrown exception (fail-safe).
- **FR-005**: The system MUST surface a typed snapshot of the observed evidence per family — at minimum the
  observed version and the baseline it was compared against, the present and missing required metadata fields,
  the resolved versus expected pins, the observed publishing posture, the observed trusted-publishing
  configuration, and the observed provenance evidence — alongside each fact state, so later rows can name
  concrete specifics.
- **FR-006**: All impure repository reads MUST be confined to a single injected effects boundary (the same
  discipline as the F016 snapshot ports and the F08 Host interpreter), so the derivation of fact states over
  recovered evidence is pure and the sensing can run against a real fixture repository in tests.
- **FR-007**: Sensing MUST read only local repository state and MUST NOT contact any live network, package
  registry, or hosting/publishing-provider API.
- **FR-008**: Identical repository state and identical declared expectations MUST yield a structurally
  identical release-facts value and snapshot — every list and association-list collection emitted in a fixed
  order and the `Facts` map carrying canonical contents — so the two senses compare equal (determinism).
- **FR-009**: The system MUST produce a fact state for every one of the six recognized families on every run —
  never partial, never throwing — including when the repository is missing every governing source (all
  `Unrecoverable`).
- **FR-010**: The system MUST produce fact states only for the recognized release families and MUST NOT invent
  families or facts the repository does not evidence; extra unrelated artifacts are ignored.
- **FR-011**: The declared expectations that define "met" per family (the version baseline, the required
  metadata fields, the expected pins, the required publishing posture, and the required provenance) MUST be
  supplied as caller input, keeping the sensing free of any hardcoded product, package id, target name, or
  layout (the repository's one-way operating rule).
- **FR-012**: The system MUST NOT evaluate rules into findings, derive effective severity, roll up a release
  verdict, run the `fsgg release` host command, or emit a `release.json` document — it stops at the sensed
  facts value and snapshot consumed by F053 and the following rows.

### Key Entities *(include if feature involves data)*

- **Governed repository boundary** (input): the local working directory whose release state is sensed; the
  single source of all evidence read.
- **Declared release expectations** (input): the caller-supplied, product-neutral criteria that define "met"
  per family — the version baseline to compare against, the required metadata field set, the expected pin set,
  the required publishing posture, and the required provenance — never hardcoded.
- **Release evidence source** (per family): the local artifact(s) read to recover a family's state (a version
  declaration, a package manifest, a pin manifest, a publish plan, a publishing configuration, a provenance
  record).
- **Release snapshot**: the typed value of the observed evidence per family (the observed version + baseline,
  the present/missing metadata fields, the resolved/expected pins, the observed publishing posture, the
  observed provenance evidence) plus any sensing diagnostics — the auditable detail behind each fact state.
- **Release facts** (output): the typed map from release family to fact state — the exact value the F053
  release-gate core consumes.
- **Fact state**: the tri-state per-family outcome — `Met`, `Unmet`, or `Unrecoverable` — reused verbatim from
  the F053 vocabulary.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a fixture repository exercising all six families, the sensing produces exactly one fact state
  per family (six total) and the resulting facts value is accepted by the F053 release-gate core with no
  adaptation.
- **SC-002**: 100% of families whose governing evidence is absent or unreadable resolve to `Unrecoverable`
  (never `Met`), verified by an evidence-removed and an evidence-corrupted fixture.
- **SC-003**: Sensing the identical fixture state twice yields structurally identical release facts and
  snapshot — every list and association-list collection in the same fixed order — so the two `SensedRelease`
  values compare equal.
- **SC-004**: Sensing completes against a real local fixture repository with no network access — zero calls to
  any package registry, publishing provider, or other network endpoint.
- **SC-005**: For a fixture where every family's expectation is satisfied, every family resolves to `Met`; for
  a fixture where each family's recovered evidence violates its expectation, each resolves to `Unmet` (distinct
  from `Unrecoverable`), each with its observed evidence surfaced in the snapshot.
- **SC-006**: The set of families in the output equals the six recognized families on every run — no family
  dropped and no family fabricated, across satisfied, violated, and all-unrecoverable fixtures.

## Assumptions

- **This is the F053-named next row.** F053's "Out of Scope" names "sensing real release facts from a governed
  repository" as the next row; this spec is exactly that row. The `fsgg release` host command and the
  `release.json` projection remain following rows.
- **The output vocabulary is reused verbatim, not redefined.** The facts value, the `FactState` tri-state, and
  the six release families are the F053 `ReleaseFacts` / `FactState` / `ReleaseRuleKind` types reused as the
  sensing output — no new fact vocabulary is introduced.
- **"Met" is decided against caller-supplied declared expectations, not a live lookup.** Consistent with the
  repository's hard no-network sensing discipline (F016), version-bump "met" compares the repository's declared
  version against a caller-supplied baseline (for example, the last released version), and the metadata/pin/
  publishing/provenance expectations are likewise caller-supplied — the sensing never queries a registry or
  provider to decide "met."
- **Impurity is isolated behind an injected port at the existing effects boundary** (the F016 snapshot / F08
  Host interpreter precedent); the real reads happen only at that edge, and tests drive the pure derivation and
  the port against a real fixture repository — no live network.
- **No new third-party dependency, no schema, and no schema-version bump** are expected; the sensing is a new
  capability layered on the merged thread (the constitution's "heavier capabilities layer on top, not into the
  core").
- **The host command and projection are separate following rows.** This row produces an in-memory facts value
  and snapshot; persisting, embedding, projecting to `release.json`, or wiring an exit code are out of scope.

## Out of Scope / Deferred to Later Rows

- **The `fsgg release` host command** that wires sensing → the F053 core → enforcement → exit code, and its
  run-mode/profile CLI flags — a following row.
- **The additive `release.json` document projection** of the release findings and verdict — a following
  projection row (the RouteJson / AuditJson / CacheEligibilityJson precedent).
- **Evaluating rules into findings, deriving effective severity, and rolling up the verdict** — that is the
  already-merged F053 pure core, which this row only feeds.
- **Attestation / trusted-publishing execution** (emitting SLSA / in-toto-shaped provenance, performing a
  publish) — later rows; this row only senses whether the declared provenance/publishing expectations are
  evidenced as met.
- **The `fsgg verify` gate** — the sibling Phase 13 deliverable, its own thread.
