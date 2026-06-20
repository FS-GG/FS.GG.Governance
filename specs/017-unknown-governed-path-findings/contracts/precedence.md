# Finding precedence, suppression, dedup, and ordering contract (F017)

This is the documented, deterministic decision contract for unknown-governed-path findings,
referenced by `Findings.fsi`. It exists so FR-007's mandate — *"the precedence between routine
suppression and protected escalation MUST be explicitly documented and tested"* — is met in a
single normative place, and so the determinism FR-009/SC-004 promises is reproducible by hand.

## Inputs consumed

| Input | Source | Used for |
|---|---|---|
| `report.Routings : PathRouting list` | F015 routing | the per-path `RoutingResult` (`Routed` / `UnmatchedInRoot` / `OutOfScope`) — the only thing that selects which paths are *candidates* for a finding |
| `facts.Capabilities.Surfaces : Surface list` | F014 typed facts | the declared `Routine` suppressors and `ProtectedSurface` escalators and their `Paths` / `Id` |

Nothing else is read. `report.Diagnostics` is ignored. The governed root is **not** re-derived —
`UnmatchedInRoot` already means "in root, unmatched" and `OutOfScope` already means "outside the
root"; F017 trusts that (edge case *"Governed root is a subdirectory"*, FR-014).

## Surface membership (the segment-prefix relation)

