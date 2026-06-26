// PROPOSED public surface for the NEW pure leaf FS.GG.Governance.JsonTokens (feature 073).
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II);
// the matching JsonTokens.fs carries NO access modifiers. Drafted .fsi-first (Principle I).
//
// Pure, total, System.*-free closed-enum token helpers shared by the *Json projections. Each
// `match` is EXHAUSTIVE over its closed DU with NO wildcard, so a future enum case is a compile
// error here, never a silently mis-tokened JSON field. Token strings are byte-identical to the
// strings the projections emit today (feature 073 acceptance gate: goldens unchanged).
//
// NOTE: placed ABOVE the domain-enum owners (Config/Gates/Findings/FreshnessKey/Enforcement/
// GateRun) and BELOW the projections — it is NOT under Kernel (Kernel cannot see these enums).

namespace FS.GG.Governance.JsonTokens

open FS.GG.Governance.Gates.Model          // Cost, Maturity, GateDisposition
open FS.GG.Governance.Config.Model          // EnvironmentClass
open FS.GG.Governance.Findings.Model        // Severity
open FS.GG.Governance.Enforcement.Model     // ExitCodeBasis, Profile   (exact owner confirmed at extraction)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module JsonTokens =

    /// `Cost` → `cheap` / `medium` / `high` / `exhaustive`.
    val costToken: cost: Cost -> string

    /// `Maturity` → `observe` / `warn` / `blockOnPr` / `blockOnShip` / `blockOnRelease`.
    val maturityToken: maturity: Maturity -> string

    /// `Severity` → `advisory` / `blocking`.
    val severityToken: severity: Severity -> string

    /// `EnvironmentClass` → `local` / `ci` / `localOrCi` / `release`.
    val environmentToken: env: EnvironmentClass -> string

    /// `GateDisposition` → `executed` / `reused` / `notExecuted`.
    val dispositionToken: disposition: GateDisposition -> string

    /// `ExitCodeBasis` → `clean` / `blocked`.
    val basisToken: basis: ExitCodeBasis -> string

    /// `Profile` → token strings verbatim from the current projection/Enforcement copy.
    val profileToken: profile: Profile -> string

// Open issues resolved at implementation time (do not block the contract):
//  * Exact owning module for each enum (Gates vs GateRun vs Config) — set by the real `open`s.
//  * `severityToken` operates on Findings/Enforcement `Severity` (advisory/blocking), which is
//    distinct from `Kernel.Json.severityToken` even though the strings coincide.
