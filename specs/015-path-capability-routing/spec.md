# Feature Specification: Path-to-Capability Routing with Deterministic Glob Precedence

**Feature Branch**: `015-path-capability-routing`

**Created**: 2026-06-20

**Status**: Draft

**Input**: User description: "start the next item in the implementation plan" — the next Governance-owned, unchecked row of Phase 2 (*Governance Ship Walking Skeleton And Catalog MVP*) in `docs/initial-implementation-plan.md` is **"Implement deterministic glob precedence for path-to-capability routing."** It is the first feature that *consumes* the typed capability facts produced by `014-fsgg-project-policy-capability-schemas` (F014).

## Overview

F014 gave a governed product a typed, YAML-free declaration of *what it governs*: among those facts is a **path map** — a list of `glob → capability-domain` bindings — declared in `.fsgg/capabilities.yml`, plus a single declared **governed root**. F014 deliberately stopped at the typed facts and did not *use* them.

This feature is the first consumer: given those typed capability facts and a set of repository-relative paths, it answers — deterministically and explainably — **"which capability domain does each path belong to?"** The hard part is not matching a single glob; it is deciding, when several globs match the same path, **exactly one** winner by a *total, reproducible precedence order*, so that the same inputs always produce the same routing and a human can see which glob won and why.

It deliberately stops at the routing result. It does not sense which files changed (git/CI facts are a later Phase-2 row), does not decide whether an unmatched path under a governed root is a blocking *finding* (the unknown-governed-path-findings row), does not assign surface classes, does not build the gate registry, and does not implement the `route` or `ship` commands or their JSON. This is the minimum that lets every later routing, gate, and ship feature stand on a deterministic path → capability answer.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Route a set of paths to their capability domains (Priority: P1)

A governed product has declared a path map (e.g. `src/**` → `core`, `docs/**` → `docs`, `**/*.fsproj` → `build`). A caller supplies a set of repository-relative paths — for example the paths it intends to check — and receives, for each path, the single capability domain it routes to and the exact glob that matched, or a clear "no capability matched" result.

**Why this priority**: Nothing in the capability/ship phase can select gates, scope checks, or explain a route until a path can be resolved to a capability. This is the MVP: a deterministic path → capability answer is independently valuable even before git sensing, findings, or any command exists.

**Independent Test**: Build typed capability facts (governed root + path map) directly from F014, hand the routing surface a list of paths, and assert each path maps to the expected domain with the expected matched glob, and that paths matching no glob are reported as unmatched — with no filesystem, git, or clock access involved.

**Acceptance Scenarios**:

1. **Given** a path map binding `src/**` → `core` and a path `src/Kernel/Eval.fs`, **When** the path is routed, **Then** the result is capability domain `core` and records `src/**` as the matched glob.
2. **Given** a path that matches none of the declared globs, **When** it is routed, **Then** the result is "unmatched" (no capability domain) and carries no arbitrary domain guess.
3. **Given** the same facts and the same path set routed twice, **When** the two results are compared, **Then** they are byte-for-byte identical, including the order of per-path results.

---

### User Story 2 - Resolve overlapping globs by a total, explainable precedence (Priority: P1)

A product's path map overlaps on purpose: `src/**` → `core` is the broad rule, but `src/Adapters/**` → `adapters` and `src/Kernel/Eval.fs` → `kernel-eval` are deliberately narrower. A path that matches several of these must route to **exactly one** capability — the most specific one — and the routing must explain which glob won and why, the same way on every run.

**Why this priority**: This is the literal deliverable named by the implementation-plan row ("deterministic glob *precedence*"). Overlapping globs are the normal authoring pattern (broad default plus narrow exceptions); without a total, reproducible precedence the same configuration could route a path two different ways on two runs, poisoning every downstream gate. Co-equal P1 with Story 1.

