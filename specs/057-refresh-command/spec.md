# Feature Specification: The `fsgg refresh` Host Command

**Feature Branch**: `057-refresh-command`

**Created**: 2026-06-25

**Status**: Planned

**Input**: User description: "next item in plan" — Phase 7 (Generated Views And Refresh). The SDD-owned
`fsgg-sdd refresh` shipped in 2026-06-20, but the **Governance-owned** refresh row is still partial: "Add
Governance `fsgg refresh` for gate metadata, rule catalogs, capability docs, skill references, API-surface
docs, route projections, and baselines. (Governance-owned; out of SDD scope.)"
(`docs/initial-implementation-plan.md`, Phase 7). This feature is that command: the single Governance
regeneration entry point that, for a governed repository, regenerates its Governance-owned generated views
from their declared sources, detects views that have drifted out of currency with those sources, and emits a
deterministic refresh artifact and a process exit code.

Two scope decisions were confirmed with the requester at specification time (via the clarification step):

1. **`fsgg refresh` is the next roadmap row "Governance `fsgg refresh`" (Phase 7, line ~810)** — the
   regeneration entry point for the Governance-owned generated views, distinct from the separate Phase-7 row
   "Block stale generated views at the configured Governance boundary" (line ~815), which wires drift into the
   protected-branch merge gate and remains a later feature. This feature *regenerates and reports*; the
   boundary *enforcement* wiring is out of scope here.
2. **`fsgg refresh` writes regenerated views to the working tree by default; `--dry-run` reports without
   writing.** This matches the design's framing of refresh as "the single regeneration entry point" and the
   SDD `fsgg-sdd refresh` precedent (which regenerates `work-model.json`/`summary.md` in place). Unlike the
   read-only sibling commands (`route`/`ship`/`release`/`verify`), refresh *mutates the working tree by design*
   — but only the declared generated views, and only those that are stale; `--dry-run` performs no writes and
   reports the planned regeneration plus any drift.

## Overview

The Governance command suite can already **select** the gates a change warrants (`fsgg route`), **recompute the
protected merge-boundary verdict** (`fsgg ship --mode gate`), evaluate **cache eligibility** (`fsgg
cache-eligibility`), **gate a release** (`fsgg release`), and **verify a change pre-PR** (`fsgg verify`). Each
of those commands *emits* its own deterministic projection on demand and reports when an input view is stale,
but **none of them regenerates a generated view that has fallen out of date with its declared sources.** A
governed repository accumulates generated views — rendered gate metadata, a rendered rule catalog, capability
and API-surface docs, route projections, committed baselines — and when an authoritative source changes (a
rule hash, a generator version, a gate definition, a captured artifact), those views silently drift.

What is still missing is the command a developer (or a scheduled job) runs to **make the generated views
current again**: a single `fsgg refresh` that, for a governed repository, reads the declared relationship
between each generated view and its sources, **detects which views are stale** by comparing declared-source
digests and generator versions against each view's recorded provenance (not by mere file presence),
**regenerates** each stale view from its current sources, **marks** the regenerated views as outputs with
refreshed provenance, **reports** a clear summary (human text plus a deterministic `refresh.json` artifact),
and **exits** with a code that lets automation distinguish "everything was already current" from "views were
regenerated" from "a view is stale and could not be brought current."

`fsgg refresh` is the regeneration entry point, not the merge authority. It brings the repository's generated
views back into currency and tells the developer what changed; it does not by itself decide whether a merge is
allowed. Routing drift into the protected-branch verdict remains a separate concern (the Phase-7 "block stale
generated views at the boundary" row). `fsgg refresh` and that boundary enforcement may share the same
currency evaluation, but they serve different stages: refresh is "make my generated views current," boundary
enforcement is "refuse the merge if they are not."

This feature is a new standalone executable, `FS.GG.Governance.RefreshCommand`, built to the exact
pure-core + injected-edge shape of the existing `route`/`ship`/`cache-eligibility`/`release`/`verify` commands
(a pure MVU `Loop` boundary, an `Interpreter` that binds real ports at the edge, a thin `Program.fs`), plus a
deterministic `refresh.json` projection shipped as a separate pure library mirroring the
`AuditJson`/`RouteJson`/`GatesJson`/`ReleaseJson`/`VerifyJson` precedent. It composes the existing
freshness/provenance and view-projection cores; the precise project layout and the precise reuse of the
existing rendering modules are planning decisions deferred to `/speckit-plan`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Bring stale generated views current with one command (Priority: P1)

A developer working in a governed repository has changed an authoritative source — a rule, a gate definition,
or a regenerated artifact — and the repository's generated views (rendered gate metadata, the rule catalog, a
route projection, a baseline) are now out of date. The developer runs `fsgg refresh`. The command detects
which generated views are stale relative to their declared sources, regenerates exactly those views from their
current sources (leaving already-current views untouched), reports which views it regenerated, and exits with a
code that says whether anything was regenerated — so the developer commits up-to-date views instead of stale
ones.

