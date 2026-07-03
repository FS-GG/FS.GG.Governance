namespace FS.GG.Governance.JsonTokens

// The 073 pure closed-enum token leaf. Each helper is the EXHAUSTIVE enum→string match the *Json
// projections used to hand-copy, with NO wildcard (research D3): a future enum case is a compile error
// here, never a silently mis-tokened field. Token strings are byte-identical to today's projection
// output. No clock/host/filesystem/git/environment/network; no visibility modifiers — the surface is
// JsonTokens.fsi (Principle II).
//
// `Release` is a case of BOTH `EnvironmentClass` and `Profile` (both opened here), so those two cases
// are type-qualified to disambiguate; every other case name is unique across the opened DUs.

open FS.GG.Governance.Config.Model              // Cost, Maturity, EnvironmentClass
open FS.GG.Governance.GateRun.Model             // GateDisposition
open FS.GG.Governance.Enforcement.Enforcement   // Severity, Profile
open FS.GG.Governance.Ship.Model                // ExitCodeBasis

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module JsonTokens =

    let costToken (cost: Cost) : string =
        match cost with
        | Cheap -> "cheap"
        | Medium -> "medium"
        | High -> "high"
        | Exhaustive -> "exhaustive"

    let maturityToken (maturity: Maturity) : string =
        match maturity with
        | Observe -> "observe"
        | Warn -> "warn"
        | BlockOnPr -> "blockOnPr"
        | BlockOnShip -> "blockOnShip"
        | BlockOnRelease -> "blockOnRelease"

    let severityToken (severity: Severity) : string =
        match severity with
        | Advisory -> "advisory"
        | Blocking -> "blocking"

    // DIVERGENCE — DO NOT UNIFY: this JSON-wire spelling `localOrCi` (camelCase) deliberately DIFFERS from the
    // store/config-wire spelling `local-or-ci` (kebab) emitted by `EvidenceReuseStore.environmentToken` /
    // `Config.Schema` and read back by `FreshnessSensing.parseEnv`. The strings DIVERGE, so folding the two
    // `environmentToken`s into one would change bytes and break the evidence-reuse-store round-trip at runtime
    // — exactly like the `dispositionToken`/`verdictToken` divergences in VerifyJson.Core. The camelCase
    // spelling is pinned by JsonTokensTests; the kebab spelling by EvidenceReuseStore RoundTripTests.
    let environmentToken (env: EnvironmentClass) : string =
        match env with
        | Local -> "local"
        | Ci -> "ci"
        | LocalOrCi -> "localOrCi"
        | EnvironmentClass.Release -> "release"

    let dispositionToken (disposition: GateDisposition) : string =
        match disposition with
        | Executed _ -> "executed"
        | Reused _ -> "reused"
        | NotExecuted -> "notExecuted"

    let basisToken (basis: ExitCodeBasis) : string =
        match basis with
        | Clean -> "clean"
        | Blocked -> "blocked"

    let profileToken (profile: Profile) : string =
        match profile with
        | Light -> "light"
        | Standard -> "standard"
        | Strict -> "strict"
        | Profile.Release -> "release"
