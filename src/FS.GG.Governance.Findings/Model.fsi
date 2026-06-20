// Curated public signature contract for the finding-domain types of unknown-governed-path
// findings (F017).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution
// Principle II). The matching Model.fs carries NO `private`/`internal`/`public` modifiers on
// top-level bindings — visibility is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any
// Model.fs body exists (Principle I). These are the product-neutral, YAML-free values the
// `Findings.findUnknownGovernedPaths` classifier returns: one typed finding per unclassified
// path inside a governed/protected region. They REUSE the F014 typed-fact newtypes
// (`GovernedPath`, `SurfaceId`) rather than redefining them — this feature consumes the typed
// facts and the F015 routing outcomes, it re-parses no YAML and re-routes no globs (FR-011,
// FR-014). Every emitted collection is in deterministic order (FR-009, SC-004). No field
// carries raw YAML, host paths, timestamps, or product vocabulary beyond declared ids
// (FR-008, SC-006).

namespace FS.GG.Governance.Findings

open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    // ── The CLOSED set of stable finding ids (FR-006, FR-008) ──

    /// The stable diagnostic id of an unknown-governed-path finding. Closed so tests assert
    /// exactly one fixture per id and a later gate can switch on it without re-deriving the zone:
    ///   • `UnknownGovernedPath`          — an ordinary in-root unknown: a path inside the
    ///                                       declared governed root that no capability glob
    ///                                       classified and no declared `Routine` surface covers.
    ///   • `UnknownProtectedBoundaryPath` — the escalated flavor: the same kind of unclassified
    ///                                       path, but landing on a declared `ProtectedSurface`
    ///                                       boundary (FR-006). Distinguishable by BOTH this id
    ///                                       and the `FindingZone`.
    type FindingId =
        | UnknownGovernedPath
        | UnknownProtectedBoundaryPath

    // ── Which managed region triggered the finding (FR-006, key entity "Finding zone") ──

    /// The region that triggered the finding, so a later gate treats the two differently without
    /// re-deriving the zone:
    ///   • `GovernedRootUnknown`            — an ordinary governed-root unknown (no declared
    ///                                        surface escalated it).
    ///   • `ProtectedBoundaryUnknown sid`   — an unknown on a declared protected boundary; carries
    ///                                        the escalating `ProtectedSurface`'s declared id
    ///                                        (FR-006). When more than one protected surface covers
    ///                                        the path, `sid` is the ordinal-first `SurfaceId`
    ///                                        (documented tiebreak, contracts/precedence.md).
    type FindingZone =
        | GovernedRootUnknown
        | ProtectedBoundaryUnknown of surface: SurfaceId

    // ── The finding (key entity "Unknown-governed-path finding", FR-001/FR-008) ──

    /// The typed result for ONE unclassified path inside a governed/protected region: a stable
    /// `Id`, the offending normalized `Path`, the `Zone` that triggered it (carrying the protected
    /// surface's identity where applicable), and an explained `Message` carrying at least one
    /// concrete fix hint (declare a path-map glob, mark the region routine, or classify the
    /// surface). A pure, deterministic value — no raw YAML, host paths, or product vocabulary
    /// beyond the declared ids (FR-008, SC-006).
    type UnknownGovernedPathFinding =
        { Id: FindingId
          Path: GovernedPath
          Zone: FindingZone
          Message: string }

    // ── The aggregate result (FR-001, FR-012) ──

    /// The deterministic finding set: one `UnknownGovernedPathFinding` per non-suppressed
    /// unclassified in-root path, sorted by normalized path (ordinal) then finding-id token, so
    /// identical inputs yield a byte-identical list and re-ordering the inputs never changes it
    /// (FR-009, SC-004). An EMPTY list is a valid, successful outcome — never an error and never a
    /// fabricated "all clear" finding (FR-012, edge case "Empty input").
    type FindingReport =
        { Findings: UnknownGovernedPathFinding list }

    // ── Stable rendering of a finding id (for messages, tests, and any later JSON) ──

    /// The stable wire token for a `FindingId`
    /// (e.g. `UnknownGovernedPath` → `"unknownGovernedPath"`). Deterministic and total.
    val findingIdToken: id: FindingId -> string
