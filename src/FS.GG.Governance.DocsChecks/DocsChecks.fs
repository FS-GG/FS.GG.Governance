// The pure docs/examples rule pack for F24 (P2). Visibility lives in DocsChecks.fsi (Constitution
// Principle II); this file carries NO top-level access modifiers. PURE and TOTAL (FR-007): no I/O, no
// clock, never throws; byte-identical findings for identical facts (FR-010, SC-005).

namespace FS.GG.Governance.DocsChecks

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.DocsChecks.Model

module SC = FS.GG.Governance.SurfaceChecks.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DocsChecks =

    // Fixed pre-PR enforcement maturity (see PackageChecks.checkMaturity for the rationale).
    let checkMaturity = BlockOnPr

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
        SC.mkFinding SC.DocsDomain checkMaturity request source code detail severity isInput message

    let linkFindings (request: SC.SurfaceCheckRequest) (facts: DocsFacts) : SC.SurfaceFinding list =
        facts.Links
        |> List.choose (fun l ->
            match l.Outcome with
            | LinkResolves -> None
            | LinkDangling target ->
                let (GovernedPath src) = l.Source
                let message = sprintf "link '%s' in %s does not resolve to '%s'" l.LinkText src target
                Some(mkFinding request "docs.link-currency" l.Source l.LinkText Blocking false message))

    let referenceFindings (request: SC.SurfaceCheckRequest) (facts: DocsFacts) : SC.SurfaceFinding list =
        facts.References
        |> List.choose (fun r ->
            match r.Outcome with
            | ReferenceResolves -> None
            | ReferenceStale symbol ->
                let message = sprintf "reference '%s' is stale (symbol/anchor '%s' not found)" r.Reference symbol
                Some(mkFinding request "docs.reference-currency" r.Source r.Reference Blocking false message))

    let exampleFindings (request: SC.SurfaceCheckRequest) (facts: DocsFacts) : SC.SurfaceFinding list =
        facts.Examples
        |> List.choose (fun e ->
            match e.Outcome with
            | ExampleCurrent -> None
            // Judgement-heavy: "match the current product surface" requires intent judgement ⇒ Advisory,
            // never blocks (C3, FR-011, US5). Deterministic compile/evaluate staleness is a package transcript.
            | ExampleStale detail ->
                let message = sprintf "example '%s' may no longer match the product surface: %s" e.Example detail
                Some(mkFinding request "docs.example-freshness" e.Source e.Example Advisory false message))

    let sourceFindings (request: SC.SurfaceCheckRequest) (facts: DocsFacts) : SC.SurfaceFinding list =
        facts.Unreadable
        |> List.map (fun src ->
            let message = sprintf "docs source could not be read: %s" src
            mkFinding request "docs.source-unreadable" (normalizePath src) "source-unreadable" Blocking true message)

    let evaluate (request: SC.SurfaceCheckRequest) (facts: DocsFacts) : SC.SurfaceFinding list =
        [ linkFindings request facts
          referenceFindings request facts
          exampleFindings request facts
          sourceFindings request facts ]
        |> List.concat
        |> List.sortBy (fun (f: SC.SurfaceFinding) ->
            let (GovernedPath file) = f.Location.File
            file, f.Location.Detail, f.Code)