**Why this priority**: This is the entire reason the command exists and the only slice that delivers
end-to-end value — the first time a developer can bring all the Governance-owned generated views back into
currency from one local command. Every other story refines or hardens this flow.

**Independent Test**: Point the command at a fixture repository whose generated views are all current and
confirm it regenerates nothing and reports "all current"; change one source so its dependent view goes stale,
re-run, and confirm the command regenerates exactly that one view (and no other), reports it as regenerated,
and that the regenerated view matches what the renderer produces from the current source.

**Acceptance Scenarios**:

1. **Given** a governed repository whose generated views are all current with their declared sources, **When**
   a developer runs `fsgg refresh`, **Then** the command regenerates nothing, reports every view as already
   current, and exits with the distinct "nothing to refresh" success code.
2. **Given** a generated view that is stale because its declared source changed, **When** a developer runs
   `fsgg refresh`, **Then** the command regenerates exactly that view from its current source, reports it as
   regenerated, leaves every already-current view untouched, and exits with the distinct "views regenerated"
   code.
3. **Given** several stale views with independent sources, **When** a developer runs `fsgg refresh`, **Then**
   the command regenerates each stale view, reports each one, and the result is identical regardless of the
   order in which the views are regenerated.
4. **Given** a repository that declares no Governance-owned generated views, **When** a developer runs `fsgg
   refresh`, **Then** the command reports that there is nothing to refresh and exits with the "nothing to
   refresh" success code (an empty manifest is not an error).

---

### User Story 2 - Preview the refresh without writing (Priority: P2)

Before regenerating views in place — for example in CI, in a read-only checkout, or when reviewing what a
refresh would change — a developer runs `fsgg refresh --dry-run`. The command performs the same currency
evaluation and reports exactly which views are stale and would be regenerated, but writes nothing to the
working tree. The dry-run report names each stale view, its drifted source(s), and why it is stale, and exits
with a code that distinguishes "everything is current" from "a refresh would regenerate views."

**Why this priority**: This is what makes refresh safe to run in CI and in review without mutating the
checkout, and it is the "detect stale generated views" half of the design intent surfaced without the write.
It builds directly on Story 1's currency evaluation and is independently testable once Story 1 exists.

**Independent Test**: Run `fsgg refresh --dry-run` against a fixture with a stale view and confirm it reports
that view as stale-and-would-be-regenerated, that the working tree is byte-for-byte unchanged afterward, and
that its exit code distinguishes "would regenerate" from "all current"; run it against an all-current fixture
and confirm it reports all-current and writes nothing.

**Acceptance Scenarios**:

1. **Given** a stale generated view, **When** a developer runs `fsgg refresh --dry-run`, **Then** the command
   reports that the view is stale and would be regenerated, names the drifted source(s) and the reason, and
   makes no change to the working tree.
2. **Given** an all-current repository, **When** a developer runs `fsgg refresh --dry-run`, **Then** the
   command reports everything as current, writes nothing, and exits with the "nothing to refresh" success code.
3. **Given** any `fsgg refresh --dry-run` invocation, **When** it completes, **Then** the working tree is
   byte-for-byte identical to before the run (no view, manifest, or store is written).

---

### User Story 3 - Deterministic `refresh.json` for tooling and CI (Priority: P3)

