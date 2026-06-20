# Phase 1 Data Model: Path-to-Capability Routing (F015)

The routing-domain types the public contract realizes. They are product-neutral and YAML-free,
reuse the F014 typed-fact model for shared vocabulary, and are emitted in deterministic order
(FR-012, SC-002). Authoritative signatures live in [`contracts/Model.fsi`](./contracts/Model.fsi),
[`contracts/Glob.fsi`](./contracts/Glob.fsi), and [`contracts/Routing.fsi`](./contracts/Routing.fsi);
this file is the conceptual map.

## Reused F014 types (inputs — not redefined here)

From `FS.GG.Governance.Config.Model` (Routing references Config; FR-014, research D8):

| Type | Role in routing |
|---|---|
| `GovernedPath` | A normalized governed path. Used for candidate paths, path-map globs, the governed root, and the matched glob. Routing does NOT re-normalize it. |
| `DomainId` | A declared capability domain — the thing a path routes to. |
| `PathMapEntry` (`{ Glob: GovernedPath; Capability: DomainId }`) | One `glob → domain` binding from `CapabilityFacts.PathMap`. |
| `CapabilityFacts` (`Domains`, `PathMap`, …) | The path map and declared domains routing reads. |
| `ProjectFacts` (`GovernedRoot`, …) | The governed-root anchor for in-root/out-of-scope scoping. |
| `TypedFacts` (`Project`, `Capabilities`, …) | The aggregate handed to `Routing.route`. |

## Routing-domain types (outputs — defined by this feature)

### `PrecedenceReason` (DU, closed)

Why a glob won: `OnlyMatch` | `ExactLiteral` | `MoreSpecific` | `LexicographicTiebreak`.
One value per FR-005 rung (D4). `LexicographicTiebreak` always co-occurs with an
`AmbiguousRoute` diagnostic for the same path.

### `RoutingResult` (DU, closed)

The per-path outcome (FR-004/FR-007/FR-008):

- `Routed of domain: DomainId * matchedGlob: GovernedPath * reason: PrecedenceReason`
- `UnmatchedInRoot` — in the governed root, matched nothing; no severity decided (FR-016).
- `OutOfScope` — not under the governed root.

### `PathRouting` (record)

`{ Path: GovernedPath; Result: RoutingResult }` — one candidate path paired with its outcome.

### `RoutingDiagnosticId` (DU, closed)

`AmbiguousRoute` | `ConflictingGlobBinding` | `UnsupportedGlobSyntax` — one per failure class
(D6). `routingDiagnosticIdToken` gives each a stable wire token.

### `RoutingDiagnostic` (record)

`{ Id: RoutingDiagnosticId; Path: GovernedPath option; Globs: GovernedPath list; Message: string }`.
`Path` is `None` for catalog-shape findings (conflicting binding / unsupported syntax) that are
not about a particular candidate. `Globs` lists the glob(s) involved. `Message` carries a fix hint
(FR-013).

### `RouteReport` (record — the aggregate result)

`{ Routings: PathRouting list; Diagnostics: RoutingDiagnostic list }`.

- `Routings` sorted by normalized `Path` (ordinal).
- `Diagnostics` sorted by `(Id, Path, Globs)` (ordinal).
- Byte-identical for identical input; unchanged under path-map re-ordering (FR-012, SC-002/SC-003).

### `Glob.Specificity` (opaque, totally ordered)

The specificity key computed from a glob string alone (FR-005 rungs 1–3: wildcard-free flag,
literal-segment count, `**` count — literal-character length is NOT a rung). Custom equality:
two globs with equal keys are exactly the co-specific (ambiguous) pair. `Glob.compare` layers the
final ordinal-glob-string tiebreak (rung 4) on top to produce the total winner.

## Validation / invariants (enforced by `route`, not re-validated from F014)

| Invariant | Source | Enforcement |
|---|---|---|
| Candidate paths and globs are already normalized | FR-003, D8 | Assumed; routing does not re-normalize. |
| Referenced domains are declared | FR-014 | Assumed (F014 already rejected dangling refs). |
| A path matching ≥1 glob is routed to exactly one domain | FR-005, SC-001 | Total precedence + ordinal tiebreak. |
| Ambiguity is never a silent pick | FR-006, SC-004 | `AmbiguousRoute` emitted alongside the deterministic winner. |
| Same glob → different domains is reported | FR-009 | `ConflictingGlobBinding`; the glob is excluded from matching. |
| Unsupported glob syntax is reported, never silent never-match | FR-010 | `Glob.checkSyntax` → `UnsupportedGlobSyntax`; glob excluded. |
| Out-of-scope ≠ in-root-unmatched | FR-007/FR-008 | Segment-prefix governed-root test. |

## State transitions

None. Routing is a pure total function over inputs (FR-011, research D7); there is no model,
no message, no lifecycle.
