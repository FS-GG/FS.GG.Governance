namespace FS.GG.Governance.Host

open FS.GG.Governance.Kernel

// Implementation of the edge interpreter (F08) — the ONLY impure code in the feature.
// Visibility lives in Interpreter.fsi (Principle II). `step`/`run` reify EVERY result,
// including every thrown exception, as a `Msg`; they NEVER throw out of themselves
// (FR-012, SC-006).

type ArtifactReader = ArtifactRef -> Result<string, string>

type Judge = ReviewTask -> Result<JudgeVerdict, string>

type ReviewStore =
    { Load: string -> Result<RecordedReview option, string>
      Save: RecordedReview -> Result<unit, string> }

type OutputSink = Output -> unit

type Ports =
    { Read: ArtifactReader
      Judge: Judge
      Store: ReviewStore
      Sink: OutputSink }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    // Run a port call, converting BOTH an `Error` and a thrown exception into `Error` so the
    // interpreter never throws out of itself (R-F1, FR-012, SC-006).
    let guard (call: unit -> Result<'a, string>) : Result<'a, string> =
        try
            call ()
        with e ->
            Error e.Message

    let step (ports: Ports) (effect: Effect) : Msg<'fact> list =
        match effect with
        | ReadArtifact ref -> [ Sensed(ref, guard (fun () -> ports.Read ref)) ]

        | LoadReview key -> [ Loaded(key, guard (fun () -> ports.Store.Load key)) ]

        | DispatchReview dispatch ->
            // Draw one sample per requested sample; gather into a single Reviewed Msg. A failure
            // of any sample (Error or exception) fails the whole dispatch (review stays pending).
            let result =
                guard (fun () ->
                    let samples = [ for _ in 1 .. max 1 dispatch.Samples -> ports.Judge dispatch.Task ]

                    match samples |> List.tryPick (function | Error e -> Some e | Ok _ -> None) with
                    | Some e -> Error e
                    | None -> Ok(samples |> List.choose (function | Ok s -> Some s | Error _ -> None)))

            [ Msg.Reviewed(dispatch.Task.Key, result) ]

        | RecordVerdict rr -> [ Recorded(rr.Key, guard (fun () -> ports.Store.Save rr)) ]

        | EmitOutput out ->
            ports.Sink out
            []

    let run (ports: Ports) (config: LoopConfig<'change, 'fact>) (change: 'change) : Model<'fact> =
        let m0, eff0 = Loop.init config change

        // Drive init → update* to quiescence: execute every requested Effect via `step`, feed
        // each result Msg back into the pure `update`, and repeat until no Msg is produced
        // (EmitOutput yields no Msg, so the loop terminates) (FR-004, FR-016).
        let rec drive (model: Model<'fact>) (effects: Effect list) =
            match effects |> List.collect (step ports) with
            | [] -> model
            | msgs ->
                let model, newEffects =
                    msgs
                    |> List.fold
                        (fun (m, acc) msg ->
                            let m2, e2 = Loop.update config msg m
                            m2, acc @ e2)
                        (model, [])

                drive model newEffects

        drive m0 eff0
