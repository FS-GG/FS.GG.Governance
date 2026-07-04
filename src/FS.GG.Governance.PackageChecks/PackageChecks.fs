// The pure package/API rule pack for F24 (P1). Visibility lives in PackageChecks.fsi (Constitution
// Principle II); this file carries NO top-level access modifiers — the finding builder + the two
// per-family collectors are hidden by ABSENCE from the .fsi. PURE and TOTAL (FR-007): no I/O, no clock,
// never throws; byte-identical findings for identical facts (FR-010, SC-005).

namespace FS.GG.Governance.PackageChecks

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.PackageChecks.Model

module SC = FS.GG.Governance.SurfaceChecks.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PackageChecks =

    // Every F24 surface finding carries a fixed pre-PR enforcement maturity (BlockOnPr): `fsgg verify` runs
    // these checks pre-PR, and `deriveEffectiveSeverity` decides blocking from (BaseSeverity, BlockOnPr,
    // mode, profile). A base-Advisory finding never escalates regardless (FR-011); a base-Blocking finding
    // blocks once the run mode reaches the profile-adjusted floor (e.g. Verify under Strict).
    let checkMaturity = BlockOnPr

    // Build one finding bound to the request's surface + declared evidence tag. `source` is re-normalized to
    // a repo-relative forward-slash GovernedPath (FR-010).
    // 111/A6: delegates to the shared `SC.mkFinding`, binding this pack's domain + maturity + path-source.
    let mkFinding
        (request: SC.SurfaceCheckRequest)
        (code: string)
        (source: GovernedPath)
        (detail: string)
        (severity: Severity)
        (isInput: bool)
        (message: string)
        : SC.SurfaceFinding =
        SC.mkFinding SC.PackageDomain checkMaturity request source code detail severity isInput message

    let baselineFindings
        (request: SC.SurfaceCheckRequest)
        (facts: PackageFacts)
        : SC.SurfaceFinding list =
        match facts.Baseline with
        | BaselineMatches -> []
        | BaselineDrift(added, removed) ->
            let addedS = List.sort added
            let removedS = List.sort removed

            let detail =
                sprintf "drift: +%d/-%d members" (List.length addedS) (List.length removedS)

            let render xs =
                if List.isEmpty xs then "(none)" else String.concat ", " xs

            let message =
                sprintf "public surface drifted: added [%s], removed [%s]" (render addedS) (render removedS)

            [ mkFinding request "package.baseline-drift" facts.BaselineSource detail Blocking false message ]
        | BaselineAbsent(SurfaceTokens tokens) ->
            let message =
                sprintf "no committed baseline; generated a %d-token baseline — commit it" (List.length tokens)

            [ mkFinding request "package.baseline-absent" facts.BaselineSource "baseline-absent" Blocking true message ]
        | BaselineUnreadable source ->
            let message = sprintf "baseline source could not be read: %s" source

            [ mkFinding
                  request
                  "package.baseline-unreadable"
                  facts.BaselineSource
                  "baseline-unreadable"
                  Blocking
                  true
                  message ]

    let transcriptFindings
        (request: SC.SurfaceCheckRequest)
        (facts: PackageFacts)
        : SC.SurfaceFinding list =
        facts.Transcripts
        |> List.collect (fun t ->
            match t.Outcome with
            | TranscriptPasses -> []
            | TranscriptCompileFailed detail ->
                let message = sprintf "transcript '%s' no longer compiles: %s" t.ExampleId detail
                [ mkFinding request "package.transcript-compile" t.Source t.ExampleId Blocking false message ]
            | TranscriptResultChanged(expected, actual) ->
                let message =
                    sprintf "transcript '%s' result changed: expected '%s', got '%s'" t.ExampleId expected actual

                [ mkFinding request "package.transcript-result" t.Source t.ExampleId Blocking false message ]
            | TranscriptUnlocatable source ->
                let message = sprintf "transcript '%s' could not be located: %s" t.ExampleId source
                [ mkFinding request "package.transcript-unlocatable" t.Source t.ExampleId Blocking true message ])

    let evaluate
        (request: SC.SurfaceCheckRequest)
        (facts: PackageFacts)
        : SC.SurfaceFinding list =
        List.append (baselineFindings request facts) (transcriptFindings request facts)
        |> List.sortBy (fun (f: SC.SurfaceFinding) ->
            let (GovernedPath file) = f.Location.File
            f.Location.Detail, file, f.Code)
