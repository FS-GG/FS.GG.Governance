// Curated public signature contract for the routing-domain types of path-to-capability
// routing (F015).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution
// Principle II). The matching Model.fs carries NO `private`/`internal`/`public` modifiers
// on top-level bindings — visibility is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any
// Model.fs body exists (Principle I). These are the product-neutral, YAML-free values that
// `Routing.route` returns: the per-path outcome, why a glob won, and the routing-time
// diagnostics. They reuse the F014 typed-fact model (`GovernedPath`, `DomainId`) rather than
// redefining it — Routing consumes the typed facts, it does not re-parse YAML (FR-003,
// FR-014). Every emitted collection is in deterministic order (FR-012, SC-002).

namespace FS.GG.Governance.Routing

open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    // ── Why a glob won (explainability, FR-013, SC-005) ──

    /// The reason a particular glob was selected for a path when one or more matched (D4).
    /// Maps one-to-one onto the precedence rungs of FR-005:
    ///   • `OnlyMatch`             — exactly one glob matched the path.
    ///   • `ExactLiteral`          — a wildcard-free glob equal to the path beat the field (rung 1).
    ///   • `MoreSpecific`          — won on literal-segment count or `**` count specificity
    ///                               (FR-005 rungs 2–3).
    ///   • `LexicographicTiebreak` — co-specific competitors separated only by the final ordinal
    ///                               tiebreak (rung 4); ALWAYS paired with an `AmbiguousRoute`
    ///                               diagnostic for the same path (FR-006).
    type PrecedenceReason =
        | OnlyMatch
        | ExactLiteral
        | MoreSpecific
        | LexicographicTiebreak

    // ── The per-path outcome (FR-004, FR-007, FR-008) ──

    /// The outcome of routing one candidate path:
    ///   • `Routed`          — the path matched ≥1 glob; carries the winning capability domain,
    ///                         the winning (normalized) glob, and the reason it won (FR-004/FR-005).
    ///   • `UnmatchedInRoot` — the path is within the governed root but matched no glob; carries
    ///                         no domain and asserts no finding/severity (deferred, FR-007/FR-016).
    ///   • `OutOfScope`      — the path is not under the declared governed root; never routed and
    ///                         never an ambiguity (FR-008).
    type RoutingResult =
        | Routed of domain: DomainId * matchedGlob: GovernedPath * reason: PrecedenceReason
        | UnmatchedInRoot
        | OutOfScope

    /// One candidate path paired with its outcome. The aggregate `RouteReport` holds these
    /// sorted by normalized path (ordinal), so re-ordering the input never changes the report
    /// (FR-012, SC-002).
    type PathRouting =
        { Path: GovernedPath
          Result: RoutingResult }

    // ── Routing diagnostics (FR-006, FR-009, FR-010, FR-013) ──

    /// The CLOSED set of stable routing diagnostic ids — one per failure class the spec names
    /// (D6). Closed so tests assert exactly one fixture per id:
    ///   • `AmbiguousRoute`         — two equally-specific globs matched one path; the route is
    ///                                resolved to the ordinal-first glob but the ambiguity is
    ///                                reported, never a silent pick (FR-006).
    ///   • `ConflictingGlobBinding` — two path-map entries normalize to the same glob string but
    ///                                bind different capability domains (FR-009).
    ///   • `UnsupportedGlobSyntax`  — a glob contains a reserved-but-unimplemented construct
    ///                                (`[ ] { } ! ( )`), diagnosed rather than silently never
    ///                                matched (FR-010).
    type RoutingDiagnosticId =
        | AmbiguousRoute
        | ConflictingGlobBinding
        | UnsupportedGlobSyntax

    /// A stable-id, located, explained routing finding (FR-013). `Path` is the candidate path the
    /// finding concerns (None for catalog-shape findings like `ConflictingGlobBinding`/
    /// `UnsupportedGlobSyntax` that are not about a particular candidate); `Globs` lists the
    /// glob(s) involved (the competitors for `AmbiguousRoute`, the conflicting pair for
    /// `ConflictingGlobBinding`, the offending glob for `UnsupportedGlobSyntax`). `Message`
    /// carries a fix hint. No raw YAML and no product vocabulary beyond declared domains (SC-005).
    type RoutingDiagnostic =
        { Id: RoutingDiagnosticId
          Path: GovernedPath option
          Globs: GovernedPath list
          Message: string }

    // ── The aggregate result (D5) ──

    /// The deterministic result of routing a candidate-path set against the typed facts:
    /// one `PathRouting` per input path (sorted by normalized path, ordinal) plus the routing
    /// diagnostics (sorted by id, then path, then glob). Byte-for-byte identical for identical
    /// input (FR-012, SC-002); unchanged under re-ordering of the authored path map (SC-003).
    type RouteReport =
        { Routings: PathRouting list
          Diagnostics: RoutingDiagnostic list }

    // ── Stable rendering of a diagnostic id (for messages, tests, and any later JSON) ──

    /// The stable wire token for a `RoutingDiagnosticId`
    /// (e.g. `AmbiguousRoute` → `"ambiguousRoute"`). Deterministic and total.
    val routingDiagnosticIdToken: id: RoutingDiagnosticId -> string
