// #390 — the production caller #368 never had.
//
// `CodeChecks.analyze` shipped complete and tested under FS-GG/FS.GG.Governance#368, and #385 composed it
// into the published F# constitution profile as `fsharp:idiomatic-simplicity`. Nothing in `src/` invoked it,
// so the published gate was STRUCTURALLY INERT: green because no code path could look, not because the
// repository was clean. This module is the missing call site, reached from `Interpreter.senseSurfacesReal`
// exactly where #366's `FSharpSurface.evaluate` and #369's `FSharpEffectBoundary.evaluate` are reached.
//
// APPLICABILITY IS DECLARED, NOT ASSUMED — the #369 posture, and here it is a correctness requirement rather
// than a taste. `CodeChecks.analyzeDocument` type-checks each document STANDALONE, as a script
// (`GetProjectOptionsFromScript` over `<path>.fsx`), with no project references. Swept unconditionally over a
// multi-project repository it would therefore report `compiler-analysis-failed` for essentially every source
// that references a sibling project — hundreds of findings that say nothing about the code. That boundary is
// MEASURED and filed at its root cause as FS-GG/FS.GG.Governance#391; the declared scope below CONTAINS it,
// and deliberately does not pretend to fix it. The pack is
// applicable exactly where a repository DECLARES the sources it wants analysed, in
// `.fsgg/fsharp-simplicity.json`, the same shape `.fsgg/fsharp-surface.json` already has for #366's policy.
// No declaration ⇒ no documents ⇒ no findings, and nothing is silently skipped: the absence is the
// repository's own recorded choice.
//
// FAIL CLOSED on a malformed declaration. Every parse defect raises `PolicyError`, which `sense` reifies as
// an `Error` and `senseSurfacesReal` turns into a Blocking input-state finding — never an empty pass. The
// exception is CONFINED to this module's parser: nothing throws out of `sense` or `findings`.
//
// INTERNAL on purpose: `VerifyCommand`'s public surface is exactly `Loop` + `Interpreter` + the three 076 fold
// seams (`SurfaceDriftTests`), and this is a private sense edge, not a new seam.

namespace FS.GG.Governance.VerifyCommand

open System
open System.IO
open System.Text.Json
open FS.GG.Governance.Config.Model

