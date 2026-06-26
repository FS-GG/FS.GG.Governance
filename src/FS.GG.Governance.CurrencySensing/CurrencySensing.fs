// The F070 generated-view currency EDGE SENSING (impure core). Visibility lives in CurrencySensing.fsi
// (Principle II); this file carries NO top-level access modifiers. It re-expresses the F057 refresh-host edge
// sensing (RefreshCommand.Interpreter's digest/lock helpers + Declaration's manifest node-walk) for the
// currency-only fields, so the verify/ship hosts can reuse the determination WITHOUT depending on the
// RefreshCommand host (the repo forbids command→command references). NO new staleness detection (FR-007).

namespace FS.GG.Governance.CurrencySensing

open System.IO
open System.Security.Cryptography
open System.Text
open YamlDotNet.RepresentationModel
open FS.GG.Governance.Config.Model
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.RefreshJson.RefreshModel

module CE = FS.GG.Governance.CurrencyEnforcement.CurrencyEnforcement

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CurrencySensing =

    // ── refresh.yml node helpers (parse-to-node only; hidden — the Declaration precedent) ──

    let asScalar (n: YamlNode) : string option =
        match n with
        | :? YamlScalarNode as s -> Option.ofObj s.Value
        | _ -> None

    let childByKey (m: YamlMappingNode) (name: string) : YamlNode option =
        m.Children
        |> Seq.tryPick (fun kv ->
            match kv.Key with
            | :? YamlScalarNode as k when k.Value = name -> Some kv.Value
            | _ -> None)

    let scalarField (m: YamlMappingNode) (name: string) : string option =
        childByKey m name |> Option.bind asScalar

    let scalarList (n: YamlNode) : string list =
        match n with
        | :? YamlSequenceNode as s -> s.Children |> Seq.choose asScalar |> List.ofSeq
        | _ -> []

    // Recognize the F014 Maturity tokens VERBATIM (the Config.Schema/Declaration vocabulary). Unknown ⇒ None
    // (the sensing degrades to advisory rather than throwing; a configured-but-typo'd dial simply won't block,
    // which the RefreshCommand.Declaration parser rejects loudly at refresh time — F070 T013).
    let recognizeMaturity (raw: string) : Maturity option =
        match raw.Trim() with
        | "observe" -> Some Observe
        | "warn" -> Some Warn
        | "block-on-pr" -> Some BlockOnPr
        | "block-on-ship" -> Some BlockOnShip
        | "block-on-release" -> Some BlockOnRelease
        | _ -> None

    let parseEntry (n: YamlNode) : GenerationEntry option =
        match n with
        | :? YamlMappingNode as m ->
            match scalarField m "id" |> Option.map (fun s -> s.Trim()) with
            | Some viewId when viewId <> "" ->
                { ViewId = viewId
                  Kind = scalarField m "kind" |> Option.map viewKindOfToken |> Option.defaultValue (Other "")
                  // OutputPath/Generator are NOT used by the currency decision — left empty here.
                  OutputPath = ""
                  Sources =
                    childByKey m "sources"
                    |> Option.map scalarList
                    |> Option.defaultValue []
                  Generator = []
                  GeneratorBasis = scalarField m "generatorBasis" |> Option.defaultValue "" }
                |> Some
            | _ -> None
        | _ -> None

    let parseManifest (lines: string list) : (Maturity option * GenerationEntry list) option =
        try
            let stream = YamlStream()
            stream.Load(new StringReader(String.concat "\n" lines))

            if stream.Documents.Count = 0 then
                Some(None, [])
            else
                match stream.Documents.[0].RootNode with
                | :? YamlMappingNode as root ->
                    let dial = scalarField root "currency-enforcement" |> Option.bind recognizeMaturity

                    let entries =
                        childByKey root "views"
                        |> Option.map (fun n ->
                            match n with
                            | :? YamlSequenceNode as s -> s.Children |> Seq.choose parseEntry |> List.ofSeq
                            | _ -> [])
                        |> Option.defaultValue []

                    Some(dial, entries)
                | _ -> None
        with _ ->
            None

    // ── provenance lock + source digesting (hidden) — the RefreshCommand.Interpreter precedent ──

    let sha256Hex (bytes: byte[]) : string =
        use sha = SHA256.Create()
        sha.ComputeHash bytes |> Array.map (fun b -> b.ToString("x2")) |> String.concat ""

    // Digest a declared source: a file's bytes, or a directory's sorted relative-path:hash combination.
    let digestPath (full: string) (rel: string) : Result<string, string> =
        if File.Exists full then
            Ok(sha256Hex (File.ReadAllBytes full))
        elif Directory.Exists full then
            let files = Directory.GetFiles(full, "*", SearchOption.AllDirectories) |> Array.sort

            let combined =
                files
                |> Array.map (fun f ->
                    let rp = Path.GetRelativePath(full, f).Replace('\\', '/')
                    rp + ":" + sha256Hex (File.ReadAllBytes f))
                |> String.concat "\n"

            Ok(sha256Hex (Encoding.UTF8.GetBytes combined))
        else
            Error(sprintf "source not found: %s" rel)

    // Read `.fsgg/refresh.lock.json` into a per-view (covered-artifact hashes, generator version) map.
    let readLock (repo: string) : Map<string, ArtifactHash list * GeneratorVersion> =
        try
            let path = Path.Combine(repo, ".fsgg", "refresh.lock.json")

            if not (File.Exists path) then
                Map.empty
            else
                use doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText path)

                let str (e: System.Text.Json.JsonElement) : string =
                    match e.GetString() with
                    | null -> ""
                    | s -> s

                match doc.RootElement.TryGetProperty "views" with
                | true, views ->
                    [ for p in views.EnumerateObject() ->
                          let v = p.Value

                          let srcs =
                              match v.TryGetProperty "sources" with
                              | true, a -> [ for e in a.EnumerateArray() -> ArtifactHash(str e) ]
                              | _ -> []

                          let gen =
                              match v.TryGetProperty "generatorVersion" with
                              | true, g -> str g
                              | _ -> ""

                          p.Name, (srcs, GeneratorVersion gen) ]
                    |> Map.ofList
                | _ -> Map.empty
        with _ ->
            Map.empty

    // Sense a view's CURRENT (covered-artifact hashes, generator version) by digesting its declared sources.
    let senseEntry (repo: string) (entry: GenerationEntry) : Result<ArtifactHash list * GeneratorVersion, string> =
        let rec go (acc: ArtifactHash list) (sources: string list) : Result<ArtifactHash list, string> =
            match sources with
            | [] -> Ok(List.rev acc)
            | rel :: rest ->
                match digestPath (Path.Combine(repo, rel)) rel with
                | Ok h -> go (ArtifactHash h :: acc) rest
                | Error e -> Error e

        match go [] entry.Sources with
        | Ok digests -> Ok(digests, GeneratorVersion entry.GeneratorBasis)
        | Error e -> Error e

    let senseRepo (repo: string) : CE.CurrencyFinding list =
        try
            let refreshYml = Path.Combine(repo, ".fsgg", "refresh.yml")

            if not (File.Exists refreshYml) then
                []
            else
                match parseManifest (File.ReadAllLines refreshYml |> List.ofArray) with
                | None -> []
                | Some(dial, entries) ->
                    let lockMap = readLock repo

                    let decisions =
                        entries
                        |> List.map (fun entry ->
                            let recorded = Map.tryFind entry.ViewId lockMap
                            CE.decideCurrency entry recorded (senseEntry repo entry))

                    CE.findingsOf dial decisions
        with _ ->
            []
