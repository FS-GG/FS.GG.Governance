# Phase 0 Research: Unknown Governed Path Findings (F017)

All Technical Context unknowns are resolved below. No `NEEDS CLARIFICATION` remains. The single
genuinely open spec question (FR-007 routine-vs-protected precedence) was settled with the
maintainer at plan time (D4).

## D1 — Project home: a new `FS.GG.Governance.Findings` library

**Decision.** Land the feature as a new optional, packable library `FS.GG.Governance.Findings`
(plus its test project), sibling to Config/Routing/Snapshot. It references **`FS.GG.Governance.Config`**
(typed-fact model: `GovernedPath`, `SurfaceId`, `Surface`, `SurfaceClass`, `TypedFacts`) and
**`FS.GG.Governance.Routing`** (the `RouteReport` / `PathRouting` / `RoutingResult` it consumes).
No new third-party `PackageReference`: its own code is BCL + FSharp.Core only (the transitive
YamlDotNet edge arrives only via Config and is unused here).

**Rationale.** This mirrors the established one-library-per-row shape (F014 Config, F015 Routing,
F016 Snapshot). It keeps the dependency direction strictly one-way —
`Findings → Routing → Config` and `Findings → Config` — and the kernel never sees this
surface/finding vocabulary (FR-015, constitution operating rule). It is the natural consumer of
*both* predecessors, which a single new sibling expresses cleanly.

**Alternatives rejected.**
- *Add a module to `FS.GG.Governance.Routing`.* Rejected: F015 explicitly deferred the finding
  decision to "a later Phase-2 row" and is scoped to routing only; folding findings in would
  blur that boundary and bloat Routing's surface. A separate library keeps each row's surface
  baseline independently blessed.
- *Add to `FS.GG.Governance.Config`.* Rejected: this is a *classification of routing outcomes*,
  not a parse of YAML; it depends on Routing, which Config must not.
- *Put it in Host/Cli.* Rejected: the model and classifier are product-neutral pure values; the
  whole-report consumers (the later `route`/`ship` commands) live in Cli and will *reference*
  Findings, exactly as they reference Routing/Snapshot.

## D2 — Boundary shape: a pure total function, no MVU

**Decision.** A single pure entry point `Findings.findUnknownGovernedPaths : TypedFacts ->
RouteReport -> FindingReport`, over a `Model` of finding types. No `Model`/`Msg`/`Effect`/`update`.

**Rationale.** The feature performs no I/O, senses no git, holds no multi-step state — it is a
deterministic classification of already-typed inputs (FR-011). Principle IV mandates the MVU
boundary only for stateful or I/O-bearing features; this is neither, so the plain pure function
is the idiomatic choice (Principle III), exactly as F015 `route` is. Sensing (F016) and YAML
parsing (F014) own the I/O; this row only classifies their typed outputs.

## D3 — Surface membership: reuse the segment-prefix relation, implemented locally

**Decision.** Decide "candidate path is within a declared surface" by the **same segment-prefix
relation** F015 used for the governed root (split on `/`, drop `""`/`.`, test prefix), implemented
as a small private helper in `Findings.fs` over `Surface.Paths`.

**Rationale.** Surface membership and root membership are the same containment relation on the
normalized `GovernedPath` form. Reproducing the ~5-line pure string test locally avoids a
cross-feature Tier-1 touch to expose `Routing.inRoot`, keeps Routing's public surface minimal,
and keeps the relation product-neutral. The normalization itself is *not* duplicated — paths
arrive already normalized (single-sourced `Config.normalizePath`, F016 D7); F017 only compares.

**Alternative rejected.** *Expose `Routing.inRoot` and call it.* Rejected for now: it enlarges
Routing's surface baseline for a trivially reproducible relation whose semantics here ("within a
surface") differ from its name ("in root"). If a third consumer ever needs it, promoting a shared
`Governed.contains` becomes worthwhile; one reuse does not justify it.

## D4 — FR-007 precedence: `Protected > Routine > Ordinary` (escalation outranks suppression)

**Decision (maintainer-confirmed at plan time).** When one `UnmatchedInRoot` path is covered by
*both* a `Routine` surface and a `ProtectedSurface`, **protected wins** — the path is escalated to
the protected-boundary finding, not suppressed. The full total order is
`Protected > Routine > Ordinary`. The finding's message names both surfaces so the contradictory
declaration is fixable.

**Rationale.** The two error modes are asymmetric: "protected wins" wrong → a *visible, trivially
fixable* false alarm; "routine wins" wrong → a `Routine` declaration *silently disarms a boundary
the gate exists to defend* — the one failure a protective tool must never have. The spec's own
tone ("more emphatic", "boundary the gate exists to defend") and its only explicit ruling
(protected outranks *ordinary*, FR-007) both point the same way; making protected the unconditional
top of the order is the simplest total rule. The contradiction is surfaced (named surfaces),
not hidden.

