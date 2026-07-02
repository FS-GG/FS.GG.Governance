// The EDGE of the docs/examples check (F24, P2) — the ONLY impure code in the domain (FR-007). Visibility
// lives in Interpreter.fsi (Constitution Principle II); no top-level access modifiers here. `realPort` reads
// only LOCAL files via BCL `System.IO`; it never throws out of itself — an unreadable source becomes an
// input fact in `Unreadable` (FR-012). Markdown/link FORMAT knowledge lives here in the swappable port.

namespace FS.GG.Governance.DocsChecks

open System.IO
open System.Text.RegularExpressions
open FS.GG.Governance.Config.Model
open FS.GG.Governance.DocsChecks.Model

module SC = FS.GG.Governance.SurfaceChecks.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    type DocsPort =
        { ReadSource: GovernedPath -> Result<string, string>
          ResolveTarget: string -> bool
          ResolveSymbol: string -> bool }

    // ── Deterministic markdown extraction (no clock, no order dependence) ──

    // `[text](target)` links. Returns (linkText, target) pairs in source order.
    let extractLinks (text: string) : (string * string) list =
        Regex.Matches(text, @"\[([^\]\[]+)\]\(([^)]+)\)")
        |> Seq.map (fun m -> m.Groups.[1].Value, m.Groups.[2].Value)
        |> List.ofSeq

    // `[[symbol]]` wiki references. Returns the symbol tokens in source order.
    let extractReferences (text: string) : string list =
        Regex.Matches(text, @"\[\[([^\]]+)\]\]")
        |> Seq.map (fun m -> m.Groups.[1].Value)
        |> List.ofSeq

    let readSource (repo: string) (path: GovernedPath) : Result<string, string> =
        let (GovernedPath rel) = path
        let full = Path.Combine(repo, rel)

        if not (File.Exists full) then
            Error(sprintf "docs source not found: %s" rel)
        else
            try
                Ok(File.ReadAllText full)
            with ex ->
                Error(sprintf "docs source unreadable: %s: %s" rel ex.Message)

    // A target resolves when (after dropping any `#anchor`) the path part exists under the repo root; a
    // pure same-page anchor (`#x`) is treated as resolving (its existence is verified by ResolveSymbol).
    // STANDALONE-SAFE (FR-016): a target that escapes the product root via `..` NEVER resolves — it is a
    // dangling link, never a fabricated pass against a file outside the standalone product.
    let resolveTarget (repo: string) (target: string) : bool =
        let pathPart =
            let i = target.IndexOf '#'
            if i >= 0 then target.Substring(0, i) else target

        if pathPart = "" then
            true
        else
            let rootFull = Path.GetFullPath repo
            let combined = Path.GetFullPath(Path.Combine(repo, pathPart))

            // Compare against the root WITH a trailing separator so a sibling prefixed by the root name
            // (e.g. `../<repoName>-sibling/file.md`) does NOT count as inside (FR-016 — mirrors the
            // Scaffold.Interpreter.resolveUnder guard). Without this, `StartsWith rootFull` fabricates a
            // pass against a file outside the standalone product.
            let rootWithSep =
                if rootFull.EndsWith(string Path.DirectorySeparatorChar) then
                    rootFull
                else
                    rootFull + string Path.DirectorySeparatorChar

            combined.StartsWith rootWithSep && File.Exists combined

    // A symbol/anchor resolves when its token appears (whole-word) in any committed `.fsi` under the repo.
    let resolveSymbol (repo: string) (symbol: string) : bool =
        try
            let pattern = @"\b" + Regex.Escape symbol + @"\b"

            Directory.GetFiles(repo, "*.fsi", SearchOption.AllDirectories)
            |> Array.exists (fun f ->
                try
                    Regex.IsMatch(File.ReadAllText f, pattern)
                with _ ->
                    false)
        with _ ->
            false

    let realPort (repo: string) : DocsPort =
        { ReadSource = readSource repo
          ResolveTarget = resolveTarget repo
          ResolveSymbol = resolveSymbol repo }

    let senseDocs (port: DocsPort) (request: SC.SurfaceCheckRequest) : DocsFacts =
        let source = request.Path
        let (GovernedPath srcStr) = source

        let safe (read: unit -> Result<'a, string>) : Result<'a, string> =
            try
                read ()
            with ex ->
                Error(sprintf "read threw: %s" ex.Message)

        match safe (fun () -> port.ReadSource source) with
        | Error _ ->
            { Sources = [ source ]
              Links = []
              References = []
              Examples = []
              Unreadable = [ srcStr ] }
        | Ok text ->
            let links =
                extractLinks text
                |> List.map (fun (linkText, target) ->
                    let outcome = if port.ResolveTarget target then LinkResolves else LinkDangling target

                    { Source = source
                      LinkText = linkText
                      Target = target
                      Outcome = outcome })

            let references =
                extractReferences text
                |> List.map (fun symbol ->
                    let outcome =
                        if port.ResolveSymbol symbol then
                            ReferenceResolves
                        else
                            ReferenceStale symbol

                    { Source = source
                      Reference = symbol
                      Outcome = outcome })

            { Sources = [ source ]
              Links = links
              References = references
              Examples = []
              Unreadable = [] }
