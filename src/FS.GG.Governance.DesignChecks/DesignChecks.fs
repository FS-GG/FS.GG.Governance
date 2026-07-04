// The pure design/rendering rule pack for F24 (P3, render-fenced). Visibility lives in DesignChecks.fsi
// (Constitution Principle II); this file carries NO top-level access modifiers. PURE and TOTAL (FR-007): no
// I/O, no clock, NO rendering reference; byte-identical findings for identical facts (FR-010, SC-005). Every
// design entry is caller-supplied via the facts the sensor read — never a literal here (SC-004 neutrality).

namespace FS.GG.Governance.DesignChecks

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.DesignChecks.Model

module SC = FS.GG.Governance.SurfaceChecks.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DesignChecks =

    // Fixed pre-PR enforcement maturity (see PackageChecks.checkMaturity for the rationale).
    let checkMaturity = BlockOnPr

    // 111/A6: delegates to the shared `SC.mkFinding`, binding this pack's domain + maturity + path-source.
    let mkFinding
        (request: SC.SurfaceCheckRequest)
        (code: string)
        (detail: string)
        (severity: Severity)
        (isInput: bool)
        (message: string)
        : SC.SurfaceFinding =
        SC.mkFinding SC.DesignDomain checkMaturity request request.Path code detail severity isInput message

    let resolveFinding
        (request: SC.SurfaceCheckRequest)
        (code: string)
        (kind: string)
        (entry: string)
        (outcome: ResolveOutcome)
        : SC.SurfaceFinding option =
        match outcome with
        | Resolves -> None
        | Absent missing ->
            let message = sprintf "%s '%s' is not in the catalog" kind missing
            Some(mkFinding request code entry Blocking false message)

    let evaluate (request: SC.SurfaceCheckRequest) (facts: DesignFacts) : SC.SurfaceFinding list =
        let tokenFindings =
            facts.Tokens |> List.choose (fun t -> resolveFinding request "design.token" "token" t.Token t.Outcome)

        let captureFindings =
            facts.Captures
            |> List.choose (fun c -> resolveFinding request "design.capture" "capture" c.Capture c.Outcome)

        let controlFindings =
            facts.Controls
            |> List.choose (fun c -> resolveFinding request "design.control" "control" c.Control c.Outcome)

        let contrastFindings =
            facts.Contrasts
            |> List.choose (fun c ->
                if c.Meets then
                    None
                else
                    let message =
                        sprintf "contrast pair '%s' ratio %M is below threshold %M" c.Pair c.Ratio c.Threshold

                    Some(mkFinding request "design.contrast" c.Pair Blocking false message))

        let catalogFindings =
            facts.CatalogUnavailable
            |> List.map (fun cat ->
                let message = sprintf "design catalog could not be read: %s" cat
                mkFinding request "design.catalog-unavailable" cat Blocking true message)

        [ tokenFindings
          captureFindings
          controlFindings
          contrastFindings
          catalogFindings ]
        |> List.concat
        |> List.sortBy (fun (f: SC.SurfaceFinding) -> f.Code, f.Location.Detail)
