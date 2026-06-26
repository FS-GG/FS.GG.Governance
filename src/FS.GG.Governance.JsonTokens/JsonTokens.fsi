// Curated public signature contract for the pure closed-enum token leaf (feature 073).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II); the
// matching JsonTokens.fs carries NO access modifiers. Drafted .fsi-first (Principle I).
//
// Pure, total, System.*-free closed-enum token helpers shared by the *Json projections. Each `match` is
// EXHAUSTIVE over its closed DU with NO wildcard, so a future enum case is a compile error here, never a
// silently mis-tokened JSON field. Token strings are byte-identical to the strings the projections emit
// today (feature 073 acceptance gate: goldens unchanged).
//
// NOTE: placed ABOVE the domain-enum owners (Config/GateRun/Enforcement/Ship) and BELOW the projections
// — it is NOT under Kernel (the kernel cannot see these enums, and the pure projections must not
// reference the kernel/host capability). The `Verdict` token is NOT one of the seven and its projection
// copies emit divergent strings, so it stays local (research D3).

namespace FS.GG.Governance.JsonTokens

open FS.GG.Governance.Config.Model              // Cost, Maturity, EnvironmentClass
open FS.GG.Governance.GateRun.Model             // GateDisposition
open FS.GG.Governance.Enforcement.Enforcement   // Severity, Profile
open FS.GG.Governance.Ship.Model                // ExitCodeBasis

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module JsonTokens =

    /// `Cost` → `cheap` / `medium` / `high` / `exhaustive`.
    val costToken: cost: Cost -> string

    /// `Maturity` → `observe` / `warn` / `blockOnPr` / `blockOnShip` / `blockOnRelease`.
    val maturityToken: maturity: Maturity -> string

    /// `Severity` → `advisory` / `blocking` (the Enforcement severity, distinct from `Kernel.Json`'s).
    val severityToken: severity: Severity -> string

    /// `EnvironmentClass` → `local` / `ci` / `localOrCi` / `release`.
    val environmentToken: env: EnvironmentClass -> string

    /// `GateDisposition` → `executed` / `reused` / `notExecuted`.
    val dispositionToken: disposition: GateDisposition -> string

    /// `ExitCodeBasis` → `clean` / `blocked`.
    val basisToken: basis: ExitCodeBasis -> string

    /// `Profile` → `light` / `standard` / `strict` / `release`.
    val profileToken: profile: Profile -> string
