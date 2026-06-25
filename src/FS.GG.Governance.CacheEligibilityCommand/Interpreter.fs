// The EDGE interpreter of the `fsgg cache-eligibility` host command (F044) — the ONLY impure code in the
// feature. Visibility lives in Interpreter.fsi (Principle II). It executes the `Loop.Effect`s the pure
// `update` requests against INJECTED, FAKEABLE ports and feeds each result back as a `Loop.Msg`. It REUSES
// `Config.Loader` for catalog reads (F014) and `Snapshot.Interpreter` for git sensing + base/head (F016),
// adding the new `FreshnessSensor` sensing, the read-only `StoreReader`, the atomic `Write`, and the `Out`
// sink. TOTAL and SAFE (FR-010/FR-013): every port `Error` and every thrown exception is caught and reified
// to the matching `Msg` — it NEVER throws and (via temp+rename) NEVER leaves a partial artifact. It assembles
// `SensedFacts` from `RepoSnapshot.Range` (base/head) + the `FreshnessSensor`, NEVER fabricating an unsensed
// fact (D4/L3).

namespace FS.GG.Governance.CacheEligibilityCommand

open System
open System.IO
open System.Text
open System.Text.Json
open System.Security.Cryptography
open FS.GG.Governance.Config // Loader, Schema
open FS.GG.Governance.Config.Model // GovernedPath, Validation, Invalid, Diagnostic, DiagnosticId, FsggFile, Locator, CheckId, DomainId, CommandId, EnvironmentClass
open FS.GG.Governance.Snapshot.Model // GitRef, SnapshotOptions, RepoSnapshot, CommitId, sensingDiagnosticIdToken
open FS.GG.Governance.FreshnessKey.Model // RuleHash, ArtifactHash, CommandVersion, GeneratorVersion, Revision, FreshnessInputs
open FS.GG.Governance.FreshnessResolution.Model // SensedFacts
open FS.GG.Governance.EvidenceReuse // empty
open FS.GG.Governance.EvidenceReuse.Model // ReuseStore, RecordedEvidence, EvidenceRef
open FS.GG.Governance.HumanText // RenderMode (selectMode), ReportView (F27 wiring 063 US2)
open FS.GG.Governance.HumanRender // Capability.senseCapability, RichRender.emitStdout (Spectre confined here)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    type FreshnessSensor =
        { SenseRuleHash: unit -> RuleHash option
          SenseGeneratorVersion: unit -> GeneratorVersion option
          SenseCoveredArtifacts: FS.GG.Governance.Gates.Model.Gate -> ArtifactHash list option
          SenseCommandVersion: CommandId -> CommandVersion option }

    type StoreReader = string -> Result<ReuseStore option, string>

    type Ports =
        { Files: Loader.FileReader
          Git: FS.GG.Governance.Snapshot.Ports
          Freshness: FreshnessSensor
          Store: StoreReader
          Write: string -> string -> Result<unit, string>
          Out: string -> unit
          SenseCapability: bool -> RenderMode.ColorCapability
          RenderReport: ReportView.ReportView -> unit }

    // ── safety helpers (mirror RouteCommand) ──

    // Run a port call, converting BOTH an `Error` and a thrown exception into `Error` so the interpreter
    // never throws out of itself (FR-010/FR-013).
    let guard (call: unit -> Result<'a, string>) : Result<'a, string> =
        try
            call ()
        with e ->
            Error e.Message

    // The real persistence port: create parent dirs, write to a unique temp sibling, then atomically rename
    // over the target — a failed write leaves NO partial/truncated file (FR-010).
    let writeAtomic (path: string) (content: string) : Result<unit, string> =
        try
            match Path.GetDirectoryName path with
            | null
            | "" -> ()
            | dir -> Directory.CreateDirectory dir |> ignore

            let tmp = path + ".tmp-" + Guid.NewGuid().ToString("N")
            File.WriteAllText(tmp, content)
            File.Move(tmp, path, true)
            Ok()
        with e ->
            Error e.Message

    // ── real BCL-crypto freshness sensing (the only genuinely new sensing) ──

    let sha256Hex (bytes: byte[]) : string =
        use sha = SHA256.Create()
        sha.ComputeHash bytes |> Array.map (fun b -> b.ToString("x2")) |> String.concat ""

    // The rule-pack hash: a SHA-256 over the repo's `.fsgg/*.yml` catalog bytes (filename + content, sorted),
    // so it is content-addressed and working-directory independent. `None` when no catalog is present.
    let senseCatalogHash (repo: string) : string option =
        let dir = Path.Combine(repo, ".fsgg")

        if not (Directory.Exists dir) then
            None
        else
            let files =
                Directory.GetFiles(dir, "*.yml", SearchOption.TopDirectoryOnly)
                |> Array.sortWith (fun a b -> String.CompareOrdinal(a, b))

            if files.Length = 0 then
                None
            else
                let nameBytes (f: string) =
                    match Path.GetFileName f with
                    | null -> Array.empty<byte>
                    | n -> Encoding.UTF8.GetBytes n

                let bytes =
                    files
                    |> Array.collect (fun f -> Array.append (nameBytes f) (File.ReadAllBytes f))

                Some(sha256Hex bytes)

    // The covered-artifact hashes: a content SHA-256 per file under the repo's `src/**` package surface,
    // ordinal-sorted. MVP scope — every gate shares the repo surface; finer PER-GATE scoping is a documented
    // later refinement. Content-addressed ⇒ working-directory independent. A missing `src/` ⇒ `[]`
    // (sensed-empty, a legitimate resolved value — L4), distinct from the `None` an unsensable surface yields.
    let senseSrcHashes (repo: string) : ArtifactHash list =
        let srcDir = Path.Combine(repo, "src")

        if not (Directory.Exists srcDir) then
            []
        else
            Directory.GetFiles(srcDir, "*", SearchOption.AllDirectories)
            |> Array.sortWith (fun a b -> String.CompareOrdinal(a, b))
            |> Array.map (fun f -> ArtifactHash(sha256Hex (File.ReadAllBytes f)))
            |> Array.toList

    let toolVersion () : string =
        let asm = System.Reflection.Assembly.GetExecutingAssembly()

        match asm.GetName().Version with
        | null -> "0.0.0"
        | v -> v.ToString()

    // ── read-only evidence-reuse store deserializer (fsgg.evidence-reuse-store/v1, artifacts §A5) ──

    let storeSchemaVersion = "fsgg.evidence-reuse-store/v1"

    let parseEnv (s: string) : EnvironmentClass =
        match s with
        | "local" -> EnvironmentClass.Local
        | "ci" -> EnvironmentClass.Ci
        | "local-or-ci" -> EnvironmentClass.LocalOrCi
        | "release" -> EnvironmentClass.Release
        | other -> failwithf "unknown environment class: %s" other

    let reqStr (el: JsonElement) (name: string) : string =
        match el.TryGetProperty name with
        | true, v when v.ValueKind = JsonValueKind.String ->
            match v.GetString() with
            | null -> failwithf "field %s is null" name
            | s -> s
        | _ -> failwithf "missing or non-string field: %s" name

    let optStr (el: JsonElement) (name: string) : string option =
        match el.TryGetProperty name with
        | true, v ->
            match v.ValueKind with
            | JsonValueKind.Null -> None
            | JsonValueKind.String -> v.GetString() |> Option.ofObj
            | _ -> failwithf "field %s must be string or null" name
        | _ -> None

    let strArr (el: JsonElement) (name: string) : string list =
        match el.TryGetProperty name with
        | true, v when v.ValueKind = JsonValueKind.Array ->
            [ for x in v.EnumerateArray() ->
                  match x.ValueKind with
                  | JsonValueKind.String ->
                      match x.GetString() with
                      | null -> failwithf "null element in %s" name
                      | s -> s
                  | _ -> failwithf "non-string element in %s" name ]
        | _ -> failwithf "missing or non-array field: %s" name

    let parseEntry (el: JsonElement) : RecordedEvidence =
        // Built via the public F029/F030 constructors only — computes NO hash/key/digest (FR-013); the
        // opaque newtype strings are taken verbatim from the document.
        let inputs: FreshnessInputs =
            { Check = CheckId(reqStr el "check")
              Domain = DomainId(reqStr el "domain")
              Command = optStr el "command" |> Option.map CommandId
              Environment = parseEnv (reqStr el "environment")
              RuleHash = RuleHash(reqStr el "ruleHash")
              CoveredArtifacts = strArr el "coveredArtifacts" |> List.map ArtifactHash
              CommandVersion = optStr el "commandVersion" |> Option.map CommandVersion
              GeneratorVersion = GeneratorVersion(reqStr el "generatorVersion")
              Base = Revision(reqStr el "base")
              Head = Revision(reqStr el "head") }

        { Inputs = inputs
          Evidence = EvidenceRef(reqStr el "evidence") }

    let parseStore (json: string) : Result<ReuseStore, string> =
        try
            use doc = JsonDocument.Parse json
            let root = doc.RootElement

            let schema =
                match root.TryGetProperty "schemaVersion" with
                | true, v when v.ValueKind = JsonValueKind.String -> v.GetString()
                | _ -> failwith "missing schemaVersion"

            if schema <> storeSchemaVersion then
                failwithf "unknown store schema: %s" schema

            let recorded =
                match root.TryGetProperty "recorded" with
                | true, v when v.ValueKind = JsonValueKind.Array -> [ for el in v.EnumerateArray() -> parseEntry el ]
                | _ -> failwith "missing recorded array"

            Ok(ReuseStore recorded)
        with e ->
            Error e.Message

    let realStoreReader: StoreReader =
        fun path ->
            try
                if not (File.Exists path) then
                    Ok None
                else
                    parseStore (File.ReadAllText path) |> Result.map Some
            with e ->
                Error e.Message

    // ── step — execute one Effect, reify the result as a Msg; TOTAL and SAFE ──

    let step (ports: Ports) (effect: Loop.Effect) : Loop.Msg =
        match effect with
        | Loop.SenseScope scope ->
            let options =
                match scope with
                | Loop.Since rev -> { Since = Some(GitRef rev); Base = None; Head = None }
                | Loop.ExplicitPaths _
                | Loop.DefaultRange -> { Since = None; Base = None; Head = None }

            let result =
                try
                    let snap = FS.GG.Governance.Snapshot.Interpreter.senseSnapshot ports.Git options
                    // senseSnapshot NEVER throws; a failure surfaces as a SensingDiagnostic ⇒ InputUnavailable.
                    match snap.Diagnostics with
                    | [] -> Ok snap
                    | ds ->
                        ds
                        |> List.map (fun d -> sprintf "%s: %s" (sensingDiagnosticIdToken d.Id) d.Message)
                        |> String.concat "; "
                        |> Error
                with e ->
                    Error e.Message

            Loop.Sensed result

        | Loop.LoadCatalog _ ->
            let validation =
                try
                    Loader.readSource (GovernedPath ".") ports.Files |> Schema.validate
                with e ->
                    Invalid
                        [ { Id = MissingRequiredFile
                            File = Project
                            Locator = { Field = None; Id = None; Line = None }
                            Message = "catalog read failed: " + e.Message } ]

            Loop.Loaded validation

        | Loop.SenseFreshness(gates, (baseOpt, headOpt)) ->
            // Assemble SensedFacts from base/head (passed through from RepoSnapshot.Range) + the
            // FreshnessSensor over each selected gate / declared command. A present Map key means SENSED
            // (even when the value is empty); an absent key means NOT SENSED — never fabricated (L3/L4).
            let result =
                try
                    let ruleHash = ports.Freshness.SenseRuleHash()
                    let genVer = ports.Freshness.SenseGeneratorVersion()

                    let covered =
                        gates
                        |> List.choose (fun g -> ports.Freshness.SenseCoveredArtifacts g |> Option.map (fun hs -> g.Id, hs))
                        |> Map.ofList

                    let commandVersions =
                        gates
                        |> List.choose (fun g -> g.FreshnessKey.Command)
                        |> List.distinct
                        |> List.choose (fun cid -> ports.Freshness.SenseCommandVersion cid |> Option.map (fun v -> cid, v))
                        |> Map.ofList

                    let facts: SensedFacts =
                        { RuleHash = ruleHash
                          GeneratorVersion = genVer
                          Base = baseOpt
                          Head = headOpt
                          CoveredArtifacts = covered
                          CommandVersions = commandVersions }

                    Ok facts
                with e ->
                    Error e.Message

            Loop.FreshnessSensed result

        | Loop.LoadStore path ->
            let result =
                try
                    match ports.Store path with
                    | Ok None -> Ok EvidenceReuse.empty // absent ⇒ empty (FR-006)
                    | Ok(Some store) -> Ok store
                    | Error e -> Error e
                with e ->
                    Error e.Message

            Loop.StoreLoaded result

        | Loop.WriteArtifact(kind, path, content) -> Loop.Wrote(kind, guard (fun () -> ports.Write path content))

        // F27 wiring (063) US2: the render-mode dispatch lives HERE at the edge (FR-004). Json (human = None)
        // and the ANSI-free Plain path go via the existing `Out` sink (byte-stable, captured in tests); only
        // the interactive `Rich` path goes through `RenderReport` (Spectre, confined to HumanRender) followed
        // by the operational lines. The mode is `selectMode false (senseCapability explicitPlain)`.
        | Loop.EmitSummary(text, human, explicitPlain) ->
            match human with
            | None -> ports.Out text
            | Some(view, operational) ->
                match RenderMode.selectMode false (ports.SenseCapability explicitPlain) with
                | RenderMode.Rich ->
                    ports.RenderReport view
                    if operational <> "" then ports.Out operational
                | RenderMode.Plain
                | RenderMode.Json -> ports.Out text

            Loop.Emitted

    // ── realPorts — wire the real edges ──

    let realPorts (repo: string) : Ports =
        // Sense the rule pack + the covered surface ONCE; closed over by the sensor (deterministic).
        let catalogHash = senseCatalogHash repo
        let covered = senseSrcHashes repo
        let gv = toolVersion ()

        let sensor =
            { SenseRuleHash = fun () -> catalogHash |> Option.map RuleHash
              SenseGeneratorVersion = fun () -> Some(GeneratorVersion gv)
              // MVP: a gate covers the repo's `src/**` surface (finer per-gate scoping deferred).
              SenseCoveredArtifacts = fun _gate -> Some covered
              // MVP coarse command version: a short digest of the command id stamped against the rule pack
              // it is declared in (changes when the rule pack changes). Cheap, real, deterministic; richer
              // command-version sensing is a later refinement. `None` when no catalog (unsensed, no-hide).
              SenseCommandVersion =
                fun (CommandId c) ->
                    catalogHash
                    |> Option.map (fun h -> CommandVersion((sha256Hex (Encoding.UTF8.GetBytes(c + "@" + h))).Substring(0, 12))) }

        { Files = Loader.fileSystemReader repo
          Git = FS.GG.Governance.Snapshot.Interpreter.realPorts repo
          Freshness = sensor
          Store = realStoreReader
          Write = writeAtomic
          Out = fun text -> Console.Out.WriteLine text
          SenseCapability = Capability.senseCapability
          RenderReport = fun view -> RichRender.emitStdout RenderMode.Rich view "" }

    // ── run — drive init → update* to Done ──

    let run (ports: Ports) (request: Loop.RunRequest) : Loop.Model =
        let m0, eff0 = Loop.init request

        let rec drive (model: Loop.Model) (effects: Loop.Effect list) : Loop.Model =
            if model.Phase = Loop.Done then
                model
            else
                match effects with
                | [] -> model
                | _ ->
                    let model2, newEffects =
                        effects
                        |> List.map (step ports)
                        |> List.fold
                            (fun (m, acc) msg ->
                                let m2, e2 = Loop.update msg m
                                m2, acc @ e2)
                            (model, [])

                    drive model2 newEffects

        drive m0 eff0