A scheduled job or another tool runs `fsgg refresh` and consumes a `refresh.json` artifact that projects, per
generated view, its currency status (current / regenerated / stale-unresolved), the source(s) that drove the
decision, and the overall summary. For identical repository state and identical regeneration outcomes the
artifact is byte-for-byte identical, so it can be diffed, cached, and asserted against a golden baseline; the
process exit code lets the job branch on whether anything was regenerated or left unresolved.

**Why this priority**: Deterministic machine output is what lets refresh be wired into automation and
regression-tested, but it depends on Stories 1–2 producing the currency evaluation it projects.

**Independent Test**: Run `fsgg refresh` (or `--dry-run`) twice with the requested artifact over a fixture with
identical state and identical outcomes and confirm the two `refresh.json` files are byte-identical; confirm the
artifact omits timestamps, absolute paths, and machine-specific content; confirm the printed machine output
equals the persisted file.

**Acceptance Scenarios**:

1. **Given** identical repository state and identical regeneration outcomes across two runs, **When** `fsgg
   refresh` is asked to emit `refresh.json` each time, **Then** the two files are byte-for-byte identical.
2. **Given** a request for machine output, **When** `fsgg refresh` runs, **Then** the machine output it prints
   is the verbatim content of the persisted `refresh.json` (one source of truth).
3. **Given** the artifact is generated, **When** it is inspected, **Then** it carries a versioned schema
   identifier and contains no timestamp, absolute path, username, or other machine-specific content.

---

### Edge Cases

- **No generation manifest / no declared generated views**: the repository declares no Governance-owned
  generated views ⇒ Success with a "nothing to refresh" report (an empty manifest is not an error), and no
  artifact mutation beyond the optionally-requested `refresh.json`.
- **Malformed or unreadable manifest / source**: the repository's declared sources or generation manifest are
  absent, malformed, or unreadable so currency cannot be evaluated ⇒ `InputUnavailable` (exit `3`) with a
  diagnostic that distinguishes missing input from a tool defect; no partial regeneration is left behind.
- **A view cannot be regenerated (renderer/IO defect)**: a view is stale but the regeneration itself fails for
  a tool/IO reason (e.g. an unwritable output path, a renderer defect) ⇒ `ToolError` (exit `4`), kept distinct
  from a view that was successfully regenerated and from a view reported as stale-unresolved; the working tree
  is left with no partially-written view.
- **A view is stale and its source is itself unresolved/uncertain**: the view cannot be brought current because
  its source state cannot be resolved; it is surfaced as stale-unresolved and is never silently treated as
  current; in a non-dry run this is a distinct non-success outcome rather than a fabricated "refreshed".
- **Invalid command-line arguments** (unknown flag, missing value, mutually exclusive selectors) ⇒ `UsageError`
  (exit `2`) with usage guidance; a typo writes no view and no artifact.
- **Partial scope selection**: the developer scopes the refresh to a subset of views (e.g. a specific view kind
  or work-item id); only the in-scope views are evaluated and regenerated, and out-of-scope views are reported
  as not-evaluated rather than silently assumed current.
- **Already-current run**: every view is current ⇒ no view is written, the run is reported as "nothing to
  refresh," and (absent any other failure) it exits with the distinct "nothing to refresh" success code.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a single host command, `fsgg refresh`, that takes a governed repository
  and produces a refresh summary and a process exit code.
- **FR-002**: The system MUST determine, for each declared Governance-owned generated view, whether it is
  **current or stale** by comparing the recorded provenance of the view (its source digest(s) and generator
  version) against the current state of its declared sources — by digest and generator version, **not** by file
  presence — reusing the existing freshness/provenance machinery rather than introducing a new staleness
  mechanism.
- **FR-003**: The system MUST, by default, **regenerate** each stale view from its current sources, leaving
  already-current views byte-for-byte untouched, and MUST record refreshed provenance for each regenerated view
  so a subsequent run detects it as current.
- **FR-004**: The system MUST provide a **`--dry-run`** mode that performs the same currency evaluation, reports
  which views are stale and would be regenerated (and why), and writes nothing to the working tree.
- **FR-005**: The system MUST **mark** every regenerated view as a generated output carrying its source
  relationship and generator version, so consumers can detect future drift; it MUST NOT turn a generated view
  into a second source of truth.
