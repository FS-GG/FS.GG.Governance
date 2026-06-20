# Phase 0 Research: Path-to-Capability Routing with Deterministic Glob Precedence

All Technical-Context unknowns are resolved below. Each entry records the decision, why it was
chosen, and the alternatives rejected.

## D1 — Where routing lives (project home)

**Decision**: A new optional class library `FS.GG.Governance.Routing`, sibling to
Kernel/Host/adapters/Config, referencing only `FS.GG.Governance.Config` for the shared typed-fact
model and adding **no** third-party dependency.

**Rationale**: Routing is the first *consumer* of the F014 typed facts — the `Model.fsi` header of
Config literally names "routing" among the later features that consume `TypedFacts`. It is a distinct
concern from F014's "YAML → typed facts": it needs none of YamlDotNet, none of F014's strictness
machinery, and produces a different kind of value (a per-path route report). A dedicated library keeps
that separation clean, keeps the dependency direction one-way (Routing → Config → YamlDotNet), and lets
later Phase-2 features (the unknown-path-findings classifier, the gate registry, the `route` command)
reference routing without dragging in YAML-parsing concerns. The kernel stays out entirely: globs,
paths, and capability vocabulary are config-domain, and the kernel receives only typed facts.

**Alternatives rejected**:
- *Add a `Routing` module inside `FS.GG.Governance.Config`* — would mix "turn YAML into facts" with
  "use the facts," and would make every Config consumer carry the routing surface. Config's stated job
  (research D1 of F014) is the typed facts; routing is a separate, separately-packable use of them.
- *Add to the Kernel* — forbidden by the constitution (kernel is BCL-only and product-neutral;
  config/route vocabulary never enters it). Routing over `.fsgg`-declared globs is config-domain.
- *Add to Host* — Host is the sense/plan/act loop, not a place for a pure routing primitive that other
  libraries (gate registry, CLI) need to reference directly.

## D2 — Glob matching algorithm

**Decision**: A hand-written, BCL-only **segment matcher** over the closed MVP syntax. Split both the
glob and the candidate path on `/` into segments; match segment-by-segment, where a `**` segment
matches zero or more whole segments (a `let rec` backtracking walk) and within a single segment `*`
matches zero or more characters and `?` matches exactly one character. No regex translation, no
external glob/filesystem library.

**Rationale**: The supported vocabulary is small and closed (FR-002): literal, `?`, `*` (single
segment), `**` (cross segment). A direct segment matcher is the plainest correct implementation
(Principle III), is trivially deterministic, and keeps the library dependency-free. Operating on the
already-normalized `GovernedPath` form (research D8) means there is no separator ambiguity to handle —
both sides use `/`.

**Alternatives rejected**:
- *`System.IO.Enumeration.FileSystemName.MatchesSimpleExpression`* — its wildcard semantics are
  filesystem-oriented and do not give the `**` zero-or-more-segments semantics we need; tying routing
  to filesystem-name matching also blurs the "pure, no-I/O" contract (FR-011).
- *`Microsoft.Extensions.FileSystemGlobbing`* — a new third-party dependency, filesystem-bound (it
  globs against a directory), and not a pure string function; contradicts the no-new-dependency posture
  and FR-011.
- *Translate each glob to a `System.Text.RegularExpressions.Regex`* — getting `**` (zero-or-more
  segments, including the boundary `/`) correct in regex is subtle and opaque, the compiled regex is
  harder to reason about for specificity, and it adds no value over a direct matcher for four
  constructs.

**`**` zero-or-more semantics (the spec edge case)**: `src/**` matches both the deep `src/a/b.fs` and
the shallow `src/a.fs`, and also `src` itself is *not* matched by `src/**` (the `**` requires the
`src/` prefix). This zero-or-more reading is stated in the glob-precedence contract and applied
consistently by the matcher.

## D3 — Specificity metric and the total precedence order

**Decision**: Order matching globs by a **specificity key** computed from the glob alone (never from
the candidate path, so the order is stable across the paths it matches). For a match against a path,
the winner is the glob with the smallest key under this ascending tuple:

The `specificity` key — used for the co-specific/ambiguity test — is the ascending tuple of rungs
(1)–(3), computed from the glob alone:

1. **Exact-literal match**: `0` if the glob has no wildcards, else `1`. (Among globs that matched
   one path, a wildcard-free glob equals that path, so it beats every wildcard glob — FR-005 rung 1.)
2. **Literal-segment count, negated**: more segments that contain no `*`/`?`/`**` rank earlier.
   (`src/Adapters/**` with literal `{src, Adapters}` beats `src/**` with literal `{src}` — rung 2.)
3. **Count of `**` segments**: fewer cross-segment wildcards rank earlier.
   (`src/*` beats `src/**` when both match `src/a` — single-segment `*` is more specific than `**`,
   rung 3.)

