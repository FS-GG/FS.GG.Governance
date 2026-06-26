// The template-provider seam's value types (071). Visibility lives in Model.fsi (Principle II) — this
// file carries NO access modifiers on top-level bindings. Pure data only: immutable records / closed
// DUs, no helpers with I/O. Every consumer matches these exhaustively and wildcard-free so a new case
// is a compile error (data-model §1-6).

namespace FS.GG.Governance.Scaffold

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type ProviderId = ProviderId of string

    type ProviderContractVersion = { Major: int; Minor: int }

    type ScaffoldRequest =
        { Target: string
          ReservedPaths: string list }

    type EmittedFile =
        { RelativePath: string
          Contents: string }

    type ProviderEmission = { Files: EmittedFile list }

    type ProviderError =
        | Unresolvable of detail: string
        | EmitFailed of detail: string

    type TemplateProvider =
        { Id: ProviderId
          ContractVersion: ProviderContractVersion
          Emit: ScaffoldRequest -> Result<ProviderEmission, ProviderError> }

    type Refusal =
        | ContractMismatch of declared: ProviderContractVersion
        | ProviderUnavailable of detail: string
        | OutOfTarget of paths: string list
        | Collision of paths: string list
        | ProviderErrored of detail: string

    type ScaffoldOutcome =
        | NoProvider
        | Scaffolded
        | Refused of Refusal

    type PathOwnership = ProviderOwned

    type GeneratedPath =
        { RelativePath: string
          Ownership: PathOwnership }

    type ScaffoldManifest =
        { Provider: (ProviderId * ProviderContractVersion) option
          Outcome: ScaffoldOutcome
          Generated: GeneratedPath list
          Collisions: string list }
