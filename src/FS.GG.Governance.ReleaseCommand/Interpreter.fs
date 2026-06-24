// The EDGE interpreter of the `fsgg release` host command (F055) — the ONLY impure code in the feature.
// Visibility lives in Interpreter.fsi (Principle II). It executes the `Loop.Effect`s the pure `update`
// requests against INJECTED, FAKEABLE ports and feeds each result back as a `Loop.Msg`. It REUSES the
// existing edges verbatim — `Config.Loader` for the `.fsgg/release.yml` read (F014) and F054
// `senseRelease`/`realPort` for the repository sensing — adding only the persistence (`ArtifactWriter`)
// and stdout (`OutputSink`) ports. TOTAL and SAFE: every port `Error` and every thrown exception is caught
// and reified to the matching `Msg` — it NEVER throws and (via temp+rename) NEVER leaves a partial
// artifact, and a write failure is reified to a `Wrote(Error)` (mapped by `update` to ToolError, never
// Blocked). F054 `senseRelease` is itself TOTAL & SAFE (every read failure becomes an `Unrecoverable`
// family), so the `Sensed` path always carries a complete six-family `SensedRelease`.

namespace FS.GG.Governance.ReleaseCommand

open System
open System.IO
open FS.GG.Governance.Config                       // Loader
open FS.GG.Governance.ReleaseFactsSensing.Model     // SourceLayout, ReleaseExpectations, SensedRelease

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    type ArtifactWriter = string -> string -> Result<unit, string>

    type OutputSink = string -> unit

    type Ports =
        { Files: Loader.FileReader
          Sense: SourceLayout -> ReleaseExpectations -> SensedRelease
          Write: ArtifactWriter
          Out: OutputSink }

    // Run a port call, converting BOTH an `Error` and a thrown exception into `Error` so the interpreter
    // never throws out of itself.
    let guard (call: unit -> Result<'a, string>) : Result<'a, string> =
        try
            call ()
        with e ->
            Error e.Message

    // The real persistence port: create parent dirs, write to a unique temp sibling, then atomically
    // rename over the target — a failed write leaves NO partial/truncated file (FR-012).
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

    // Build a typed `DeclarationLoaded(Error _)` (the annotation resolves the `Reason` field to DeclError,
    // distinct from a sensing diagnostic's `Reason`).
    let declError (reason: string) : Result<Declaration.ReleaseDeclaration, Declaration.DeclError> =
        Error { Reason = reason }

    let step (ports: Ports) (effect: Loop.Effect) : Loop.Msg =
        match effect with
        | Loop.LoadDeclaration _ ->
            // Read `.fsgg/release.yml` through the F014 reader (pre-bound to the repo in realPorts). An
            // absent file (`Ok None`) and a read `Error` are both reified to `DeclarationLoaded(Error)` ⇒
            // InputUnavailable (exit 3). A misbehaving reader that throws is guarded to the same category.
            let loaded =
                try
                    match ports.Files "release.yml" with
                    | Ok(Some content) ->
                        let lines = content.Replace("\r\n", "\n").Split('\n') |> List.ofArray
                        Declaration.parse lines
                    | Ok None -> declError ".fsgg/release.yml not found"
                    | Error reason -> declError ("unreadable: " + reason)
                with e ->
                    declError ("read failed: " + e.Message)

            Loop.DeclarationLoaded loaded

        | Loop.SenseRelease(layout, expectations) ->
            // F054 `senseRelease` is TOTAL & SAFE: every absent/unreadable/unparseable source becomes an
            // `Unrecoverable` family, never a throw and never a fabricated `Met`. So the `Sensed` Msg always
            // carries a complete six-family `SensedRelease`.
            Loop.Sensed(ports.Sense layout expectations)

        | Loop.WriteArtifact(path, content) -> Loop.Wrote(guard (fun () -> ports.Write path content))

        | Loop.EmitSummary text ->
            ports.Out text
            Loop.Emitted

    let realPorts (repo: string) : Ports =
        { Files = Loader.fileSystemReader repo
          Sense =
            fun layout expectations ->
                FS.GG.Governance.ReleaseFactsSensing.Interpreter.senseRelease
                    (FS.GG.Governance.ReleaseFactsSensing.Interpreter.realPort repo layout)
                    expectations
          Write = writeAtomic
          Out = fun text -> Console.Out.WriteLine text }

    let run (ports: Ports) (request: Loop.RunRequest) : Loop.Model =
        let m0, eff0 = Loop.init request

        // Drive init → update* to Done: execute every requested Effect via `step`, feed each result Msg
        // back into the pure `update`, accumulate new effects, repeat. Stops at Done or quiescence
        // (EmitSummary → Emitted → Done). NEVER throws.
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