`compare` orders by this `specificity` key and then breaks any remaining tie by the **ordinal glob
string** (FR-005 rung 4) — the final, total tiebreak that yields a single deterministic winner even
for genuinely co-specific globs. Literal-character length is deliberately **not** a specificity rung:
an earlier draft made it rung 4, which both contradicted the worked ambiguity example and made
`AmbiguousRoute` nearly impossible to trigger. The 3-rung key matches spec FR-005 verbatim and keeps
the ambiguity boundary natural.

**Rationale**: This is the literal deliverable of the implementation-plan row. The key is *total* and
*deterministic*: every pair of globs is comparable, the comparison depends only on the glob strings
(not on iteration order or the path), and the final ordinal tiebreak (rung 4) removes any residual
ambiguity, so a path matching ≥1 glob is never unrouted (FR-005) and the winner is identical across
runs and across
re-ordering of the path map (SC-002/SC-003). Computing the key from the glob alone (rather than from
how it matched a particular path) keeps a glob's rank constant across all paths, which is what makes
the report stable.

**Ambiguity boundary (FR-006)**: two globs that tie on the `specificity` key (rungs 1–3) and are
separated only by the ordinal tiebreak (rung 4) are *genuinely ambiguous*. Routing emits an
`AmbiguousRoute` diagnostic naming the path and both competing globs/domains, **and** resolves to the
ordinal-first glob so downstream stays total. The tie is detected by comparing the `specificity` of
the top two competitors (`Glob.isAmbiguousPair`), not by a separate pass.

**Alternatives rejected**:
- *First-match-wins by authored order* — makes routing order-sensitive, violating FR-012/SC-003; the
  whole point is a precedence independent of how the path map was written.
- *Longest-glob-string-wins* — a crude proxy that mis-ranks (`src/**/x.fs` is longer than
  `src/Kernel/*` yet less specific); the segment/literal decomposition matches author intuition and the
  FR-005 rungs directly.
- *Most-characters-matched-against-the-path-wins* — makes a glob's rank depend on the path, so the same
  two globs could order differently for different paths; harder to explain and to test, and unnecessary
  for the MVP.

## D4 — Explaining the winner (precedence reason)

**Decision**: Record, on each routed path, a closed `PrecedenceReason`:
`OnlyMatch` (exactly one glob matched), `ExactLiteral` (won on key 1), `MoreSpecific` (won on keys
2–4), or `LexicographicTiebreak` (separated only by key 5 — always paired with an `AmbiguousRoute`
diagnostic).

**Rationale**: FR-013 and SC-005 require every routing decision to name the glob and the *reason* it
won, so a later route report can explain a route to a human without re-deriving it. A closed reason DU
is assertable in tests (one fixture per reason) and maps one-to-one to the precedence rungs of D3.

**Alternatives rejected**:
- *Expose the raw specificity key* — leaks an internal numeric tuple into the public contract and is
  not self-explaining; the named reason is the stable, human-facing fact.
- *No reason field* — fails the explainability requirement (FR-013/SC-005) and makes the ambiguity case
  indistinguishable from a clean most-specific win.

## D5 — Result model and ordering

**Decision**: The per-path outcome is a closed DU `RoutingResult`:
`Routed of domain: DomainId * matchedGlob: GovernedPath * reason: PrecedenceReason`
| `UnmatchedInRoot` | `OutOfScope`. The aggregate `RouteReport` carries the per-path entries
(`{ Path: GovernedPath; Result: RoutingResult }`) sorted by normalized path (ordinal) and a separately
sorted `Diagnostics: RoutingDiagnostic list`. Identical input ⇒ byte-identical report.

**Rationale**: Three outcomes are exactly what FR-004/FR-007/FR-008 require — routed, in-root but
matched nothing, or outside the governed root. Sorting the per-path entries and the diagnostics by a
stable ordinal key (mirroring Config research D8) guarantees FR-012/SC-002 and makes the FsCheck
permutation property (SC-003) hold. Keeping `UnmatchedInRoot` distinct from `OutOfScope` carries
exactly the information the later unknown-governed-path-findings feature needs without this feature
deciding any severity (FR-007, FR-016).

**Alternatives rejected**:
- *Collapse `UnmatchedInRoot` and `OutOfScope` into one "no domain" case* — loses the governed-root
  distinction the next feature depends on (FR-007/FR-008) and would force it to re-derive scoping.
- *Return only a `Map<path, domain>`* — drops the matched glob, the reason, the unmatched/out-of-scope
  distinction, and the diagnostics — i.e. the explainability the spec requires.

## D6 — Routing diagnostics taxonomy

