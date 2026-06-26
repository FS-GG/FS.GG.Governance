// Curated public signature contract for the template-provider seam's value types (071).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II).
// The matching Model.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings —
// visibility is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Model.fs body
// exists (Principle I). These are the product-neutral, immutable values the generic seam delegates
// across: the provider identity + contract version, the bounded scaffold request, the provider's
// declarative emission (it DESCRIBES, never writes — research D1), the in-process provider port, the
// tool-owned pre-write safety refusals, and the deterministic provenance manifest. Every type is an
// immutable record / closed DU; every match downstream is exhaustive and wildcard-free so a new case
// is a compile error, never a silently mistyped field (data-model §1-6). The seam hardcodes NO
// provider name, package id, target name, toolchain, or layout (FR-003).

namespace FS.GG.Governance.Scaffold

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// Stable, provider-supplied identifier. The tool never interprets its content (FR-003) — it only
    /// records and reports it (FR-005, FR-012).
    type ProviderId = ProviderId of string

    /// The provider-declared contract version it was authored against. A simple (major, minor) pair;
    /// the tool supports a fixed inclusive range (research D1/D4).
    type ProviderContractVersion = { Major: int; Minor: int }

    /// The bounded scaffold request. `Target` is the operator-chosen project root the provider may
    /// populate; the provider returns paths RELATIVE to it and never sees or writes anything outside
    /// it. `ReservedPaths` are lifecycle-skeleton paths the host already owns (target-relative) so a
    /// provider can avoid them; the tool also treats any of them as a hard collision (research D3).
    type ScaffoldRequest =
        { Target: string
          ReservedPaths: string list }

    /// One file the provider wants laid down, addressed RELATIVE to the target. The provider supplies
    /// content as data; the TOOL writes it (research D1). Provider-owned.
    type EmittedFile =
        { RelativePath: string
          Contents: string }

    /// The provider's complete description of the runtime skeleton. Pure data.
    type ProviderEmission = { Files: EmittedFile list }

    /// Why a provider's own `Emit` failed (its internal error), surfaced verbatim by the tool. Distinct
    /// from the tool's safety refusals (`Refusal`).
    type ProviderError =
        | Unresolvable of detail: string      // the provider could not be produced/run (FR-009)
        | EmitFailed of detail: string        // the provider errored mid-description (FR-008)

    /// A resolved, selectable provider (research D1). `Emit` is PURE-SHAPED from the tool's view: given
    /// a request, it returns a description or an error — it performs NO filesystem writes. Third-party
    /// providers implement this in a .NET assembly; discovery/loading is a deferred host concern (the
    /// core gets a resolved value).
    type TemplateProvider =
        { Id: ProviderId
          ContractVersion: ProviderContractVersion
          Emit: ScaffoldRequest -> Result<ProviderEmission, ProviderError> }

    /// Why the TOOL refused to scaffold — decided in pure `update` BEFORE any write (research D4). Each
    /// is explicit and actionable (Principle VI, SC-005).
    type Refusal =
        | ContractMismatch of declared: ProviderContractVersion   // FR-009
        | ProviderUnavailable of detail: string                   // wraps ProviderError.Unresolvable (FR-009)
        | OutOfTarget of paths: string list                       // emitted path escapes the target (FR-009, D5)
        | Collision of paths: string list                         // path already exists / reserved (FR-007, D3)
        | ProviderErrored of detail: string                       // wraps ProviderError.EmitFailed (FR-008)

    /// The closed outcome of one seam run.
    type ScaffoldOutcome =
        | NoProvider                          // FR-002: nothing selected; seam is a no-op, no manifest write
        | Scaffolded                          // provider emission written in full
        | Refused of Refusal                  // explicit, recoverable safety refusal

    /// One generated path, marked provider-owned so later steps never mistake it for a
    /// lifecycle-authored source (FR-005, FR-006). The type leaves room for future ownership kinds.
    type PathOwnership = ProviderOwned

    /// One generated path. Target-RELATIVE for determinism (research D6).
    type GeneratedPath =
        { RelativePath: string
          Ownership: PathOwnership }

    /// The deterministic record of one scaffold run — the provenance other steps and automation consume
    /// (FR-005, FR-010, FR-012). Carries NO absolute target path, clock, or environment value (research
    /// D6, SC-004). `Provider` is `None` only for `NoProvider`. `Generated` lists written paths,
    /// ascending by `RelativePath`, `[]` unless `Scaffolded`. `Collisions` lists the pre-existing/
    /// reserved paths that forced a refusal, ascending, `[]` otherwise.
    type ScaffoldManifest =
        { Provider: (ProviderId * ProviderContractVersion) option
          Outcome: ScaffoldOutcome
          Generated: GeneratedPath list
          Collisions: string list }
