// The thin host entry for `fsgg route` (F022): parse argv → build the real ports → run the
// interpreter → print any diagnostics to stderr → return the mapped exit code. ALL decision logic
// lives in the pure `Loop`; ALL I/O in the `Interpreter`. A `parse` rejection exits 2 BEFORE any
// port is built, so a usage error writes no artifact and starts no git/filesystem work (FR-010).

module FS.GG.Governance.RouteCommand.Program

open System
open System.Diagnostics
open FS.GG.Governance.RouteCommand
open FS.GG.Governance.HumanText
open FS.GG.Governance.HumanRender

let usageMessage (err: Loop.UsageError) : string =
    match err with
    | Loop.UnknownFlag flag -> "unknown flag: " + flag
    | Loop.MissingValue flag -> "missing value for flag: " + flag
    | Loop.PathsAndSinceTogether -> "--paths and --since are mutually exclusive"
    | Loop.EmptyPaths -> "--paths requires at least one path"

let categoryToken (decision: Loop.ExitDecision) : string =
    match decision with
    | Loop.Success -> "ok"
    | Loop.UsageError' -> "usage"
    | Loop.InputUnavailable -> "input-unavailable"
    | Loop.ToolError -> "tool-error"

[<EntryPoint>]
let main argv =
    match Loop.parse (List.ofArray argv) with
    | Error err ->
        eprintfn "fsgg route: %s" (usageMessage err)
        Loop.exitCode Loop.UsageError'
    | Ok request when request.Watch ->
        // F27 wiring (063, US3): the read-only watch loop. A burst of edits coalesces into ONE settled
        // re-render via the pure `Watch.update` debounce; each re-render re-runs the EXISTING route
        // evaluation and re-projects — it writes NO new contract (FR-009). Stop on a `q` keypress.
        let mode = RenderMode.selectMode false (Capability.senseCapability request.ExplicitPlain)
        let mode = if mode = RenderMode.Json then RenderMode.Plain else mode // watch is interactive, never Json
        let sw = Stopwatch.StartNew()

        let reRender (root: string) (md: RenderMode.RenderMode) : Watch.WatchSignal =
            try
                // Read-only: re-run the EXISTING evaluation with no-op write/output ports so the watch
                // re-render computes the report WITHOUT persisting any contract artifact (FR-009).
                let roPorts =
                    { Interpreter.realPorts root with
                        Write = (fun _ _ -> Ok())
                        Out = (fun _ -> ()) }

                let m = Interpreter.run roPorts { request with Watch = false }

                match Loop.humanView m with
                | Some view ->
                    RichRender.emitStdout md view (HumanText.render view)
                    Watch.Rendered
                | None -> Watch.InputUnreadable "route evaluation produced no report"
            with e ->
                Watch.InputUnreadable e.Message

        let shouldStop () =
            Console.KeyAvailable && Console.ReadKey(true).Key = ConsoleKey.Q

        Watch.run request.Repo mode (fun () -> sw.ElapsedMilliseconds) reRender shouldStop
        Loop.exitCode Loop.Success

    | Ok request ->
        let ports = Interpreter.realPorts request.Repo
        let model = Interpreter.run ports request

        for d in model.Diagnostics do
            eprintfn "fsgg route [%s]: %s" (categoryToken d.Category) d.Message

        Loop.exitCode model.Exit