A candidate path *p* is **within** a surface *s* iff *p* equals, or is a segment-prefixed
descendant of, **any** declared path in `s.Paths`. This is the same pure segment-prefix relation
F015 used for the governed root (`inRoot`): split on `/`, drop `""` and `.` segments, and test
that the surface-path segments are a prefix of *p*'s segments. It is decided on the **normalized
`GovernedPath` form only** — never raw or host paths (FR-014, edge case *"Surface paths vs
candidate paths"*). A surface with an empty `Paths` list matches nothing.

Only two `SurfaceClass` values affect the decision in this MVP:

- **`Routine`** — a *suppressor*. An `UnmatchedInRoot` path within it yields no finding.
- **`ProtectedSurface`** — an *escalator*. An `UnmatchedInRoot` path within it is escalated.

`GovernedRoot`, `GeneratedView`, and `ReleaseSurface` surfaces are **inert** here: they neither
suppress nor escalate, so a path covered only by them is an ordinary governed-root unknown. (A
later Phase-2/Phase-5 row may give those classes meaning; this row does not.)

## Per-path decision (FR-002 / FR-003 / FR-004 / FR-005 / FR-006 / FR-007)

For each **unique** candidate path (see dedup below), by its `RoutingResult`:

| Routing outcome | Decision |
|---|---|
| `Routed _` | **No finding.** A classified path is never an unknown, even if it also lies on a protected boundary (FR-005). |
| `OutOfScope` | **No finding.** Outside the governed root; no global default-deny (FR-003). |
| `UnmatchedInRoot` | Classify against surfaces by the precedence ladder below. |

### Precedence ladder for an `UnmatchedInRoot` path — `Protected > Routine > Ordinary`

1. **Protected (escalate).** If the path is within ≥1 `ProtectedSurface`, emit one finding with
   `Id = UnknownProtectedBoundaryPath` and `Zone = ProtectedBoundaryUnknown sid`, where `sid` is
   the **ordinal-first `SurfaceId`** among the matching protected surfaces (compare by
   `String.CompareOrdinal` on the underlying string). **Protected outranks Routine**: a path
   declared *both* routine and protected is escalated, never silenced — the fail-safe posture for
   a boundary the gate exists to defend (resolves FR-007's open routine-vs-protected case). When
   the path is *also* within a routine surface, the message names both the protected and the
   routine surface so the contradictory declaration is fixable (see Messages).
2. **Routine (suppress).** Else, if the path is within ≥1 `Routine` surface, emit **no finding** —
   an explicitly-declared unmanaged region is, by declaration, not an unknown governed path
   (FR-004).
3. **Ordinary.** Else, emit one finding with `Id = UnknownGovernedPath` and
   `Zone = GovernedRootUnknown` (FR-002).

This ladder is a **total order over the three outcomes** and is independent of the order surfaces
were authored in (FR-009): membership is a set test and the protected tiebreak is by `SurfaceId`
ordinal, never by declaration order.

> **Worked precedence example.** Path `src/Kernel/New.fs`, routed `UnmatchedInRoot`.
> Surfaces: `Routine{Id="legacy"; Paths=["src/Kernel"]}` and
> `ProtectedSurface{Id="kernel-core"; Paths=["src/Kernel"]}`. Both match. Rung 1 fires →
> one finding, `UnknownProtectedBoundaryPath`, `ProtectedBoundaryUnknown (SurfaceId "kernel-core")`,
> message naming both surfaces. Reverse the authoring order of the two surfaces → identical result.

## Deduplication (FR-010 / SC-007)

A path may appear in `report.Routings` more than once (e.g. the caller concatenated the routed
F016 committed/dirty/untracked planes). **Group `Routings` by normalized path and keep one routing
per path** before deciding. Result: exactly one finding (or none) per distinct path. Because the
decision is path+surface keyed and never plane keyed, the same unclassified in-root path yields the
*same* finding whichever plane it came from (SC-007). The plane is not retained on the finding in
this MVP.

**Why the kept routing is unambiguous (FR-009).** `Routing.route` is a pure total function of
`(facts, path)`, so every `PathRouting` in a path-group carries an **identical** `RoutingResult` —
there is no "which result wins" question to answer, and the kept value is the same regardless of
which duplicate is chosen or what order the input arrived in. The dedup therefore keeps the first
member of each path-group and the choice is **value-immaterial**; it does NOT depend on input
order, so permutation-invariance holds. (Should a hand-built `Routings` list ever pair one path
with *differing* results — outside the real input domain, since routing is deterministic — this
rule still yields exactly one finding; F017 deliberately does NOT define a semantic winner for that
impossible-by-construction case, and the dedup test exercises only the realistic identical-result
duplicate.)

(In practice F015 `route` already emits one `PathRouting` per input path, so duplicates only arise
when a caller concatenates several routed sets; the dedup is defensive and is tested directly by
constructing a `Routings` list with a repeated path that carries the same `RoutingResult`.)

## Ordering (FR-009 / SC-004)

`FindingReport.Findings` is sorted by:

1. `Path` — `String.CompareOrdinal` on the normalized path string;
2. then `findingIdToken Id` — `String.CompareOrdinal` on the stable id token.

(Two findings can never share a path after dedup, so the id key is a defensive secondary.)
Identical inputs ⇒ byte-identical list; re-ordering the candidate paths or the authored surfaces
⇒ unchanged list.

## Messages (FR-008 / SC-006)

Every finding's `Message`:

- names the **offending normalized path**;
- offers **≥1 concrete remediation** — "declare a path-map glob", "mark the region routine", or
  "classify the surface";
- for a protected-boundary finding, names the **escalating `SurfaceId`**; and when the path is
  *also* within a routine surface, names that routine `SurfaceId` too and states that protected
  precedence applied, so the contradictory declaration is actionable;
- contains **no raw YAML, no host paths, no timestamps**, and no product vocabulary beyond
  declared domain/surface ids.

## Stable id tokens

| `FindingId` | token |
|---|---|
| `UnknownGovernedPath` | `unknownGovernedPath` |
| `UnknownProtectedBoundaryPath` | `unknownProtectedBoundaryPath` |

## Out of scope (FR-013)

No severity, base/effective enforcement, profile/mode/maturity adjustment; no gate registry or
`GateId`; no evidence freshness, ship verdict, or route/audit JSON; no CLI command. F017 stops at
the typed `FindingReport` those later rows consume.