**Independent Test**: Author a path map with deliberately overlapping globs of differing specificity, route paths that match more than one, and assert each routes to the single most-specific domain, that the winner is stable across runs and across re-ordering of the path-map entries, and that the result records the precedence reason the winner was chosen.

**Acceptance Scenarios**:

1. **Given** both `src/**` → `core` and `src/Adapters/**` → `adapters` match `src/Adapters/SpecKit.fs`, **When** the path is routed, **Then** it routes to `adapters` because the more-specific glob wins, and the result records that precedence reason.
2. **Given** an exact-literal glob `src/Kernel/Eval.fs` → `kernel-eval` and a wildcard glob `src/Kernel/**` → `core` both matching `src/Kernel/Eval.fs`, **When** routed, **Then** the exact-literal glob wins over the wildcard.
3. **Given** a single-segment wildcard `src/*/Host.fs` and a cross-segment wildcard `src/**` both matching `src/Cli/Host.fs`, **When** routed, **Then** the single-segment wildcard is treated as more specific and wins.
4. **Given** the path-map entries are re-ordered in the authored source, **When** the same path is routed, **Then** the winning domain, matched glob, and precedence reason are unchanged.

---

### User Story 3 - Make genuinely ambiguous and out-of-scope cases explicit (Priority: P2)

Two globs can be *equally* specific and still both match one path (e.g. `src/*/Eval.fs` → `a` and `src/Kernel/*.fs` → `b` both match `src/Kernel/Eval.fs`). And some supplied paths lie entirely outside the declared governed root. A maintainer needs both handled explicitly: ambiguity must be reported (never an arbitrary silent pick) while still resolving to one deterministic winner so downstream stays total; out-of-scope paths must be reported as out-of-scope, not forced into a capability.

**Why this priority**: Stories 1–2 prove routing and precedence for the common case. P2 hardens the corners that otherwise produce non-determinism or surprising routes. It is still required for a trustworthy machine contract, but it builds on the P1 mechanism.

**Independent Test**: Author two equally-specific overlapping globs mapping to different domains, route a path both match, and assert a stable ambiguity diagnostic is emitted *and* a single deterministic winner is chosen; separately route a path outside the governed root and assert it is reported as out-of-scope with no capability and no ambiguity diagnostic.

**Acceptance Scenarios**:

1. **Given** two equally-specific globs mapping the same path to different domains, **When** routed, **Then** an `AmbiguousRoute` diagnostic names the path and both competing globs/domains, **and** the path still resolves to one deterministic winner (the lexicographically-first competing glob).
2. **Given** a path that lies outside the declared governed root, **When** routed, **Then** it is reported as out-of-scope — neither routed to a capability nor reported as an in-root unmatched path — and produces no ambiguity diagnostic.
3. **Given** a path within the governed root that matches no declared glob, **When** routed, **Then** it is reported as in-root unmatched, carrying enough information for the later unknown-governed-path-findings feature, **without** this feature deciding any finding severity.

---

### Edge Cases