- **FR-006**: The system MUST report a summary in human-readable text by default, and MUST emit a deterministic
  machine artifact (`refresh.json`) when requested, projecting per-view currency status (current / regenerated /
  stale-unresolved), the source(s) that drove each decision, and the overall outcome.
- **FR-007**: The machine artifact MUST be **byte-for-byte identical** for identical repository state and
  identical regeneration outcomes, and MUST contain no timestamp, absolute path, username, or other
  machine-specific content; the printed machine output MUST equal the persisted file verbatim (one source of
  truth).
- **FR-008**: The machine artifact MUST carry a **versioned schema identifier**.
- **FR-009**: The system MUST exit with one of distinguishable codes covering: success with nothing to refresh,
  success with views regenerated, at least one view stale-and-unresolved (could not be brought current),
  invalid arguments, absent/invalid governing inputs the host cannot proceed past, and a genuine tool/IO defect
  — each distinguishable from the others (the precise numeric assignment, mirroring the
  `release`/`verify`/`ship` five-way contract, is a planning decision).
- **FR-010**: The system MUST be **fail-safe**: a missing, unreadable, or unexpected input, or a view whose
  currency cannot be evaluated, MUST resolve to a stale-unresolved finding or a distinct non-success exit —
  never a fabricated "all current" report, never a silently-skipped stale view, and never a crash.
- **FR-011**: The system MUST be **product-neutral**: it MUST NOT hardcode any product identity, view identity,
  path, generator version, or source identity; the set of generated views, their sources, and their renderers
  all come from the governed repository's declared sources (so the same command serves gate metadata, rule
  catalogs, capability docs, skill references, API-surface docs, route projections, and baselines without
  naming any of them in code).
- **FR-012**: The system MUST treat an **empty set of declared generated views** (or an in-scope subset that is
  empty) as Success with a "nothing to refresh" report, not as an error.
- **FR-013**: The system MUST NOT mutate the governed repository except for (a) the generated views it
  regenerates (and their recorded provenance) in a non-dry run, written atomically, (b) the
  explicitly-requested `refresh.json` artifact, and (c) any opt-in persistence the shared cores already
  perform; in `--dry-run` it MUST write nothing; on a tool error it MUST leave no partial view and no partial
  artifact behind.
- **FR-014**: The system MUST be **network-free** in its own logic (verifiable by a scope guard).
- **FR-015**: The system MUST accept a documented way to **scope** the refresh to a subset of the declared views
  (for example by view kind or by work-item id), reject mutually exclusive or invalid selectors as a usage
  error, apply a documented default scope (all declared views) when none is given, and report out-of-scope
  views as not-evaluated rather than assumed current.
- **FR-016**: Diagnostics MUST distinguish a missing/malformed **input** from a **tool defect** in both message
  and exit code, and MUST name the offending source/view so a developer can fix it.
