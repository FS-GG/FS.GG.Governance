// The shared F057 currency/decision/manifest vocabulary. Visibility lives in RefreshModel.fsi
// (Principle II) — this file carries NO top-level access modifiers. These product-neutral closed DUs /
// records are the values the whole F057 row composes: `Declaration.parse` produces a `GenerationManifest`,
// the pure `Loop` decides per-view `CurrencyStatus` and assembles a `RefreshDecision`, and
// `RefreshJson.ofRefreshDecision` projects it. No product, view, path, generator, or renderer identity is
// named here (FR-011). `viewKindToken` is the only behavior — a total, exhaustive token renderer.

namespace FS.GG.Governance.RefreshJson

open FS.GG.Governance.FreshnessKey.Model    // InputCategory
open FS.GG.Governance.Config.Model          // Maturity (F070 additive currency-enforcement dial)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RefreshModel =

    type ViewKind =
        | GateMetadata
        | RuleCatalog
        | CapabilityDoc
        | SkillReference
        | ApiSurfaceDoc
        | RouteProjection
        | Baseline
        | Other of string

    type GenerationEntry =
        { ViewId: string
          Kind: ViewKind
          OutputPath: string
          Sources: string list
          Generator: string list
          GeneratorBasis: string }

    type GenerationManifest =
        { Entries: GenerationEntry list
          CurrencyEnforcement: Maturity option }

    type DeclError = { Reason: string }

    type CurrencyStatus =
        | Current
        | Regenerated of drifted: InputCategory list
        | WouldRegenerate of drifted: InputCategory list
        | StaleUnresolved of reason: string
        | NotEvaluated

    type ViewDecision =
        { Entry: GenerationEntry
          Status: CurrencyStatus
          Drifted: InputCategory list }

    type RefreshOutcome =
        | NothingToRefresh
        | ViewsRegenerated
        | StaleUnresolved'
        | UsageError'
        | InputUnavailable
        | ToolError

    type RefreshDecision =
        { Outcome: RefreshOutcome
          DryRun: bool
          Views: ViewDecision list
          RegeneratedCount: int
          CurrentCount: int
          UnresolvedCount: int
          NotEvaluatedCount: int }

    // EXHAUSTIVE with NO wildcard — a future kind is a compile error here, never a silently mis-tokened
    // field. `Other s` renders `s` verbatim (product-neutral, FR-011).
    let viewKindToken (kind: ViewKind) : string =
        match kind with
        | GateMetadata -> "gate-metadata"
        | RuleCatalog -> "rule-catalog"
        | CapabilityDoc -> "capability-doc"
        | SkillReference -> "skill-reference"
        | ApiSurfaceDoc -> "api-surface-doc"
        | RouteProjection -> "route-projection"
        | Baseline -> "baseline"
        | Other s -> s

    // Normalize a token for comparison: drop separators, lowercase. Lets a declaration/selector write
    // `gate-metadata`, `gateMetadata`, or `gate_metadata` interchangeably (the value is a STRUCTURAL kind,
    // not product identity — FR-011). An unrecognized token is carried as `Other` with the RAW value.
    let viewKindOfToken (raw: string) : ViewKind =
        match raw.Replace("-", "").Replace("_", "").ToLowerInvariant() with
        | "gatemetadata" -> GateMetadata
        | "rulecatalog" -> RuleCatalog
        | "capabilitydoc" -> CapabilityDoc
        | "skillreference" -> SkillReference
        | "apisurfacedoc" -> ApiSurfaceDoc
        | "routeprojection" -> RouteProjection
        | "baseline" -> Baseline
        | _ -> Other raw