**Alternative rejected.** *Routine wins (respect the explicit opt-out).* Defensible — an explicit
unmanaged declaration is intentional — but it trades a silent security hole for reduced noise,
the wrong trade for a governance gate. A heavier third option (emit a distinct "conflicting
surface declaration" diagnostic) was deferred: that is closer to F014 catalog validation than to
this finding row, and the escalated finding + dual-surface message already make the conflict
visible.

## D5 — Input contract: consume the `RouteReport`, key on path

**Decision.** Take the F015 `RouteReport` directly (read `Routings`, ignore `Diagnostics`) plus
`TypedFacts`. Deduplicate `Routings` by normalized path (keep ordinal-first) before deciding;
sort the output by (path, finding-id token).

**Rationale.** This is the faithful "feed routing's output straight in" contract (mirrors the
F016→F015 feed-through). Keying on path makes US5 plane-uniformity automatic: the decision never
consults a plane, so the same in-root unknown path yields the same finding regardless of plane,
and a path appearing in several planes collapses to one finding (FR-010, SC-007). The plane is not
modeled as a type in this MVP — FR-010 says it *MAY* be retained, and the MUSTs (uniform decision,
single finding) are met by path-keying. A later row that needs plane provenance on the finding can
add it additively.

**Alternative rejected.** *Introduce a `ChangePlane` DU and tag each finding.* Rejected as
premature: it adds surface for a value no consumer in scope reads, and risks letting the plane
leak into the decision. Determinism and dedup are cleaner without it.

## D6 — Determinism mechanics

**Decision.** Membership is a pure set test; the protected-surface tiebreak is the ordinal-first
`SurfaceId`; the final list is sorted by `String.CompareOrdinal` on the path then the id token —
the identical ordinal-sort discipline F014/F015/F016 use. Any `mutable` accumulator in the
fold is disclosed at the use site (Principle III).

**Rationale.** Reusing the repo-wide ordinal-sort convention guarantees byte-identity (FR-009,
SC-004) and cross-feature consistency, and makes "re-order the inputs ⇒ unchanged output"
hold by construction rather than by test luck.

## D7 — Testing strategy (Principle V real evidence)

**Decision.** Pure unit + property tests over real in-memory `TypedFacts` and real
`RouteReport`s — the actual values a downstream caller passes, not mocks. No git, no filesystem,
no network is reachable from this feature, so "real evidence" here *is* real typed inputs:
- US1/US2 (`FindingDecisionTests`): `UnmatchedInRoot` non-routine ⇒ one finding; `Routed`,
  `OutOfScope`, and routine-covered ⇒ none; mixed sets ⇒ finding per non-routine in-root unknown.
- US3 (`PrecedenceTests`): protected ⇒ escalated id/zone carrying the `SurfaceId`; ordinary vs
  protected distinguishable; overlapping routine+protected ⇒ single escalated finding by D4.
- US4 (`DeterminismTests`): compute twice ⇒ identical; FsCheck permutation of candidate paths and
  authored surfaces ⇒ unchanged list; every message names the path and ≥1 remediation.
- US5 (`PlaneUniformityTests`): same path "from" each plane ⇒ same decision; duplicate path in
  `Routings` ⇒ single finding (dedup).
- `SurfaceDriftTests`: guards `surface/FS.GG.Governance.Findings.surface.txt`.
- An FSI transcript in `scripts/prelude.fsx` exercises the packed surface end-to-end (Principle I).

**Rationale.** A pure classifier's honest evidence is real typed facts and real routing outcomes
exercised through the public surface, asserting behavior not internals (Principle V). No synthetic
evidence is anticipated; if a literal stands in for an un-derivable case it carries `Synthetic` in
the test name with a use-site disclosure.

## Resolved Technical Context

| Field | Value |
|---|---|
| Language/Version | F# on .NET `net10.0` (Directory.Build.props) |
| Primary Dependencies | `FS.GG.Governance.Config` + `FS.GG.Governance.Routing` project refs; **no new third-party package** |
| Storage | None — pure in-memory values |
| Testing | `dotnet test` (Expecto + FsCheck via VSTest) |
| Target Platform | Cross-platform .NET library |
| Project Type | Optional packable F# class library + one test project |
| Performance Goals | Deterministic, O(paths × surfaces) classification + one sort; byte-identical output |
| Constraints | Pure, total, never throws, no I/O/git/clock; ordinal-deterministic; no raw YAML/host paths/timestamps |
| Scale/Scope | One new src project + one test project; modules `Model`, `Findings`; one closed `FindingId` set |
