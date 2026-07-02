// The EDGE interpreter of the `fsgg refresh` host command (F057) — the ONLY impure code in the feature.
// Visibility lives in Interpreter.fsi (Principle II). It executes the `Loop.Effect`s the pure `update`
// requests against INJECTED, FAKEABLE ports and feeds each result back as a `Loop.Msg`. It REUSES the
// existing edges verbatim — `Config.Loader` for the `.fsgg/refresh.yml` read (F014) and the F051
// `GateExecution.Interpreter.realPort` process port for generator runs — adding only the per-source SHA-256
// digester, the recorded-provenance lock reader/writer, the atomic artifact writer, and a stdout sink. TOTAL
// and SAFE: every port `Error` and every thrown exception is caught and reified to the matching `Msg` — it
// NEVER throws and (via temp+rename) NEVER leaves a partial artifact. NETWORK-FREE: every port reaches only
// local files via `System.IO` and the F051 process port.

namespace FS.GG.Governance.RefreshCommand

open System
open System.IO
open System.Text
open System.Text.Json
open System.Security.Cryptography
open FS.GG.Governance.Config                       // Loader
open FS.GG.Governance.CommandRecord.Model           // Executable, Argument, WorkingDirectory, EnvironmentDelta, ExitCode, CapturedOutput
open FS.GG.Governance.Config.Model                  // TimeoutLimit
open FS.GG.Governance.GateExecution.Model            // GateCommand
open FS.GG.Governance.FreshnessKey.Model             // ArtifactHash, GeneratorVersion
open FS.GG.Governance.RefreshJson.RefreshModel       // GenerationEntry

