namespace FS.GG.Governance.Cli

open System
open System.Diagnostics
open System.IO
open System.Text
open System.Text.Json
open System.Text.RegularExpressions
open FS.GG.Governance.Kernel
open FS.GG.Governance.Host
open FS.GG.Governance.Adapters.SpecKit
open FS.GG.Governance.Adapters.DesignSystem
// F27 wiring (063): RenderMode/ReportView + the Spectre-owning rich render / capability sensing /
// read-only watch+tui MVUs. RouteCommand is NOT opened (its `Loop` would shadow `Host.Loop`); it is
// reached fully-qualified so the dispatcher can compose a real F19 `RouteResult` view.
open FS.GG.Governance.HumanText
open FS.GG.Governance.HumanRender

module Program =

    let fullPath (path: string) = Path.GetFullPath path

    let reviewBudgetLimit (budget: ReviewBudget) =
        match budget with
        | CacheOnly -> 0
        | FreshReviews count -> count

    let addUnique (value: string) (values: string list) =
        if List.contains value values then values else values @ [ value ]

    let emptyBudget: BudgetState =
        { Requested = []
          CacheHits = []
          CacheMisses = []
          FreshDispatches = []
          Pending = []
          BudgetExhausted = [] }

    let markRequested (key: string) (budget: BudgetState) = { budget with Requested = addUnique key budget.Requested }

    let markHit (key: string) (budget: BudgetState) = { budget with CacheHits = addUnique key budget.CacheHits }

    let markMiss (key: string) (budget: BudgetState) = { budget with CacheMisses = addUnique key budget.CacheMisses }

    let markFresh (key: string) (budget: BudgetState) = { budget with FreshDispatches = addUnique key budget.FreshDispatches }

    let markPending (key: string) (budget: BudgetState) = { budget with Pending = addUnique key budget.Pending }

    let markExhausted (key: string) (budget: BudgetState) = { budget with BudgetExhausted = addUnique key budget.BudgetExhausted }

    let runHost (request: RunRequest) (snapshot: ProjectSnapshot) =
        let options = ArtifactReading.optionsFor request
        let config = Project.toLoopConfig options request.Mode snapshot
        let model0, effects0 = Loop.init config snapshot.Change
        let limit = reviewBudgetLimit request.ReviewBudget

        let rec stepEffect (budget: BudgetState) (effect: FS.GG.Governance.Host.Effect) =
            match effect with
            | ReadArtifact artifact ->
                budget, [ FS.GG.Governance.Host.Msg.Sensed(artifact, ArtifactReading.readArtifact snapshot.Root artifact) ]
            | LoadReview key ->
                let budget = budget |> markRequested key

                match ReviewStore.loadReview request key with
                | Ok (Some review) -> budget |> markHit key, [ FS.GG.Governance.Host.Msg.Loaded(key, Ok(Some review)) ]
                | Ok None -> budget |> markMiss key, [ FS.GG.Governance.Host.Msg.Loaded(key, Ok None) ]
                | Error reason -> budget, [ FS.GG.Governance.Host.Msg.Loaded(key, Error reason) ]
            | DispatchReview dispatch ->
                let key = dispatch.Task.Key

                if List.length budget.FreshDispatches < limit then
                    let budget = budget |> markFresh key

                    let reason = "fresh agent dispatch is not configured for this CLI run"

                    budget |> markPending key, [ FS.GG.Governance.Host.Msg.Reviewed(key, Error reason) ]
                else
                    budget |> markPending key |> markExhausted key, []
            | RecordVerdict review ->
                budget, [ FS.GG.Governance.Host.Msg.Recorded(review.Key, ReviewStore.saveReview request review) ]
            | EmitOutput _ -> budget, []

        let rec drive (model: FS.GG.Governance.Host.Model<ProjectFact>) effects (budget: BudgetState) =
            let budget, messages =
                effects
                |> List.fold
                    (fun (budget, messages) effect ->
                        let budget, produced = stepEffect budget effect
                        budget, messages @ produced)
                    (budget, [])

            match messages with
            | [] -> model, budget
            | _ ->
                let model, effects =
                    messages
                    |> List.fold
                        (fun (model, effects) message ->
                            let model, produced = Loop.update config message model
                            model, effects @ produced)
                        (model, [])

                drive model effects budget

        drive model0 effects0 emptyBudget

    let writeOutput (request: RunRequest) (result: CommandResult) =
        try
            let text = CliRender.render result

            match request.OutputPath with
            | Some path ->
                let path = fullPath path
                match Path.GetDirectoryName path |> Option.ofObj with
                | Some dir when not (String.IsNullOrWhiteSpace dir) ->
                    Directory.CreateDirectory(dir) |> ignore
                | _ -> ()

                File.WriteAllText(path, text + Environment.NewLine)
            | None -> Console.Out.WriteLine text

            Ok()
        with ex ->
            Error ex.Message

    // ── F27 wiring (063, US3/US4): the read-only watch/tui interpreter edges ──────────────────────────
    //
    // The dispatcher does NOT hold the F19 `RouteResult` that `HumanText`/`HumanRender` project (its one-shot
    // `route`/`evidence` commands carry the Kernel `Route`/`ProjectEvidenceReport` whose JSON contract stays
    // byte-identical). So the new interactive surfaces COMPOSE a real `RouteResult` by reusing the proven
    // RouteCommand pipeline (`Interpreter.run` + `Loop.humanView`) over the repo root, then project it to a
    // `ReportView` and drive the read-only `Watch.run`/`Tui.run` MVUs. These surfaces write NO artifact and
    // carry NO JSON contract — they are a pure second projection over a freshly composed report (FR-009).

    /// Read-only RouteCommand ports: the real catalog/git/freshness senses, but the persistence and stdout
    /// ports are no-ops so a compose pass writes no artifact and prints nothing (the caller renders).
    let readOnlyRoutePorts (root: string) : FS.GG.Governance.RouteCommand.Interpreter.Ports =
        { FS.GG.Governance.RouteCommand.Interpreter.realPorts root with
            Write = (fun _ _ -> Ok())
            Out = (fun _ -> ()) }

    /// The RouteCommand request the dispatcher composes for the watch/tui surfaces: the default changed-path
    /// range over the repo, carrying the dispatcher's `--plain` through to the capability-sensing edge. The
    /// out paths are irrelevant (the read-only ports never write).
    let routeRequestFor (root: string) (request: RunRequest) : FS.GG.Governance.RouteCommand.Loop.RunRequest =
        { Repo = root
          Scope = FS.GG.Governance.RouteCommand.Loop.DefaultRange
          Format = FS.GG.Governance.RouteCommand.Loop.Text
          GatesOut = Path.Combine(root, ".fsgg", "gates.json")
          RouteOut = Path.Combine(root, "readiness", "route.json")
          StorePath = Path.Combine(root, "readiness", "evidence-reuse.json")
          PersistStore = false
          ExplicitPlain = request.ExplicitPlain
          Watch = false }

    /// Compose a real F19 `RouteResult` view over `root` — the SAME `ReportView` the route plain/rich surfaces
    /// project. `None` when the evaluation produced no report (e.g. an unreadable tree). Writes nothing.
    let composeRouteView (root: string) (request: RunRequest) : ReportView.ReportView option =
        let model = FS.GG.Governance.RouteCommand.Interpreter.run (readOnlyRoutePorts root) (routeRequestFor root request)
        FS.GG.Governance.RouteCommand.Loop.humanView model

    /// The read-only `watch` subcommand: re-run the composed route evaluation and re-render on each settled
    /// debounce window; a burst of edits coalesces into one re-render via the pure `Watch.update` (FR-009).
    let runWatch (request: RunRequest) : int =
        let root = fullPath request.Root
        let mode0 = RenderMode.selectMode false (Capability.senseCapability request.ExplicitPlain)
        let mode = if mode0 = RenderMode.Json then RenderMode.Plain else mode0 // watch is interactive, never Json
        let sw = Stopwatch.StartNew()

        let reRender (r: string) (md: RenderMode.RenderMode) : Watch.WatchSignal =
            try
                match composeRouteView r request with
                | Some view ->
                    RichRender.emitStdout md view (HumanText.render view)
                    Watch.Rendered
                | None -> Watch.InputUnreadable "route evaluation produced no report"
            with e ->
                Watch.InputUnreadable e.Message

        // Stop on `q`; the shared `safeKeyPoll` also stops cleanly (never crashes) when stdin is
        // redirected / no console is attached. A nonexistent root makes the watcher fail to construct
        // ⇒ `InputUnreadable` ⇒ input-unavailable exit (66), never a crash (H3 / #47).
        match Watch.run root mode (fun () -> sw.ElapsedMilliseconds) reRender Watch.safeKeyPoll with
        | Watch.InputUnreadable reason -> Cli.exitCode (InputUnavailable reason)
        | _ -> Cli.exitCode Success

    /// Plain-text TUI draw (Spectre stays confined to HumanRender, so the navigator cursor is rendered as
    /// plain text at the edge): the selection path plus the full report projection.
    let drawTui (model: Tui.TuiModel) : unit =
        try
            Console.Clear()
        with _ ->
            ()

        let path = model.Path |> List.map string |> String.concat "."
        Console.Out.WriteLine(sprintf "report navigator — section %s  (up/down move, right/left expand/collapse, q quit)" path)
        Console.Out.WriteLine(HumanText.render model.View)

    /// Map a blocking key read to a navigation message; any other key quits. Headless-safe: exactly like
    /// the shared `Watch.safeKeyPoll`, `Console.ReadKey` throws `InvalidOperationException` when stdin is
    /// redirected or no console is attached (pipes, CI). An interactive `tui` cannot be driven without a
    /// console, so ANY such failure QUITS cleanly (clean exit) instead of crashing the process — the
    /// navigation-surface sibling of the `watch` stop-poll guard (CLI-1; same headless-fragility class as
    /// H3/#47). `tui` has a single reader (only the dispatcher owns it), so the guard lives here at the
    /// interpreter edge rather than being hoisted like the two-entry `watch` poll.
    let tuiKeyReader () : Tui.TuiMsg =
        try
            match Console.ReadKey(true).Key with
            | ConsoleKey.UpArrow
            | ConsoleKey.K -> Tui.MoveUp
            | ConsoleKey.DownArrow
            | ConsoleKey.J -> Tui.MoveDown
            | ConsoleKey.RightArrow
            | ConsoleKey.L -> Tui.Expand
            | ConsoleKey.LeftArrow
            | ConsoleKey.H -> Tui.Collapse
            | _ -> Tui.Quit
        with _ ->
            Tui.Quit

    /// The read-only `tui` subcommand: navigate the composed route `ReportView`. Navigation changes only the
    /// cursor/expanded state (Tui.update is pure & read-only); no verdict/contract is touched (FR-009).
    let runTui (request: RunRequest) : int =
        let root = fullPath request.Root

        match composeRouteView root request with
        | Some view ->
            Tui.run view tuiKeyReader drawTui
            Cli.exitCode Success
        | None ->
            Console.Error.WriteLine "fsgg-governance tui: route evaluation produced no report"
            Cli.exitCode (InputUnavailable "route evaluation produced no report")

    [<EntryPoint>]
    let main argv =
        // F27 wiring (063): the read-only watch/tui surfaces are interactive loops, not the one-shot
        // snapshot→host→output MVU — intercept them at the edge before building the one-shot ports.
        match Cli.parse (argv |> Array.toList) with
        | Ok request when request.Command = WatchCommand -> runWatch request
        | Ok request when request.Command = TuiCommand -> runTui request
        | _ ->
            let ports =
                { LoadSnapshot = ArtifactReading.loadSnapshot
                  RunHost = fun request snapshot -> runHost request snapshot
                  WriteOutput = fun request result -> writeOutput request result }

            let result = Cli.run ports (argv |> Array.toList)

            if result.Request.IsNone then
                Console.Error.WriteLine(CliRender.render result)

            Cli.exitCode result.Exit
