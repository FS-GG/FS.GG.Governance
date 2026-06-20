// Finding-domain types for unknown-governed-path findings (F017). The public surface is fixed
// by Model.fsi (Principle II); no top-level binding here carries an access modifier. These are
// product-neutral, YAML-free values that `Findings.findUnknownGovernedPaths` returns; they
// reuse the F014 typed-fact newtypes (`GovernedPath`, `SurfaceId`) rather than redefining them
// (FR-008, FR-014). Every emitted collection is in deterministic order (FR-009, SC-004).

namespace FS.GG.Governance.Findings

open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type FindingId =
        | UnknownGovernedPath
        | UnknownProtectedBoundaryPath

    type FindingZone =
        | GovernedRootUnknown
        | ProtectedBoundaryUnknown of surface: SurfaceId

    type UnknownGovernedPathFinding =
        { Id: FindingId
          Path: GovernedPath
          Zone: FindingZone
          Message: string }

    type FindingReport =
        { Findings: UnknownGovernedPathFinding list }

    // The stable wire token for each finding id (FR-008). Total: every case is named, so adding
    // a case is a compile error here rather than a silent fall-through.
    let findingIdToken (id: FindingId) : string =
        match id with
        | UnknownGovernedPath -> "unknownGovernedPath"
        | UnknownProtectedBoundaryPath -> "unknownProtectedBoundaryPath"
