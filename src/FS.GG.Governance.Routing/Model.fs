// Routing-domain types for path-to-capability routing (F015). The public surface is fixed
// by Model.fsi (Principle II); no top-level binding here carries an access modifier. These
// are product-neutral, YAML-free values that `Routing.route` returns; they reuse the F014
// typed-fact model (`GovernedPath`, `DomainId`) rather than redefining it (FR-003, FR-014).

namespace FS.GG.Governance.Routing

open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type PrecedenceReason =
        | OnlyMatch
        | ExactLiteral
        | MoreSpecific
        | LexicographicTiebreak

    type RoutingResult =
        | Routed of domain: DomainId * matchedGlob: GovernedPath * reason: PrecedenceReason
        | UnmatchedInRoot
        | OutOfScope

    type PathRouting =
        { Path: GovernedPath
          Result: RoutingResult }

    type RoutingDiagnosticId =
        | AmbiguousRoute
        | ConflictingGlobBinding
        | UnsupportedGlobSyntax

    type RoutingDiagnostic =
        { Id: RoutingDiagnosticId
          Path: GovernedPath option
          Globs: GovernedPath list
          Message: string }

    type RouteReport =
        { Routings: PathRouting list
          Diagnostics: RoutingDiagnostic list }

    // The stable wire token for each diagnostic id (FR-013). Total: every case is named, so
    // adding a case is a compile error here rather than a silent fall-through.
    let routingDiagnosticIdToken (id: RoutingDiagnosticId) : string =
        match id with
        | AmbiguousRoute -> "ambiguousRoute"
        | ConflictingGlobBinding -> "conflictingGlobBinding"
        | UnsupportedGlobSyntax -> "unsupportedGlobSyntax"
