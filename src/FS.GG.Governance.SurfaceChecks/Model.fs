// The shared cross-domain finding vocabulary for F24 surface checks (implementation).
// Visibility lives in Model.fsi (Constitution Principle II); this file carries NO top-level access modifiers.
// PURE data + total token tables: no I/O, no clock, no environment. `enforcementInputOf` only assembles the
// F023 `EnforcementInput` — it adds NO enforcement-truth-table logic (FR-014).

namespace FS.GG.Governance.SurfaceChecks

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type CheckDomain =
        | PackageDomain
        | DocsDomain
        | SkillDomain
        | DesignDomain

    type FindingLocation =
        { File: GovernedPath
          Detail: string }

    type SurfaceFinding =
        { Domain: CheckDomain
          Surface: SurfaceId
          Code: string
          Location: FindingLocation
          BaseSeverity: Severity
          Maturity: Maturity
          EvidenceTag: EvidenceTag option
          IsInputState: bool
          Message: string }

    type SurfaceCheckRequest =
        { Domain: CheckDomain
          Surface: SurfaceId
          Class: SurfaceClass
          Path: GovernedPath
          EvidenceTag: EvidenceTag option }

    let checkDomainToken (domain: CheckDomain) : string =
        match domain with
        | PackageDomain -> "package"
        | DocsDomain -> "docs"
        | SkillDomain -> "skill"
        | DesignDomain -> "design"

    let checkDomainOrdinal (domain: CheckDomain) : int =
        match domain with
        | PackageDomain -> 0
        | DocsDomain -> 1
        | SkillDomain -> 2
        | DesignDomain -> 3

    let severityToken (severity: Severity) : string =
        match severity with
        | Advisory -> "advisory"
        | Blocking -> "blocking"

    let enforcementInputOf (finding: SurfaceFinding) (mode: RunMode) (profile: Profile) : EnforcementInput =
        { BaseSeverity = finding.BaseSeverity
          Maturity = finding.Maturity
          Mode = mode
          Profile = profile }

    // 111/A6: the shared finding-builder + read-guard the four *Checks packs used to hand-copy. `mkFinding`
    // is parameterized by domain + maturity + source path (each pack keeps a one-line wrapper binding those);
    // `safe` reifies BOTH an `Error` and a thrown exception into `Error`.

    let mkFinding
        (domain: CheckDomain)
        (maturity: Maturity)
        (request: SurfaceCheckRequest)
        (source: GovernedPath)
        (code: string)
        (detail: string)
        (severity: Severity)
        (isInput: bool)
        (message: string)
        : SurfaceFinding =
        let (GovernedPath raw) = source

        { Domain = domain
          Surface = request.Surface
          Code = code
          Location = { File = normalizePath raw; Detail = detail }
          BaseSeverity = severity
          Maturity = maturity
          EvidenceTag = request.EvidenceTag
          IsInputState = isInput
          Message = message }

    let safe (read: unit -> Result<'a, string>) : Result<'a, string> =
        try
            read ()
        with ex ->
            Error(sprintf "read threw: %s" ex.Message)
