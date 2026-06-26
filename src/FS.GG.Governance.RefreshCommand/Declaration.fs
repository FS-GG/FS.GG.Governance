// The row-local `.fsgg/refresh.yml` generation-manifest adapter (F057). Visibility lives in
// Declaration.fsi (Principle II) — this file carries NO top-level access modifiers; the YamlDotNet node
// helpers and the kind recognizer are hidden by their absence from the .fsi. It parses the new
// generation-manifest surface into the shared `RefreshModel` `GenerationManifest` WITHOUT editing F014
// `Config`'s frozen four-file schema. YamlDotNet is used in parse-to-node mode only (the F014 `Schema.fs` /
// F055 `release.yml` precedent — NO new dependency). PURE and TOTAL: a malformed/absent value is an
// `Error DeclError`, never an exception and never partial facts. Every value is read from the file
// (product-neutral, FR-011).

namespace FS.GG.Governance.RefreshCommand

open YamlDotNet.RepresentationModel
open FS.GG.Governance.Config.Model            // Maturity (F070 currency-enforcement dial)
open FS.GG.Governance.RefreshJson.RefreshModel

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Declaration =

    // ── YamlDotNet node helpers (parse-to-node only; hidden — absent from Declaration.fsi) ──

    /// Parse the joined content to a single mapping-node root; an empty document, a non-mapping root, or a
    /// YamlDotNet parse error (e.g. a duplicate key) is an `Error` (the F014 `Schema.loadRoot` precedent).
    let loadRoot (content: string) : Result<YamlMappingNode, string> =
        try
            let stream = YamlStream()
            stream.Load(new System.IO.StringReader(content))

            if stream.Documents.Count = 0 then
                Error "refresh.yml is empty"
            else
                match stream.Documents.[0].RootNode with
                | :? YamlMappingNode as m -> Ok m
                | _ -> Error "refresh.yml root must be a mapping"
        with ex ->
            Error(sprintf "refresh.yml is not valid YAML: %s" ex.Message)

    let asScalar (n: YamlNode) : string option =
        match n with
        | :? YamlScalarNode as s -> Option.ofObj s.Value
        | _ -> None

    let asMapping (n: YamlNode) : YamlMappingNode option =
        match n with
        | :? YamlMappingNode as m -> Some m
        | _ -> None

    let asSeq (n: YamlNode) : YamlNode list option =
        match n with
        | :? YamlSequenceNode as s -> Some(List.ofSeq s.Children)
        | _ -> None

    /// The value of a scalar-keyed mapping entry by name, if present.
    let childByKey (m: YamlMappingNode) (name: string) : YamlNode option =
        m.Children
        |> Seq.tryPick (fun kv ->
            match kv.Key with
            | :? YamlScalarNode as k when k.Value = name -> Some kv.Value
            | _ -> None)

    let scalarField (m: YamlMappingNode) (name: string) : string option =
        childByKey m name |> Option.bind asScalar

    /// A sequence of scalars as a string list; a non-sequence or a non-scalar element ⇒ `None` (malformed).
    let scalarList (n: YamlNode) : string list option =
        match asSeq n with
        | None -> None
        | Some items ->
            let vals = items |> List.choose asScalar
            if vals.Length = items.Length then Some vals else None

    // ── per-entry parser (hidden) ── (kind recognition reuses the shared `RefreshModel.viewKindOfToken`)

    let private nonEmpty (label: string) (value: string) : Result<string, string> =
        if System.String.IsNullOrWhiteSpace value then
            Error(sprintf "a views[] entry has an empty '%s'" label)
        else
            Ok value

    let parseEntry (n: YamlNode) : Result<GenerationEntry, string> =
        match asMapping n with
        | None -> Error "each views[] entry must be a mapping with id/kind/output/sources/generator/generatorBasis"
        | Some m ->
            match scalarField m "id" |> Option.map (fun s -> s.Trim()) with
            | None -> Error "a views[] entry is missing its 'id'"
            | Some idRaw ->
                match nonEmpty "id" idRaw with
                | Error e -> Error e
                | Ok viewId ->
                    match scalarField m "kind" with
                    | None -> Error(sprintf "view '%s' is missing its 'kind'" viewId)
                    | Some kindRaw ->
                        match scalarField m "output" |> Option.bind (fun s -> match nonEmpty "output" s with Ok v -> Some v | Error _ -> None) with
                        | None -> Error(sprintf "view '%s' is missing/empty its 'output'" viewId)
                        | Some output ->
                            // `sources` is optional (an absent/empty sequence ⇒ a source-less view); a present
                            // non-sequence or non-scalar element is malformed.
                            let sourcesResult =
                                match childByKey m "sources" with
                                | None -> Ok []
                                | Some node ->
                                    match scalarList node with
                                    | Some xs -> Ok xs
                                    | None -> Error(sprintf "view '%s' has a malformed 'sources' (expected a list of paths)" viewId)

                            match sourcesResult with
                            | Error e -> Error e
                            | Ok sources ->
                                match childByKey m "generator" |> Option.bind scalarList with
                                | None -> Error(sprintf "view '%s' is missing/malformed its 'generator' (expected a non-empty command list)" viewId)
                                | Some [] -> Error(sprintf "view '%s' has an empty 'generator' command" viewId)
                                | Some generator ->
                                    match scalarField m "generatorBasis" |> Option.bind (fun s -> match nonEmpty "generatorBasis" s with Ok v -> Some v | Error _ -> None) with
                                    | None -> Error(sprintf "view '%s' is missing/empty its 'generatorBasis'" viewId)
                                    | Some basis ->
                                        Ok
                                            { ViewId = viewId
                                              Kind = viewKindOfToken kindRaw
                                              OutputPath = output
                                              Sources = sources
                                              Generator = generator
                                              GeneratorBasis = basis }

    /// Fold a `Result` list into a `Result` of the list, first error wins (total, no exceptions).
    let sequenceResults (rs: Result<'a, string> list) : Result<'a list, string> =
        (Ok [], rs)
        ||> List.fold (fun acc r ->
            match acc, r with
            | Error e, _ -> Error e
            | Ok xs, Ok x -> Ok(xs @ [ x ])
            | Ok _, Error e -> Error e)

    // ── F070 manifest-level currency-enforcement dial (hidden) ──
    // Recognizes the canonical F014 Maturity tokens VERBATIM (the Config.Schema.parseMaturity vocabulary,
    // re-expressed here since that helper is private to Config). An unknown value is an Error — never silently
    // dropped (it would otherwise read as "advisory"). Absent key ⇒ None (opt-in / byte-identity).
    let recognizeMaturity (raw: string) : Result<Maturity, string> =
        match raw.Trim() with
        | "observe" -> Ok Observe
        | "warn" -> Ok Warn
        | "block-on-pr" -> Ok BlockOnPr
        | "block-on-ship" -> Ok BlockOnShip
        | "block-on-release" -> Ok BlockOnRelease
        | other ->
            Error(
                sprintf
                    "refresh.yml 'currency-enforcement' has an unknown value '%s' (expected one of: observe, warn, block-on-pr, block-on-ship, block-on-release)"
                    other
            )

    // ── the public entry point ──

    let parse (lines: string list) : Result<GenerationManifest, DeclError> =
        let content = String.concat "\n" lines

        let result =
            match loadRoot content with
            | Error e -> Error e
            | Ok root ->
                // An absent `views` key OR an empty sequence both yield the empty manifest (FR-012). A
                // present non-sequence `views` is malformed.
                let entriesNode =
                    match childByKey root "views" with
                    | None -> Ok []
                    | Some node ->
                        match asSeq node with
                        | Some xs -> Ok xs
                        | None -> Error "refresh.yml 'views' must be a sequence"

                match entriesNode with
                | Error e -> Error e
                | Ok nodes ->
                    match nodes |> List.map parseEntry |> sequenceResults with
                    | Error e -> Error e
                    | Ok entries ->
                        let ids = entries |> List.map (fun e -> e.ViewId)
                        let duplicated = ids |> List.countBy id |> List.filter (fun (_, n) -> n > 1) |> List.map fst

                        if not (List.isEmpty duplicated) then
                            Error(sprintf "refresh.yml declares a view id more than once (%s)" (String.concat ", " duplicated))
                        else
                            // F070: an absent `currency-enforcement` key ⇒ None (opt-in); a present unknown
                            // value is a hard error, not a silent advisory default.
                            match scalarField root "currency-enforcement" with
                            | None -> Ok { Entries = entries; CurrencyEnforcement = None }
                            | Some raw ->
                                match recognizeMaturity raw with
                                | Error e -> Error e
                                | Ok maturity ->
                                    Ok
                                        { Entries = entries
                                          CurrencyEnforcement = Some maturity }

        result |> Result.mapError (fun reason -> { Reason = reason })
