// Curated public signature contract for the shared F057 currency/decision/manifest vocabulary.
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching RefreshModel.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings —
// visibility is presence/absence here.
//
// These are the product-neutral, closed-DU / record values the whole F057 row is built from. They live in
// the leaf `RefreshJson` project so the `RefreshCommand` executable (`Declaration`/`Loop`/`Interpreter`)
// consumes them by referencing this project, keeping the row to two `src` projects with a clean dependency
// direction (RefreshCommand -> RefreshJson). `RefreshJson.ofRefreshDecision` projects a `RefreshDecision`;
// `Declaration.parse` produces a `GenerationManifest`/`DeclError`; the pure `Loop` decides per-view
// currency over them. Every kind/identity/path is STRUCTURAL — no product, view, path, generator, or
// renderer identity is named here (FR-011). The drifted-category vocabulary REUSES the F029
// `FreshnessKey.Model.InputCategory` verbatim.

namespace FS.GG.Governance.RefreshJson

open FS.GG.Governance.FreshnessKey.Model    // InputCategory
open FS.GG.Governance.Config.Model          // Maturity (F070 additive currency-enforcement dial)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RefreshModel =

    /// The closed set of declared view KINDS — structural, product-neutral (FR-011). The named cases are
    /// structural categories a repository may declare, NOT product identities; `Other of string` keeps the
    /// surface open without naming products. The kind is descriptive metadata projected into `refresh.json`;
    /// it never selects a hardcoded renderer (regeneration runs the entry's DECLARED generator command).
    type ViewKind =
        | GateMetadata
        | RuleCatalog
        | CapabilityDoc
        | SkillReference
        | ApiSurfaceDoc
        | RouteProjection
        | Baseline
        | Other of string

    /// One declared generated view and the relationship to its sources (the manifest's "view" entry). Every
    /// field is read from `.fsgg/refresh.yml`; the adapter hardcodes none (FR-011). `Sources` is in declared
    /// order (the source-digest set's input order, FR-002); `Generator` is the declared command argv run at
    /// the edge to regenerate the view (research D3); `GeneratorBasis` is how the generator version is sensed
    /// (a product-neutral token read from the file).
    type GenerationEntry =
        { ViewId: string
          Kind: ViewKind
          OutputPath: string
          Sources: string list
          Generator: string list
          GeneratorBasis: string }

    /// The whole authored generation manifest. An EMPTY entry list is VALID ("nothing to refresh", FR-012).
    /// `CurrencyEnforcement` is the F070 ADDITIVE opt-in stale-view blocking dial (the manifest-level
    /// `currency-enforcement:` key): `None` (default) keeps stale-view findings advisory and every existing
    /// artifact byte-identical; `Some maturity` lets a stale generated view fold into the verify/ship verdict
    /// through the existing F023 truth table. It is NOT projected into `refresh.json` (the projection renders
    /// only the fields it already renders), so `refresh.json` stays byte-identical.
    type GenerationManifest =
        { Entries: GenerationEntry list
          CurrencyEnforcement: Maturity option }

    /// A closed, explained rejection of a malformed/unreadable `refresh.yml` (the `ReleaseCommand.DeclError`
    /// spirit). Parsing is PURE and TOTAL — malformed input is an `Error DeclError`, never an exception.
    type DeclError = { Reason: string }

    /// The per-view currency outcome. `StaleUnresolved` carries WHY (FR-016) and is NEVER coerced to
    /// `Current` (FR-010); `Regenerated`/`WouldRegenerate` are mutually exclusive by run mode (write vs
    /// `--dry-run`) and carry the drifted `InputCategory` list that drove staleness.
    type CurrencyStatus =
        | Current
        | Regenerated of drifted: InputCategory list
        | WouldRegenerate of drifted: InputCategory list
        | StaleUnresolved of reason: string
        | NotEvaluated

    /// One view's decision: the declared entry, its resolved status, and the drifted categories (empty when
    /// current / not-evaluated). `Drifted` mirrors the categories carried in `Regenerated`/`WouldRegenerate`
    /// and is the projection's `drifted` field.
    type ViewDecision =
        { Entry: GenerationEntry
          Status: CurrencyStatus
          Drifted: InputCategory list }

    /// The overall run category that drives the exit code (cli.md exit-code table, research D5). Six
    /// distinguishable outcomes; the trailing `'` on the colliding constructor names disambiguates them from
    /// the `CurrencyStatus` cases.
    type RefreshOutcome =
        | NothingToRefresh
        | ViewsRegenerated
        | StaleUnresolved'
        | UsageError'
        | InputUnavailable
        | ToolError

    /// The whole-run value the projection renders and the summary reports. `Views` is in declared manifest
    /// order (deterministic). `DryRun` distinguishes a preview run (statuses are `WouldRegenerate`) from a
    /// write run (statuses are `Regenerated`); it is the projection's top-level `dryRun` field.
    type RefreshDecision =
        { Outcome: RefreshOutcome
          DryRun: bool
          Views: ViewDecision list
          RegeneratedCount: int
          CurrentCount: int
          UnresolvedCount: int
          NotEvaluatedCount: int }

    /// The stable, kebab-case wire token for a `ViewKind` (for the manifest round-trip and the projection's
    /// `kind` field). TOTAL and exhaustive — a future kind is a compile error here. `Other s` renders `s`
    /// verbatim (product-neutral).
    val viewKindToken: kind: ViewKind -> string

    /// Recognize a declared/selector kind token into a `ViewKind`, kebab/camel/underscore tolerant (the
    /// release-rule recognizer precedent). An unrecognized token is carried as `Other` with the RAW value
    /// verbatim (product-neutral, FR-011) — so an unknown kind round-trips and a `--view-kind` selector can
    /// still match it. Shared by `Declaration.parse` (manifest kinds) and `Loop.parse` (the scope selector).
    val viewKindOfToken: raw: string -> ViewKind