- **Empty path map**: With no `glob → domain` bindings, every supplied in-root path is in-root unmatched and every out-of-root path is out-of-scope; no routing error is raised.
- **Catch-all glob**: A root-level `**` mapping is the least-specific possible match and loses to any glob with literal segments; it wins only when nothing more specific matches.
- **`**` matching zero segments**: `src/**` MUST match both `src/a/b.fs` and the shallow `src/a.fs`; the chosen zero-or-more semantics MUST be stated and applied consistently.
- **Identical globs, different domains**: If two path-map entries normalize to the *same* glob string but bind different capability domains, the conflict MUST be diagnosed deterministically rather than silently resolved by source order.
- **Path equal to the governed root**: A path that is exactly the governed root is in-root; whether it can match a glob follows the same rules as any other in-root path.
- **Separator / case portability**: Because matching runs over the F014-normalized `GovernedPath` form, paths authored with `\` vs `/`, leading `./`, or differing case MUST route identically; routing MUST NOT re-decide normalization F014 already settled.
- **Unrecognized glob construct**: A path-map glob containing a construct outside the supported MVP set MUST produce a clear configuration diagnostic, not a silent never-match.
- **Duplicate paths in the input set**: The same path supplied twice MUST route identically and the result MUST remain deterministically ordered.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The routing surface MUST accept, as input, the typed capability facts produced by F014 (the declared governed root, the declared capability domains, and the path map of `glob → capability-domain` bindings) together with a caller-supplied set of repository-relative candidate paths, and MUST produce one routing result per candidate path. It MUST NOT re-parse `.fsgg` YAML.
- **FR-002**: The routing surface MUST support a closed, documented MVP glob syntax over normalized governed paths — literal path segments, `?` (single character within a segment), `*` (wildcard that does not cross a path separator), and `**` (wildcard that may cross path separators, matching zero or more segments). No other glob metacharacters are supported in this feature.
- **FR-003**: Matching MUST be computed over the F014-normalized `GovernedPath` form (unified separators, resolved `.`/`..`, kept relative to the governed root). Routing MUST NOT perform additional normalization or re-decide case/separator behavior that F014 already guarantees, so authoring differences never change a route.
- **FR-004**: When exactly one declared glob matches a candidate path, the routing result MUST route that path to the matched glob's capability domain and record the matched glob.
- **FR-005**: When more than one declared glob matches a candidate path, the routing surface MUST select exactly one winner by a **total, deterministic precedence order**: (1) an exact-literal glob (no wildcards) that equals the path beats any wildcard glob; (2) otherwise the glob with the greater literal-segment specificity (more matched literal, non-wildcard segments) wins; (3) otherwise a single-segment `*` is more specific than a cross-segment `**` at the same position; (4) a final lexicographic ordering of the normalized glob string breaks any remaining tie. A path that matches at least one glob MUST NEVER be left unrouted.
- **FR-006**: When two matching globs are equal under rules (1)–(3) of FR-005 and are only separated by the lexicographic tiebreaker (4), the routing surface MUST emit a deterministic `AmbiguousRoute` diagnostic naming the path and the competing globs and domains, **and** MUST still resolve the path to the lexicographically-first competing glob so downstream consumers remain total.
- **FR-007**: When no declared glob matches a candidate path that lies within the governed root, the routing surface MUST report it as in-root unmatched (carrying that it is within the governed root) WITHOUT deciding any finding, severity, or blocking behavior. Finding semantics for unmatched in-root paths are deferred (FR-016).
- **FR-008**: When a candidate path lies outside the declared governed root, the routing surface MUST report it as out-of-scope — neither routed to a capability nor reported as in-root unmatched — and MUST NOT emit an ambiguity diagnostic for it.
- **FR-009**: If two path-map entries normalize to the same glob string but bind different capability domains, the routing surface MUST emit a deterministic conflict diagnostic identifying both domains, rather than resolving the conflict by authored source order.
- **FR-010**: A path-map glob containing a construct outside the supported MVP syntax (FR-002) MUST produce a deterministic configuration diagnostic naming the glob and the unsupported construct, never a silent never-match.
- **FR-011**: Routing MUST be a pure computation with no I/O: it MUST NOT read the filesystem, run git, sense which files changed, or read a clock. The candidate path set is always supplied by the caller; producing that set from git/CI is a later feature (FR-016).
- **FR-012**: Every emitted collection — the per-path routing results and the competing globs/domains within a diagnostic — MUST be in a deterministic, documented order, so identical inputs yield byte-identical output (and re-ordering the authored path map changes nothing).
- **FR-013**: Each routing result MUST be explainable: a routed path MUST expose the matched glob, the capability domain, and the precedence reason it won; a diagnostic MUST carry a stable id, the path and/or glob involved, and a fix hint, consistent with the F014 diagnostic style. No raw YAML text and no product-specific vocabulary beyond the declared capability domains may appear in the result.
- **FR-014**: The routing surface MUST assume the typed facts it is handed are already valid per F014 (domains referenced by surviving path-map entries are declared; paths are already normalized). It MUST NOT re-validate the capability catalog or re-run F014's diagnostics.
- **FR-015**: The routing result MUST be a structured value sufficient for a later route report and gate selector to consume directly (per-path domain or unmatched/out-of-scope status, matched glob, precedence reason, diagnostics) WITHOUT this feature emitting route/audit JSON or providing any CLI command.
- **FR-016**: Out of scope, held firm: git/CI changed-path sensing; the *finding* severity and governed-root/protected-boundary semantics for unmatched paths; surface-class assignment (routine / governed-root / protected / generated-view / release) per path; the typed gate registry; profile/mode enforcement; and the `route` and `ship` commands and their JSON. This feature stops at the pure path → capability routing result with deterministic precedence.

### Key Entities *(include if feature involves data)*

- **Candidate path**: A repository-relative path, supplied by the caller in the F014-normalized governed-path form, that is to be routed. The set of candidate paths is an input, not something this feature discovers.
- **Path-map glob**: A normalized glob from F014's path map, bound to one capability domain, expressed in the closed MVP syntax (literal, `?`, `*`, `**`).
- **Routing result (per path)**: The outcome for one candidate path — one of *routed* (capability domain + matched glob + precedence reason), *in-root unmatched*, or *out-of-scope* — plus any diagnostics attached to that path.
- **Precedence reason**: The explanation of why a particular glob won when several matched (exact-literal, greater literal specificity, single-segment over cross-segment, or lexicographic tiebreak).
- **Routing diagnostic**: A stable-id finding raised during routing — at minimum `AmbiguousRoute` (equally-specific competitors), a same-glob/different-domain conflict, and an unsupported-glob-syntax diagnostic — each with the path and/or glob involved and a fix hint.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For every candidate path that matches at least one declared glob, routing returns **exactly one** capability domain — never zero, never two — across the full fixture battery.
- **SC-002**: Routing the same typed facts and the same candidate-path set twice produces byte-for-byte identical results, including ordering of per-path results and diagnostics.
- **SC-003**: Permuting the authored order of the path-map entries leaves every path's routed domain, matched glob, precedence reason, and diagnostics unchanged (verified by property-based permutation).
- **SC-004**: When two equally-specific globs match one path, the outcome is the same single domain on every run **and** an `AmbiguousRoute` diagnostic is reported — there is no silent arbitrary pick.
- **SC-005**: Every routing decision can name the exact glob and precedence reason that produced it, and the routing result contains no raw YAML and no product-specific vocabulary beyond the declared capability domains.
- **SC-006**: Each supported glob construct (literal, `?`, `*`, `**`) has at least one accepting fixture, and each rung of the FR-005 precedence ladder (exact-literal, literal specificity, single- vs cross-segment, lexicographic tiebreak) has at least one fixture demonstrating its effect.

## Assumptions

- This feature consumes the F014 typed capability facts (`FS.GG.Governance.Config`); it does not re-parse the `.fsgg` files or duplicate F014's schema validation.
- Candidate paths are supplied already normalized to the F014 `GovernedPath` form. The git/CI sensing that *produces* a real changed-path set is a separate, later Phase-2 feature.
- The supported glob vocabulary is the closed MVP set in FR-002. Richer constructs (brace expansion, character classes, negation) are deferred and, if they appear in a path map, are diagnosed (FR-010).
- A project declares a single governed root (F014 `ProjectFacts.GovernedRoot`); multi-root scoping is out of scope for this version.
- The routing logic lives in the product-neutral Governance configuration/routing library layer; the kernel never sees YAML, globs, or product vocabulary (constitution operating rule and the F014 layering assumption).
- The precedence order in FR-005 is the agreed model; the plan may refine the precise specificity metric but MUST preserve totality, determinism, exact-literal-wins, narrower-beats-broader, and a final lexicographic tiebreak.
