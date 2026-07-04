// The EDGE of the design check (F24, P3) — the ONLY impure code in the domain and the ONLY place a catalog
// is read (FR-007, SC-004). Visibility lives in Interpreter.fsi (Constitution Principle II); no top-level
// access modifiers here. `realPort` reads only LOCAL files via `System.IO` / `System.Text.Json`; it starts
// no process, opens no socket, references NO Skia/rendering/UI/registry. It never throws out of itself — an
// absent/unreadable catalog or descriptor becomes an input fact in `CatalogUnavailable` (FR-012).

namespace FS.GG.Governance.DesignChecks

open System.IO
open System.Text.Json
open FS.GG.Governance.Config.Model
open FS.GG.Governance.DesignChecks.Model

module SC = FS.GG.Governance.SurfaceChecks.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    type DesignPort =
        { ReadDescriptor: GovernedPath -> Result<string, string>
          ReadTokenCatalog: unit -> Result<Set<string>, string>
          ReadCaptureCatalog: unit -> Result<Set<string>, string>
          ReadControlCatalog: unit -> Result<Set<string>, string>
          ReadContrastCatalog: unit -> Result<Map<string, decimal * decimal>, string> }

    let readDescriptor (repo: string) (path: GovernedPath) : Result<string, string> =
        let (GovernedPath rel) = path
        let full = Path.Combine(repo, rel)

        if not (File.Exists full) then
            Error(sprintf "design surface descriptor not found: %s" rel)
        else
            try
                Ok(File.ReadAllText full)
            with ex ->
                Error(sprintf "design surface descriptor unreadable: %s: %s" rel ex.Message)

    // Read a JSON array-of-strings catalog into a Set (System.Text.Json only — no rendering).
    let readStringSetCatalog (repo: string) (rel: string) : Result<Set<string>, string> =
        let full = Path.Combine(repo, rel)

        if not (File.Exists full) then
            Error(sprintf "catalog not found: %s" rel)
        else
            try
                use doc = JsonDocument.Parse(File.ReadAllText full)

                doc.RootElement.EnumerateArray()
                |> Seq.choose (fun e -> e.GetString() |> Option.ofObj)
                |> Set.ofSeq
                |> Ok
            with ex ->
                Error(sprintf "catalog unreadable: %s: %s" rel ex.Message)

    // Read a JSON object { "pair": { "ratio": <num>, "threshold": <num> } } into a Map.
    let readContrastCatalog (repo: string) (rel: string) : Result<Map<string, decimal * decimal>, string> =
        let full = Path.Combine(repo, rel)

        if not (File.Exists full) then
            Error(sprintf "catalog not found: %s" rel)
        else
            try
                use doc = JsonDocument.Parse(File.ReadAllText full)

                doc.RootElement.EnumerateObject()
                |> Seq.map (fun p ->
                    let ratio = p.Value.GetProperty("ratio").GetDecimal()
                    let threshold = p.Value.GetProperty("threshold").GetDecimal()
                    p.Name, (ratio, threshold))
                |> Map.ofSeq
                |> Ok
            with ex ->
                Error(sprintf "contrast catalog unreadable: %s: %s" rel ex.Message)

    let realPort (repo: string) (catalogLayout: string * string * string * string) : DesignPort =
        let tokenPath, capturePath, controlPath, contrastPath = catalogLayout

        { ReadDescriptor = readDescriptor repo
          ReadTokenCatalog = fun () -> readStringSetCatalog repo tokenPath
          ReadCaptureCatalog = fun () -> readStringSetCatalog repo capturePath
          ReadControlCatalog = fun () -> readStringSetCatalog repo controlPath
          ReadContrastCatalog = fun () -> readContrastCatalog repo contrastPath }

    // Parse the neutral design surface descriptor: `token:` / `capture:` / `control:` referenced ids.
    let parseDescriptor (text: string) : string list * string list * string list =
        let lines =
            text.Split([| '\n'; '\r' |])
            |> Array.map (fun s -> s.Trim())
            |> Array.filter (fun s -> s <> "")

        let valuesFor (prefix: string) =
            lines
            |> Array.filter (fun l -> l.StartsWith prefix)
            |> Array.map (fun l -> l.Substring(prefix.Length).Trim())
            |> List.ofArray

        valuesFor "token:", valuesFor "capture:", valuesFor "control:"

    let senseDesign (port: DesignPort) (request: SC.SurfaceCheckRequest) : DesignFacts =
        let mutable unavailable = []

        let refTokens, refCaptures, refControls =
            match SC.safe (fun () -> port.ReadDescriptor request.Path) with
            | Ok text -> parseDescriptor text
            | Error e ->
                unavailable <- sprintf "design surface descriptor: %s" e :: unavailable
                [], [], []

        let resolveSet (read: unit -> Result<Set<string>, string>) (mk: string -> ResolveOutcome -> 'a) (refs: string list) (label: string) : 'a list =
            // `safe` wraps the port read so a throwing catalog port degrades to a sensed error instead of
            // escaping `senseDesign` (its never-throws contract, #56/B13) — previously only ReadDescriptor
            // was guarded, leaving four of the five port calls able to escape.
            match SC.safe read with
            | Ok set -> refs |> List.map (fun r -> mk r (if Set.contains r set then Resolves else Absent r))
            | Error e ->
                unavailable <- sprintf "%s catalog: %s" label e :: unavailable
                []

        let tokens =
            resolveSet port.ReadTokenCatalog (fun t o -> { Token = t; Outcome = o }) refTokens "token"

        let captures =
            resolveSet port.ReadCaptureCatalog (fun c o -> { Capture = c; Outcome = o }) refCaptures "capture"

        let controls =
            resolveSet port.ReadControlCatalog (fun c o -> { Control = c; Outcome = o }) refControls "control"

        let contrasts =
            match SC.safe (fun () -> port.ReadContrastCatalog()) with
            | Ok m ->
                m
                |> Map.toList
                |> List.map (fun (pair, (ratio, threshold)) ->
                    { Pair = pair
                      Ratio = ratio
                      Threshold = threshold
                      Meets = ratio >= threshold })
            | Error e ->
                unavailable <- sprintf "contrast catalog: %s" e :: unavailable
                []

        { Tokens = tokens
          Captures = captures
          Controls = controls
          Contrasts = contrasts
          CatalogUnavailable = List.rev unavailable }