open FS.GG.Governance.JsonText // 073: the shared deterministic-emit helper JsonText.writeToString
open FS.GG.Governance.CommandHost           // 049: shared host-loop combinators (guard/drive)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    type Ports =
        { Files: Loader.FileReader
          Sense: GenerationEntry -> Result<ArtifactHash list * GeneratorVersion, string>
          ReadProv: string -> (ArtifactHash list * GeneratorVersion) option
          Generate: GenerationEntry -> Result<ArtifactHash, string>
          WriteProv: string -> (ArtifactHash list * GeneratorVersion * ArtifactHash) -> Result<unit, string>
          Write: string -> string -> Result<unit, string>
          Out: string -> unit }

    // ── shared helpers (hidden — absent from Interpreter.fsi) ──

    /// A non-null string from a `JsonElement.GetString()` (Nullable=enable safe).
    let str (e: JsonElement) : string =
        match e.GetString() with
        | null -> ""
        | s -> s

    let sha256Hex (bytes: byte[]) : string =
        use sha = SHA256.Create()
        sha.ComputeHash bytes |> Array.map (fun b -> b.ToString("x2")) |> String.concat ""

    /// Digest a declared source PATH content. A file digests its bytes; a directory digests the
    /// ordinal-sorted sequence of (relative-path, per-file digest) pairs (deterministic, content-based, never
    /// mtime/size — FR-002). An absent path is an `Error` (⇒ stale-unresolved, never a fabricated digest).
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

    // ── Sense: per-source digest + generator version (research D2) ──

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

    // ── Generate: run the declared generator via the F051 process port (research D3) ──

    let generateEntry (repo: string) (entry: GenerationEntry) : Result<ArtifactHash, string> =
        match entry.Generator with
        | [] -> Error "empty generator command"
        | exe :: args ->
            let command: GateCommand =
                { Executable = Executable exe
                  Arguments = args |> List.map Argument
                  WorkingDirectory = WorkingDirectory repo
                  Environment = { Added = []; Changed = []; Removed = [] }
                  Timeout = TimeoutLimit 300
                  CapturedOutput = NoCapturedOutput }

            let outcome = FS.GG.Governance.GateExecution.Interpreter.realPort command
            let (ExitCode code) = outcome.ExitCode

            if code <> 0 then
                let stderr = (Encoding.UTF8.GetString outcome.Stderr).Trim()
                Error(sprintf "generator exited %d%s" code (if stderr = "" then "" else ": " + stderr))
            else
                // Re-digest the produced output — its digest is recorded as provenance's output digest.
                match digestPath (Path.Combine(repo, entry.OutputPath)) entry.OutputPath with
                | Ok h -> Ok(ArtifactHash h)
                | Error e -> Error(sprintf "generator produced no output at %s (%s)" entry.OutputPath e)

    // ── recorded-provenance lock (generated companion — research D4) ──
    //
    // T024 decision: a MINIMAL deterministic row-local lock at `<repo>/.fsgg/refresh.lock.json` (sorted view
    // ids, no clock / no absolute path). The F048 `EvidenceReuseStore` serialization is per-CHANGE
    // (Base/Head-keyed) and does not fit the revision-INDEPENDENT view-currency triple (source digests +
    // generator version + output digest, keyed by view id), so a row-local lock is used instead.

    let lockPath (repo: string) : string =
        Path.Combine(repo, ".fsgg", "refresh.lock.json")

    /// Read the whole lock into a per-view map of (source digests, generator version, output digest). A
    /// missing/unreadable lock is the empty map (a view with no prior provenance ⇒ first generation).
    let readLock (path: string) : Map<string, string list * string * string> =
        try
            if not (File.Exists path) then
                Map.empty
            else
                use doc = JsonDocument.Parse(File.ReadAllText path)

                match doc.RootElement.TryGetProperty "views" with
                | true, views ->
                    [ for p in views.EnumerateObject() ->
                          let v = p.Value

                          let srcs =
                              match v.TryGetProperty "sources" with
                              | true, a -> [ for e in a.EnumerateArray() -> str e ]
                              | _ -> []

                          let gen =
                              match v.TryGetProperty "generatorVersion" with
                              | true, g -> str g
                              | _ -> ""

                          let out =
                              match v.TryGetProperty "output" with
                              | true, o -> str o
                              | _ -> ""

                          p.Name, (srcs, gen, out) ]
                    |> Map.ofList
                | _ -> Map.empty
        with _ ->
            Map.empty

    let renderLock (views: Map<string, string list * string * string>) : string =
        JsonText.writeToString (fun w ->
            w.WriteStartObject()
            w.WriteString("schemaVersion", "fsgg.refresh-lock/v1")
            w.WritePropertyName "views"
            w.WriteStartObject()

            for (viewId, (srcs, gen, out)) in (views |> Map.toList |> List.sortBy fst) do
                w.WritePropertyName viewId
                w.WriteStartObject()
                w.WritePropertyName "sources"
                w.WriteStartArray()

                for s in srcs do
                    w.WriteStringValue s

                w.WriteEndArray()
                w.WriteString("generatorVersion", gen)
                w.WriteString("output", out)
                w.WriteEndObject()

            w.WriteEndObject()
            w.WriteEndObject())

    let readProv (repo: string) (viewId: string) : (ArtifactHash list * GeneratorVersion) option =
        match Map.tryFind viewId (readLock (lockPath repo)) with
        | Some(srcs, gen, _out) -> Some(srcs |> List.map ArtifactHash, GeneratorVersion gen)
        | None -> None

    /// The real persistence port: create parent dirs, write to a unique temp sibling, then atomically rename
    /// over the target — a failed write leaves NO partial/truncated file (FR-013).
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

    let writeProv (repo: string) (viewId: string) (provenance: ArtifactHash list * GeneratorVersion * ArtifactHash) : Result<unit, string> =
        try
            let digests, generator, output = provenance
            let (GeneratorVersion gen) = generator
            let (ArtifactHash out) = output
            let srcs = digests |> List.map (fun (ArtifactHash h) -> h)
            let path = lockPath repo
            let updated = Map.add viewId (srcs, gen, out) (readLock path)
            writeAtomic path (renderLock updated)
        with e ->
            Error e.Message

    // ── step / realPorts / run ──

    let manifestError (reason: string) : Result<GenerationManifest, DeclError> = Error { Reason = reason }

    let step (ports: Ports) (effect: Loop.Effect) : Loop.Msg =
        match effect with
        | Loop.LoadManifest _ ->
            let loaded =
                try
                    match ports.Files "refresh.yml" with
                    | Ok(Some content) ->
                        let lines = content.Replace("\r\n", "\n").Split('\n') |> List.ofArray
                        Declaration.parse lines
                    | Ok None -> manifestError ".fsgg/refresh.yml not found"
                    | Error reason -> manifestError ("unreadable: " + reason)
                with e ->
                    manifestError ("read failed: " + e.Message)

            Loop.ManifestLoaded loaded

        | Loop.SenseSource entry -> Loop.Sensed(entry.ViewId, CommandHost.guard (fun () -> ports.Sense entry))

        | Loop.ReadRecorded viewId ->
            let recorded =
                try
                    ports.ReadProv viewId
                with _ ->
                    None

            Loop.RecordedRead(viewId, recorded)

        | Loop.RegenerateView entry -> Loop.Regenerated'(entry.ViewId, CommandHost.guard (fun () -> ports.Generate entry))

        | Loop.RecordProvenance(viewId, provenance) -> Loop.ProvenanceWritten(CommandHost.guard (fun () -> ports.WriteProv viewId provenance))

        | Loop.WriteArtifact(path, content) -> Loop.Wrote(CommandHost.guard (fun () -> ports.Write path content))

        | Loop.EmitSummary text ->
            ports.Out text
            Loop.Emitted

    let realPorts (repo: string) : Ports =
        { Files = Loader.fileSystemReader repo
          Sense = senseEntry repo
          ReadProv = readProv repo
          Generate = generateEntry repo
          WriteProv = writeProv repo
          Write = writeAtomic
          Out = fun text -> Console.Out.WriteLine text }

    let run (ports: Ports) (request: Loop.RunRequest) : Loop.Model =
        let m0, eff0 = Loop.init request

        CommandHost.drive (fun (m: Loop.Model) -> m.Phase = Loop.Done) (step ports) Loop.update m0 eff0
