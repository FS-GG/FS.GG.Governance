namespace FS.GG.Governance.HumanRender

open System
open System.IO
open System.Threading
open FS.GG.Governance.HumanText

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Watch =

    type WatchSignal =
        | Idle
        | Rendered
        | InputUnreadable of reason: string

    type WatchModel =
        { Root: string
          Mode: RenderMode.RenderMode
          PendingSince: int64 option
          LastSignal: WatchSignal }

    type WatchMsg =
        | ChangeDetected of at: int64
        | WindowSettled of at: int64
        | Rerendered of WatchSignal

    type WatchEffect =
        | SenseChanges of root: string
        | ScheduleDebounce of dueAt: int64
        | ReRender of root: string * mode: RenderMode.RenderMode

    let debounceWindow = 200L

    let init (root: string) (mode: RenderMode.RenderMode) : WatchModel * WatchEffect list =
        { Root = root
          Mode = mode
          PendingSince = None
          LastSignal = Idle },
        [ SenseChanges root ]

    let update (msg: WatchMsg) (model: WatchModel) : WatchModel * WatchEffect list =
        match msg with
        | ChangeDetected at ->
            // (Re)start the window from the latest change; a burst keeps pushing the due time out,
            // so only the final change's WindowSettled survives the guard below ⇒ one re-render.
            { model with PendingSince = Some at }, [ ScheduleDebounce(at + debounceWindow) ]
        | WindowSettled at ->
            match model.PendingSince with
            | Some since when at >= since + debounceWindow ->
                // the window since the latest change has fully elapsed ⇒ settle and re-render once.
                { model with PendingSince = None }, [ ReRender(model.Root, model.Mode) ]
            | _ ->
                // a newer change arrived after this debounce was scheduled ⇒ ignore; a later
                // WindowSettled will fire for it. No effect, no state change.
                model, []
        | Rerendered signal -> { model with LastSignal = signal }, []

    // ── interactive stop-poll (headless-safe) ──
    // `Console.KeyAvailable`/`ReadKey` throw `InvalidOperationException` when stdin is redirected or no
    // console is attached (pipes, CI). An interactive `--watch` cannot be driven without a console, so
    // ANY such failure STOPS the loop (clean exit) rather than crashing the process or spinning forever.
    // Hoisted here so BOTH watch entries share one guard and the fix can't land in only one copy again
    // (H3 / #47; same headless-fragility class as 091). Tests inject their own `shouldStop`.
    let safeKeyPoll () : bool =
        try
            Console.KeyAvailable && Console.ReadKey(true).Key = ConsoleKey.Q
        with _ ->
            true

    // ── interpreter edge (read-only; no contract written) ──

    let run
        (root: string)
        (mode: RenderMode.RenderMode)
        (clock: unit -> int64)
        (reRender: string -> RenderMode.RenderMode -> WatchSignal)
        (shouldStop: unit -> bool)
        : WatchSignal =
        // The init effect is `SenseChanges`, realized below by the FileSystemWatcher itself.
        let mutable model = fst (init root mode)

        let lastChange = ref (None: int64 option)

        // Guard watcher construction: a nonexistent / unreadable root makes `new FileSystemWatcher`
        // throw. Surface it as `InputUnreadable` (the host maps this to its input-unavailable exit
        // code — 3 / 66) instead of letting the process crash, matching the key-poll guard above.
        match (try Ok(new FileSystemWatcher(root)) with e -> Error e.Message) with
        | Error reason -> InputUnreadable reason
        | Ok w ->
            use watcher = w
            watcher.IncludeSubdirectories <- true
            watcher.EnableRaisingEvents <- false

            let onChange (_: FileSystemEventArgs) = lastChange.Value <- Some(clock ())

            watcher.Changed.Add onChange
            watcher.Created.Add onChange
            watcher.Deleted.Add onChange
            watcher.Renamed.Add(fun _ -> lastChange.Value <- Some(clock ()))

            let apply (msg: WatchMsg) =
                let m, es = update msg model
                model <- m

                for e in es do
                    match e with
                    | SenseChanges _ -> ()
                    | ScheduleDebounce _ -> ()
                    | ReRender(r, md) ->
                        let signal = reRender r md
                        let m2, _ = update (Rerendered signal) model
                        model <- m2

            watcher.EnableRaisingEvents <- true

            while not (shouldStop ()) do
                match lastChange.Value with
                | Some t ->
                    lastChange.Value <- None
                    apply (ChangeDetected t)
                    Thread.Sleep(int debounceWindow + 50)
                    apply (WindowSettled(t + debounceWindow))
                | None -> Thread.Sleep 50

            model.LastSignal