- **FR-017**: `fsgg refresh` MUST NOT be presented or used as the protected merge-boundary authority; it brings
  generated views current and reports drift, but routing drift into the merge verdict (the Phase-7 "block stale
  generated views at the boundary" row) is out of scope for this feature. A clean `fsgg refresh` does not by
  itself authorize a merge.

### Key Entities *(include if data involved)*

- **Refresh request**: the governed repository, the view scope (all declared views / a view-kind or work-item
  subset), the mode (write vs `--dry-run`), the output format, and the optional `refresh.json` output path.
- **Generation manifest entry**: the declared relationship between one generated view and its sources — the
  view identity/kind and output location, the renderer/generator and its version, the declared source(s) and
  their digests, and the currency basis used to decide staleness.
- **Generated view**: a Governance-owned rendered output (e.g. rendered gate metadata, a rule catalog, a route
  projection, a capability/API-surface doc, a baseline) that is derived from declared sources and can drift out
  of currency with them.
- **Currency status**: per generated view — current (untouched), regenerated (was stale, brought current this
  run), or stale-unresolved (stale but could not be brought current), with the drifted source(s) and the reason.
- **Refresh summary**: the overall outcome plus the per-view currency statuses and the count of views
  regenerated, left current, and left unresolved.
- **`refresh.json` artifact**: the deterministic, versioned projection of the refresh summary and per-view
  currency statuses.
- **Exit decision**: the process-result category (nothing-to-refresh / views-regenerated / stale-unresolved /
  UsageError / InputUnavailable / ToolError).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can bring every stale Governance-owned generated view in a repository back into
  currency with a single command, regenerating exactly the stale views and leaving current views untouched.
- **SC-002**: A re-run immediately after a successful refresh regenerates nothing (every view is detected as
  current) and exits with the "nothing to refresh" success code; staleness is detected by source/generator
  digest, not by file presence.
- **SC-003**: `fsgg refresh --dry-run` leaves the working tree byte-for-byte unchanged while still reporting
  every stale view that would be regenerated and why.
- **SC-004**: For identical repository state and identical regeneration outcomes, two `refresh.json` artifacts
  are byte-for-byte identical, contain no timestamp/absolute-path/machine-specific content, and equal the
  printed machine output verbatim.
- **SC-005**: Every failure mode resolves to the correct, distinguishable exit code; no input or evaluation
  failure produces a fabricated "all current" report or silently skips a stale view, and no tool error leaves a
  partial view or partial artifact behind.
- **SC-006**: The command introduces no hardcoded product identity, view identity, path, generator version, or
  source identity — the set of views, their sources, and their renderers all come from the repository's declared
  sources (verifiable by inspection).
- **SC-007**: The command's own logic performs no network access (verifiable by a scope guard).

## Assumptions

- **Next item resolution**: "next item in plan" is the Phase-7 row "Add Governance `fsgg refresh` for gate
  metadata, rule catalogs, capability docs, skill references, API-surface docs, route projections, and
  baselines" (`docs/initial-implementation-plan.md`). The SDD-owned `fsgg-sdd refresh` (feature `015`) is
  complete and out of scope; this is the Governance-owned analogue.
- **Scope boundary vs the enforcement row**: this feature *regenerates and reports* generated-view currency.
  The separate Phase-7 row "Block stale generated views at the configured Governance boundary" — wiring drift
  into the protected-branch merge verdict — is a later feature and out of scope here (FR-017). Refresh's own
  exit code still distinguishes "a view is stale and unresolved," so a CI job can fail on it, but refresh does
  not modify the `fsgg ship` merge verdict.
- **Write-by-default with `--dry-run`**: confirmed with the requester at specification time. `fsgg refresh`
  regenerates and writes stale views in place by default (the design's "single regeneration entry point" and
  the SDD-refresh precedent), and `--dry-run` performs the same evaluation while writing nothing. This is the
  one place refresh diverges from the read-only `route`/`ship`/`release`/`verify` posture, and it is by design.
- **Composition, not new sensing**: `fsgg refresh` composes the already-merged freshness/provenance machinery
  (the source-digest / generator-version / output-digest currency notion the freshness cores already express)
  and the existing view renderers; it adds no new repository sensing beyond reading the declared sources and
  the views' recorded provenance. The precise reuse of the existing rendering/projection modules and the
  generation-manifest representation are planning decisions deferred to `/speckit-plan`.
- **Currency by digest, not presence**: a view is stale iff its declared sources' digests or generator version
  no longer match the view's recorded provenance — never by file mtime or mere presence/absence.
- **Default scope**: when no scope selector is given, refresh evaluates and (in a non-dry run) regenerates all
  declared Governance-owned generated views; an explicit view-kind or work-item selector narrows it, and
  mutually exclusive or invalid selectors are a usage error.
- **Exit-code parity**: the exit-code contract mirrors the `release`/`verify`/`ship` family's distinguishable
  five-way shape (`Success` / a blocking-or-unresolved code / `UsageError` / `InputUnavailable` / `ToolError`),
  refined for refresh's two success shades (nothing-to-refresh vs views-regenerated) and its stale-unresolved
  outcome; the precise numeric assignment is a planning decision.
- **Row-local surface, frozen cores untouched**: like the recent host-command rows, any new manifest or
  projection surface this row needs is row-local; the frozen `.fsgg` configuration schema (F014) and the
  existing freshness/projection cores are reused verbatim, not edited. (The precise project layout is a
  planning decision, deferred to `/speckit-plan`.)
- **No central dispatcher**: as with the other host commands, no central `fsgg` dispatcher is assumed or
  introduced; `fsgg refresh` is a standalone executable. A leading bare `refresh` token is tolerated/handled
  per the existing command precedent.
