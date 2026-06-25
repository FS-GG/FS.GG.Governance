// Product-surface classification types (F23). Visibility lives entirely in Model.fsi
// (Principle II): no top-level binding here carries an access modifier. These are plain records/DUs
// (Principle III) — the product-neutral, YAML-free values `ProductSurfaces.classify` returns. They reuse
// the F014 catalog vocabulary verbatim.

namespace FS.GG.Governance.ProductSurfaces

open FS.GG.Governance.Config.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type TierAlternative =
        | CheaperLocalTier of GeneratedProductTier
        | NoCheaperLocalTier

    type ClassificationReason =
        | OnlySurface
        | HighestPrecedenceKind
        | OrdinalSurfaceTiebreak

    type ProductClassification =
        { Path: GovernedPath
          Capability: DomainId
          Surface: SurfaceId
          Class: SurfaceClass
          SelectedTier: GeneratedProductTier
          TierIsDeclared: bool
          Alternative: TierAlternative
          Reason: ClassificationReason
          Explanation: string }

    type ProductSurfaceReport = { Classifications: ProductClassification list }

    // Total, deterministic: a new ClassificationReason case is a compile error here until it gets a token.
    let classificationReasonToken (reason: ClassificationReason) : string =
        match reason with
        | OnlySurface -> "onlySurface"
        | HighestPrecedenceKind -> "highestPrecedenceKind"
        | OrdinalSurfaceTiebreak -> "ordinalSurfaceTiebreak"
