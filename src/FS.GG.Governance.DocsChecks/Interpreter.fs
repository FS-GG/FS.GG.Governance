// The EDGE of the docs/examples check (F24, P2) — the ONLY impure code in the domain (FR-007). Visibility
// lives in Interpreter.fsi (Constitution Principle II); no top-level access modifiers here. `realPort` reads
// only LOCAL files via BCL `System.IO`; it never throws out of itself — an unreadable source becomes an
// input fact in `Unreadable` (FR-012). Markdown/link FORMAT knowledge lives here in the swappable port.

namespace FS.GG.Governance.DocsChecks

open System
open System.IO
open System.Text
open System.Text.RegularExpressions
open FS.GG.Governance.Config.Model
open FS.GG.Governance.DocsChecks.Model

module SC = FS.GG.Governance.SurfaceChecks.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    type DocsReadErrorKind =
        | PermissionDenied
        | InvalidEncoding
        | TransientIo
        | UnexpectedIo

    type DocsReadError =
        { Path: string
          Kind: DocsReadErrorKind
          Detail: string }

    type DocsPort =
        { ReadSource: GovernedPath -> Result<string, string>
          ResolveTarget: string -> bool
          ResolveSymbol: string -> Result<bool, DocsReadError list> }

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

    let classifyReadError (path: string) (ex: exn) : DocsReadError =
        let kind =
            match ex with
            | :? UnauthorizedAccessException -> PermissionDenied
            | :? DecoderFallbackException -> InvalidEncoding
            | :? IOException -> TransientIo
            | _ -> UnexpectedIo

        { Path = path
          Kind = kind
          Detail = ex.Message }

    // Strict UTF-8 (while still honoring a BOM) makes malformed encoding observable. File.ReadAllText's
    // replacement fallback would otherwise turn corrupt bytes into text and fabricate a symbol verdict.
    let readSymbolSource (path: string) : Result<string, DocsReadError> =
        try
            let utf8 = UTF8Encoding(false, true)
            use reader = new StreamReader(path, utf8, true)
            Ok(reader.ReadToEnd())
        with ex ->
            Error(classifyReadError path ex)

    // A symbol/anchor resolves when its token appears (whole-word) in any committed `.fsi` under the
    // repo. The scan is complete-or-error: every file is read in deterministic order and ANY unreadable
    // input is returned as a typed diagnostic, even if another file contains the symbol. A partial scan
    // must never claim either presence or absence.
    let resolveSymbol (repo: string) (symbol: string) : Result<bool, DocsReadError list> =
        let files =
            try
                Directory.GetFiles(repo, "*.fsi", SearchOption.AllDirectories)
                |> Array.sortWith (fun left right -> StringComparer.Ordinal.Compare(left, right))
                |> Ok
            with ex ->
                Error [ classifyReadError repo ex ]

        match files with
        | Error errors -> Error errors
        | Ok paths ->
            let pattern = @"\b" + Regex.Escape symbol + @"\b"

            let resolved, errors =
                paths
                |> Array.fold
                    (fun (found, failures) path ->
                        match readSymbolSource path with
                        | Ok text -> found || Regex.IsMatch(text, pattern), failures
                        | Error error -> found, error :: failures)
                    (false, [])

            match List.rev errors with
            | [] -> Ok resolved
            | failures -> Error failures

    let renderReadError (error: DocsReadError) : string =
        let kind =
            match error.Kind with
            | PermissionDenied -> "permission-denied"
            | InvalidEncoding -> "invalid-encoding"
            | TransientIo -> "transient-io"
            | UnexpectedIo -> "unexpected-io"

        sprintf "%s: %s: %s" error.Path kind error.Detail

    let realPort (repo: string) : DocsPort =
        { ReadSource = readSource repo
          ResolveTarget = resolveTarget repo
          ResolveSymbol = resolveSymbol repo }

    let senseDocs (port: DocsPort) (request: SC.SurfaceCheckRequest) : DocsFacts =
        let source = request.Path
        let (GovernedPath srcStr) = source

        match SC.safe (fun () -> port.ReadSource source) with
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

            let references, symbolReadErrors =
                extractReferences text
                |> List.fold
                    (fun (facts, unreadable) symbol ->
                        let resolution =
                            try
                                port.ResolveSymbol symbol
                            with ex ->
                                Error [ classifyReadError (sprintf "symbol:%s" symbol) ex ]

                        match resolution with
                        | Ok resolves ->
                            let outcome = if resolves then ReferenceResolves else ReferenceStale symbol

                            { Source = source
                              Reference = symbol
                              Outcome = outcome }
                            :: facts,
                            unreadable
                        | Error errors ->
                            facts, (errors |> List.map renderReadError) @ unreadable)
                    ([], [])

            { Sources = [ source ]
              Links = links
              References = List.rev references
              Examples = []
              Unreadable = symbolReadErrors |> List.distinct |> List.sort }
