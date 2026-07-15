// The EDGE interpreter of the template-provider seam (071) — the ONLY impure code in the feature.
// Visibility lives in Interpreter.fsi (Principle II). It executes the `Loop.Effect`s the pure `update`
// requests against INJECTED, FAKEABLE ports and feeds each result back as a `Loop.Msg`. TOTAL and SAFE
// (FR-008, SC-005): every port `Error` and every thrown exception is caught and reified to the matching
// `Msg` — it NEVER throws and (via temp+rename, all-or-nothing) NEVER leaves a partial tree. The
// host-owned lifecycle skeleton is never written here (the seam only ADDS provider-emitted runtime
// files); the manifest itself is NOT written (host concern, research D0).

namespace FS.GG.Governance.Scaffold

open System
open System.IO
open FS.GG.Governance.Scaffold.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    type Ports =
        { Invoke: TemplateProvider -> ScaffoldRequest -> Result<ProviderEmission, ProviderError>
          Probe: string list -> Result<string list, string>
          Write: (string * string) list -> Result<unit, string>
          Out: string -> unit }

    // Run a port call, converting BOTH an `Error` and a thrown exception into `Error` so the
    // interpreter never throws out of itself (the RouteCommand.Interpreter discipline).
    let guard (call: unit -> Result<'a, string>) : Result<'a, string> =
        try
            call ()
        with e ->
            Error e.Message

    // Resolve a target-relative path to a full path under `target`, then CONFIRM it stays inside the
    // target's full path (defence-in-depth over the pure boundary check — research D5). Returns the
    // resolved absolute path or an out-of-target error.
    let resolveUnder (target: string) (rel: string) : Result<string, string> =
        let rootFull = Path.GetFullPath target
        let combined = Path.GetFullPath(Path.Combine(rootFull, rel))

        // Compare against the root WITH a trailing separator so a sibling prefixed by the root name
        // (e.g. `<root>-evil`) does not count as inside.
        let rootWithSep =
            if rootFull.EndsWith(string Path.DirectorySeparatorChar) then
                rootFull
            else
                rootFull + string Path.DirectorySeparatorChar

        if combined = rootFull || combined.StartsWith rootWithSep then
            Ok combined
        else
            Error(sprintf "resolved path escapes target: %s" rel)

    // The real PROBE port for `target`: return the target-relative subset of `paths` that already exist
    // on disk (as a file or directory). A path that resolves out of target is reported as existing-ish
    // by raising it through the error channel? No — probe only reports existence; the boundary is the
    // pure check's job. Resolution failures surface as an `Error` so `update` refuses safely.
    let probeUnder (target: string) (paths: string list) : Result<string list, string> =
        try
            let existing =
                paths
                |> List.filter (fun rel ->
                    match resolveUnder target rel with
                    | Ok full -> File.Exists full || Directory.Exists full
                    | Error _ -> false)

            Ok existing
        with e ->
            Error e.Message

    // The real WRITE port for `target`: lay down every (relative-path, contents) pair ATOMICALLY and
    // all-or-nothing. Each file is written to a unique temp sibling then atomically renamed over its
    // resolved target; any failure (including an out-of-target resolution) aborts the WHOLE batch and
    // removes every temp/already-renamed file, leaving ZERO new files (SC-005). Mirrors
    // RouteCommand.Interpreter.writeAtomic's temp+rename discipline, extended to a batch.
    let writeAllUnder (target: string) (files: (string * string) list) : Result<unit, string> =
        // Pre-resolve every path first so a single bad path aborts before any write happens.
        let resolved =
            files
            |> List.map (fun (rel, contents) -> resolveUnder target rel |> Result.map (fun full -> full, contents))

        let firstError =
            resolved
            |> List.tryPick (function
                | Error e -> Some e
                | Ok _ -> None)

        match firstError with
        | Some e -> Error e
        | None ->
            let pairs =
                resolved
                |> List.choose (function
                    | Ok pc -> Some pc
                    | Error _ -> None)

            // Track what we created so we can roll back on a mid-batch failure: the files already renamed
            // into place, PLUS the single in-flight temp that was written but not yet renamed (a failure
            // between WriteAllText and Move — e.g. the target already exists — otherwise leaks a `.tmp-<guid>`),
            // PLUS every directory this batch newly created (deepest-first once reversed) so a rollback leaves
            // ZERO new files AND ZERO new directories, not just files (ADPT-4 — "ZERO new files" must hold for
            // the tree, not only leaves).
            let written = System.Collections.Generic.List<string>()
            let createdDirs = System.Collections.Generic.List<string>()
            let mutable inFlight: string option = None

            // Record and create the chain of `dir`'s ancestors that do NOT yet exist (shallowest-first, down
            // to but excluding the first already-existing ancestor — so the target root and any pre-existing
            // dirs are never recorded and never rolled back). Idempotent across files: a dir created for an
            // earlier file already exists, so it contributes nothing the second time.
            let ensureDir (dir: string) =
                let rec collectMissing (d: string) (acc: string list) =
                    if String.IsNullOrEmpty d || Directory.Exists d then
                        acc
                    else
                        match Path.GetDirectoryName d with
                        | null
                        | "" -> d :: acc
                        | parent -> collectMissing parent (d :: acc)

                for missing in collectMissing dir [] do
                    createdDirs.Add missing

                Directory.CreateDirectory dir |> ignore

            try
                for (full, contents) in pairs do
                    match Path.GetDirectoryName full with
                    | null
                    | "" -> ()
                    | dir -> ensureDir dir

                    let tmp = full + ".tmp-" + Guid.NewGuid().ToString("N")
                    inFlight <- Some tmp
                    File.WriteAllText(tmp, contents)
                    File.Move(tmp, full, false)
                    inFlight <- None
                    written.Add full

                Ok()
            with e ->
                // Roll back so no partial tree survives (SC-005): first the in-flight temp (present only if
                // the failure landed between its write and its rename), then every renamed file, then every
                // directory this batch created — deepest-first (reverse of shallowest-first record order) so a
                // child empties before its parent. Each dir delete is non-recursive and guarded, so a dir that
                // is somehow non-empty (never expected — every file we made is deleted above) is left intact
                // rather than clobbering pre-existing content.
                match inFlight with
                | Some tmp ->
                    try
                        File.Delete tmp
                    with _ ->
                        ()
                | None -> ()

                for created in written do
                    try
                        File.Delete created
                    with _ ->
                        ()

                for i in (createdDirs.Count - 1) .. -1 .. 0 do
                    try
                        Directory.Delete(createdDirs[i], false)
                    with _ ->
                        ()

                Error e.Message

    let realPorts (target: string) : Ports =
        { Invoke = fun provider request -> provider.Emit request
          Probe = probeUnder target
          Write = writeAllUnder target
          Out = fun text -> Console.Out.WriteLine text }

    let step (ports: Ports) (effect: Loop.Effect) : Loop.Msg =
        match effect with
        | Loop.InvokeProvider(provider, request) ->
            // The provider contract says `Emit` returns a Result and does not throw; we still guard at
            // the edge and reify any thrown exception to an `Unresolvable` provider error (contract C4).
            let result =
                try
                    ports.Invoke provider request
                with e ->
                    Error(Unresolvable e.Message)

            Loop.ProviderEmitted result

        | Loop.ProbeCollisions paths -> Loop.CollisionsProbed(guard (fun () -> ports.Probe paths))

        | Loop.WriteAll files -> Loop.FilesWritten(guard (fun () -> ports.Write files))

    let run (ports: Ports) (request: Loop.RunRequest) : Loop.Model =
        let m0, eff0 = Loop.init request.Request request.Provider

        // Drive init → update* to Done: execute every requested Effect via `step`, feed each result Msg
        // back into the pure `update`, accumulate new effects, repeat. Stops at Done or quiescence. NEVER
        // throws (the RouteCommand.Interpreter.run shape).
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