module internal CodeSweep =

    module SC = FS.GG.Governance.SurfaceChecks.Model
    module CP = FS.GG.Governance.SurfaceChecks.Profile
    module CM = FS.GG.Governance.CodeChecks.Model

    /// The repository's own declaration of this pack's scope. Absent ⇒ the pack is not applicable here.
    [<Literal>]
    let PolicyPath = ".fsgg/fsharp-simplicity.json"

    /// The surface id every finding of this pack carries — the `fsharp-*` naming #366 and #369 already use.
    [<Literal>]
    let SurfaceName = "fsharp-idiomatic-simplicity"

    let request: SC.SurfaceCheckRequest =
        { Domain = SC.DesignDomain
          Surface = SurfaceId SurfaceName
          Class = DesignSurface
          Path = normalizePath PolicyPath
          EvidenceTag = None }

    /// A defect in the declaration. Raised only by the parser below and caught only by `sense`.
    exception private PolicyError of string

    // ── Declaration parsing (fail-closed; every unreadable or unrecognised token raises) ──────────────

    /// `JsonElement.GetString()` is typed `string | null`; on a `JsonValueKind.String` element it never is.
    /// Normalize once, here, so every caller below stays non-nullable without a cast at each use site.
    let private text (element: JsonElement) : string =
        element.GetString() |> Option.ofObj |> Option.defaultValue ""

    let private prop (element: JsonElement) (name: string) : JsonElement option =
        match element.TryGetProperty name with
        | true, value when value.ValueKind <> JsonValueKind.Null -> Some value
        | _ -> None

    let private stringList (element: JsonElement) (name: string) : string list =
        match prop element name with
        | None -> []
        | Some value when value.ValueKind = JsonValueKind.Array ->
            value.EnumerateArray()
            |> Seq.map (fun item ->
                if item.ValueKind = JsonValueKind.String then
                    text item
                else
                    raise (PolicyError(sprintf "'%s' must contain only strings; found %O" name item.ValueKind)))
            |> Seq.toList
        | Some value -> raise (PolicyError(sprintf "'%s' must be an array; found %O" name value.ValueKind))

    let private optionalInt (element: JsonElement) (name: string) : int option =
        match prop element name with
        | None -> None
        | Some value when value.ValueKind = JsonValueKind.Number ->
            match value.TryGetInt32() with
            | true, number -> Some number
            | _ -> raise (PolicyError(sprintf "'%s' must be a 32-bit integer" name))
        | Some value -> raise (PolicyError(sprintf "'%s' must be a number; found %O" name value.ValueKind))

    let private requiredString (element: JsonElement) (name: string) : string =
        match prop element name with
        | Some value when value.ValueKind = JsonValueKind.String -> text value
        | Some value -> raise (PolicyError(sprintf "'%s' must be a string; found %O" name value.ValueKind))
        | None -> raise (PolicyError(sprintf "'%s' is required" name))

    let private optionalString (element: JsonElement) (name: string) : string =
        match prop element name with
        | None -> ""
        | Some value when value.ValueKind = JsonValueKind.String -> text value
        | Some value -> raise (PolicyError(sprintf "'%s' must be a string; found %O" name value.ValueKind))

    let private objects (element: JsonElement) (name: string) : JsonElement list =
        match prop element name with
        | None -> []
        | Some value when value.ValueKind = JsonValueKind.Array ->
            value.EnumerateArray()
            |> Seq.map (fun item ->
                if item.ValueKind = JsonValueKind.Object then
                    item
                else
                    raise (PolicyError(sprintf "'%s' must contain only objects; found %O" name item.ValueKind)))
            |> Seq.toList
        | Some value -> raise (PolicyError(sprintf "'%s' must be an array; found %O" name value.ValueKind))

    let private reason (element: JsonElement) : CM.JustificationReason =
        match requiredString element "reason" with
        | "measured" -> CM.Measured
        | "interoperability" -> CM.Interoperability
        | other ->
            raise (
                PolicyError(sprintf "unknown justification reason '%s' (expected 'measured' or 'interoperability')" other)
            )

    let private justification (element: JsonElement) : CM.ComplexityJustification =
        { Path = requiredString element "path"
          Symbol = requiredString element "symbol"
          Head = optionalString element "head"
          SourceDigest = optionalString element "sourceDigest"
          SimplerAlternative = optionalString element "simplerAlternative"
          Reason = reason element
          Evidence = optionalString element "evidence" }

    let private approvedPrimitive (element: JsonElement) : CM.ApprovedPrimitive =
        { Capability = requiredString element "capability"
          ApprovedSymbols = stringList element "approvedSymbols"
          CandidateSymbols = stringList element "candidateSymbols" }

    let private thresholds (root: JsonElement) : CM.ReviewThresholds =
        match prop root "thresholds" with
        | None ->
            { ModuleLines = None
              TypeLines = None
              MemberLines = None
              DependencyFanOut = None }
        | Some value when value.ValueKind = JsonValueKind.Object ->
            { ModuleLines = optionalInt value "moduleLines"
              TypeLines = optionalInt value "typeLines"
              MemberLines = optionalInt value "memberLines"
              DependencyFanOut = optionalInt value "dependencyFanOut" }
        | Some value -> raise (PolicyError(sprintf "'thresholds' must be an object; found %O" value.ValueKind))

    // ── Source expansion ─────────────────────────────────────────────────────────────────────────────
    // A declared entry is either one repo-relative `.fs` file or a directory prefix ending in `/`, which
    // contributes every `.fs` under it in sorted order. `obj/` and `bin/` outputs are excluded — they are
    // build products, not authored sources. A declared file that is not on disk is an ERROR, never a
    // silent omission: a policy that names a source the sweep cannot read has not been satisfied.

    let private isBuildOutput (relative: string) =
        relative.Split('/')
        |> Array.exists (fun segment ->
            String.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase)
            || String.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase))

    /// A generated document by naming convention (`*.g.fs`). `CodeChecks.analyze` drops these itself; the
    /// flag is carried so the pack — not this caller — remains the authority on what it analyses.
    let private isGenerated (relative: string) =
        relative.EndsWith(".g.fs", StringComparison.OrdinalIgnoreCase)

    // A DECLARATION GOVERNS ITS OWN REPOSITORY, AND THE PARSER ENFORCES THAT.
    //
    // Round 1 of independent review measured the hole this closes. Stripping leading `.`/`/` characters
    // and then `Path.Combine`-ing is NOT containment: it touches only the START of the entry, so a
    // MID-path `..` survives it and walks straight out of the tree. `"src/../../outside.fs"` was accepted
    // and analysed, and the DIRECTORY form was worse — `"src/../../outdir/"` yielded findings whose
    // `file` read `outdir/Out1.fs`, a governed-LOOKING path naming a file that is not in the repository,
    // so nothing downstream could see that the escape had happened.
    //
    // #366's and #369's sweeps cannot do this: they enumerate outward from `repo` and never resolve a
    // caller-supplied path. This pack takes declared paths, so it owns the check they never needed.
    //
    // The check is CANONICAL, not textual: resolve against the governed root and require the result to
    // stay under it. A rejection is the same `PolicyError` every other declaration defect raises, so it
    // fails closed through the existing Blocking input-state finding rather than silently dropping the entry.

    /// The governed root, canonicalized once and terminated with a separator, so a prefix test cannot
    /// match a SIBLING whose name merely starts with the root's (`/w/repo` must not admit `/w/repo-other`).
    let private governedRoot (repo: string) =
        let full = Path.GetFullPath repo

        if full.EndsWith(string Path.DirectorySeparatorChar, StringComparison.Ordinal) then
            full
        else
            full + string Path.DirectorySeparatorChar

    /// Resolve a declared entry against the governed root and REFUSE anything that lands outside it.
    /// Returns the canonical absolute path so the caller does not resolve it a second time (and cannot
    /// resolve it differently).
    let private contained (repo: string) (relative: string) : string =
        let root = governedRoot repo
        let candidate = Path.GetFullPath(Path.Combine(root, relative))

        let terminated =
            if candidate.EndsWith(string Path.DirectorySeparatorChar, StringComparison.Ordinal) then
                candidate
            else
                candidate + string Path.DirectorySeparatorChar

        if not (terminated.StartsWith(root, StringComparison.Ordinal)) then
            raise (
                PolicyError(
                    sprintf
                        "declared source '%s' resolves outside the repository; a declaration governs this repository's own sources only"
                        relative
                )
            )

        candidate

    /// Strip a leading `./` only. The old `TrimStart('.', '/')` also swallowed `../` and a leading `/`,
    /// silently REWRITING an escape into a different path instead of refusing it — `"../x"` became `"x"`.
    /// Leaving them intact lets `contained` see, and reject, what was actually written.
    let rec private stripLeadingCurrent (value: string) =
        if value.StartsWith("./", StringComparison.Ordinal) then
            stripLeadingCurrent (value.Substring 2)
        else
            value

    let private expand (repo: string) (entry: string) : string list =
        let normalized = entry.Replace('\\', '/') |> stripLeadingCurrent

        if normalized = "" then
            raise (PolicyError "'sources' contains an empty entry")
        elif normalized.EndsWith("/", StringComparison.Ordinal) then
            let directory = contained repo (normalized.TrimEnd('/'))

            if not (Directory.Exists directory) then
                raise (PolicyError(sprintf "declared source directory '%s' does not exist" normalized))

            Directory.EnumerateFiles(directory, "*.fs", SearchOption.AllDirectories)
            |> Seq.map (fun path -> Path.GetRelativePath(repo, path).Replace('\\', '/'))
            |> Seq.filter (isBuildOutput >> not)
            |> Seq.toList
        elif not (normalized.EndsWith(".fs", StringComparison.OrdinalIgnoreCase)) then
            raise (
                PolicyError(
                    sprintf
                        "declared source '%s' is neither an F# implementation file nor a directory prefix ending in '/'"
                        normalized
                )
            )
        elif not (File.Exists(contained repo normalized)) then
            raise (PolicyError(sprintf "declared source '%s' does not exist" normalized))
        else
            [ normalized ]

    let private documents (repo: string) (entries: string list) : CM.SourceDocument list =
        entries
        |> List.collect (expand repo)
        |> List.distinct
        |> List.sort
        |> List.map (fun relative ->
            { Path = relative
              Source = File.ReadAllText(Path.Combine(repo, relative))
              IsGenerated = isGenerated relative })

    /// Read the repository's declaration. `Ok None` means the pack is not applicable here (no policy file,
    /// or a policy that declares no sources — an empty declaration is still a declaration).
    let sense (repo: string) : Result<CM.AnalysisRequest option, string> =
        try
            let path = Path.Combine(repo, PolicyPath)

            if not (File.Exists path) then
                Ok None
            else
                use document = JsonDocument.Parse(File.ReadAllText path)
                let root = document.RootElement

                if root.ValueKind <> JsonValueKind.Object then
                    Error(sprintf "%s must contain a JSON object; found %O" PolicyPath root.ValueKind)
                else
                    match stringList root "sources" with
                    | [] -> Ok None
                    | sources ->
                        Ok(
                            Some
                                { Head = optionalString root "head"
                                  Documents = documents repo sources
                                  PureDomainPrefixes = stringList root "pureDomainPrefixes"
                                  Thresholds = thresholds root
                                  Justifications = objects root "justifications" |> List.map justification
                                  ApprovedPrimitives =
                                    objects root "approvedPrimitives" |> List.map approvedPrimitive }
                        )
        with
        | PolicyError detail -> Error(sprintf "%s is malformed: %s" PolicyPath detail)
        | ex -> Error(sprintf "%s could not be read: %s" PolicyPath ex.Message)

    /// The pack's contribution to the `fsgg verify` surface sweep. Every finding is normalized through
    /// `Profile.findingOf`, so it carries the composed profile's declared maturity for #368 and reaches
    /// `Enforcement.deriveEffectiveSeverity` through the same fold as every other surface finding.
    let findings (repo: string) : Result<SC.SurfaceFinding list, string> =
        match sense repo with
        | Error failure -> Error failure
        | Ok None -> Ok []
        | Ok(Some analysis) ->
            try
                let report =
                    FS.GG.Governance.CodeChecks.CodeChecks.analyze analysis |> Async.RunSynchronously

                report.Findings
                |> List.map (fun finding ->
                    CP.findingOf
                        CP.IdiomaticSimplicity
                        (CM.findingIdToken finding.Id)
                        request
                        (normalizePath finding.Path)
                        finding.Symbol
                        finding.Message)
                |> Ok
            with ex ->
                Error(sprintf "F# idiomatic-simplicity analysis threw: %s" ex.Message)