**Decision**: A closed set of stable routing diagnostic ids, one per failure class the spec names:
`AmbiguousRoute` (FR-006 — equally-specific competitors for one path), `ConflictingGlobBinding`
(FR-009 — two path-map entries normalize to the same glob string but bind different domains), and
`UnsupportedGlobSyntax` (FR-010 — a glob contains a reserved-but-unimplemented construct). Each
`RoutingDiagnostic` carries `Id`, the `Path` and/or `Glob` involved, and a human-readable `Message`
with a fix hint. Diagnostics are emitted in deterministic order (by id, then path, then glob).

For `UnsupportedGlobSyntax`, the reserved-but-unimplemented characters are `[`, `]`, `{`, `}`, `!`,
`(`, `)` — constructs of richer glob dialects (character classes, brace expansion, negation, groups)
that the MVP does not implement. A glob containing any of them is diagnosed rather than treated as a
literal that silently never matches.

**Rationale**: FR-006/FR-009/FR-010 each name a distinct, separately-testable failure; a closed id set
lets tests assert exactly one fixture per id and mirrors Config's diagnostic-id discipline (F014
research D7). Detecting reserved characters up front honors FR-010's "never a silent never-match":
without it, `docs/[a-z]/x` would be matched as the literal segment `[a-z]` and quietly match nothing.

**Alternatives rejected**:
- *Treat unsupported constructs as literal characters* — the silent never-match FR-010 forbids; the
  author gets no signal their richer glob is unsupported.
- *Free-text error strings* — not stable or assertable; violates FR-013.
- *Reuse Config's `Diagnostic`/`DiagnosticId`* — those ids are about YAML validity (unknown field,
  duplicate id, schema version); routing failures are a different, post-validation class and deserve
  their own closed id set rather than overloading F014's.

## D7 — MVU/I-O boundary placement (Constitution Principle IV)

**Decision**: Keep the whole routing surface a **pure total function**. `Glob.matches`,
`Glob.specificityKey`, `Glob.checkSyntax`, and `Routing.route` perform no I/O, hold no state, never
throw, and need no Elmish `Model`/`Msg`/`update`.

**Rationale**: Principle IV requires the MVU boundary only for stateful or I/O-bearing workflows;
routing has neither — no filesystem, no process, no clock, no retries, no convergence loop (FR-011).
The candidate path set and the typed facts are supplied as inputs and the route report is returned as a
value, exactly the "simple pure functions … do not need Elmish ceremony" allowance the constitution
grants and that Config's pure `Schema.validate` already uses. Adding an MVU shell here would be the
ceremony Principle III discourages.

**Alternatives rejected**:
- *Wrap routing in a `Model`/`Msg`/`update`* — pure ceremony for a stateless function; rejected for
  the same reason Config's research D3 rejected a full Elmish `Program`.
- *Have `route` read the changed paths from git itself* — would make routing I/O-bound and non-pure and
  would absorb the later git/CI sensing feature; FR-011/FR-016 hold that out.

## D8 — Candidate-path input form (no normalization in Routing)

**Decision**: Routing consumes candidate paths as already-normalized `GovernedPath` values (the F014
form), and consumes the path-map globs and governed root straight from the F014 `TypedFacts`. Routing
performs **no** path normalization and does **no** re-validation of the capability catalog.

**Rationale**: FR-003 requires matching to run over the F014-normalized form and forbids Routing from
re-deciding separator/case behavior F014 already settled; FR-014 forbids re-validating the catalog; and
the spec's Assumptions state candidate paths arrive pre-normalized. Reusing F014's `GovernedPath` for
both the globs (already normalized inside the facts) and the candidate paths makes matching a
well-defined segment comparison with no ambiguity, and keeps a single source of truth for normalization
(Config). Producing a *real* changed-path set — and normalizing it into `GovernedPath` — belongs to the
later git/CI sensing feature (FR-016); for this feature's tests, fixtures construct `GovernedPath`
values directly, which is exactly how a downstream caller will hand them in.

**Governed-root scoping**: a candidate path is *in root* when it equals or is a segment-prefixed
descendant of `ProjectFacts.GovernedRoot` (both normalized). In-root paths that match no glob are
`UnmatchedInRoot`; paths not under the governed root are `OutOfScope` (FR-007/FR-008). This is a pure
segment-prefix check, no I/O.

**Alternatives rejected**:
- *Accept raw `string` paths plus the root and normalize inside Routing* — would duplicate F014's
  normalizer (drift risk) or require exposing it from Config now; FR-003/FR-014 and the spec assumption
  make pre-normalized input the contract, and the normalizer-exposure decision belongs to the feature
  that actually senses raw paths.
- *Expose Config's normalizer as part of this feature* — out of scope; deferred to the git/CI sensing
  feature that needs to turn real, raw changed paths into `GovernedPath`s.
