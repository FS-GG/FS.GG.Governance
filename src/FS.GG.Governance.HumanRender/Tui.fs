namespace FS.GG.Governance.HumanRender

open FS.GG.Governance.HumanText
open FS.GG.Governance.HumanText.ReportView

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Tui =

    type TuiModel =
        { View: ReportView
          Path: int list
          Expanded: Set<int list> }

    type TuiMsg =
        | MoveUp
        | MoveDown
        | Expand
        | Collapse
        | Quit

    type TuiEffect =
        | ReadKey
        | Draw of TuiModel
        | Exit

    let init (view: ReportView) : TuiModel * TuiEffect list =
        let model =
            { View = view
              Path = (if List.isEmpty view.Sections then [] else [ 0 ])
              Expanded = Set.empty }

        model, [ Draw model; ReadKey ]

    let private cursor (model: TuiModel) =
        match model.Path with
        | i :: _ -> i
        | [] -> 0

    let private withCursor (model: TuiModel) (i: int) = { model with Path = [ i ] }

    let update (msg: TuiMsg) (model: TuiModel) : TuiModel * TuiEffect list =
        let lastIndex = List.length model.View.Sections - 1

        let next =
            match msg with
            | MoveUp -> withCursor model (max 0 (cursor model - 1))
            | MoveDown -> withCursor model (min (max 0 lastIndex) (cursor model + 1))
            | Expand -> { model with Expanded = Set.add model.Path model.Expanded }
            | Collapse -> { model with Expanded = Set.remove model.Path model.Expanded }
            | Quit -> model

        match msg with
        | Quit -> next, [ Exit ]
        | _ -> next, [ Draw next; ReadKey ]

    // ── interpreter edge (read-only key/redraw loop) ──

    let run (view: ReportView) (readKey: unit -> TuiMsg) (draw: TuiModel -> unit) : unit =
        let mutable model, effects = init view

        // run the init effects, then loop on key input through the pure update until Quit.
        for e in effects do
            match e with
            | Draw m -> draw m
            | ReadKey -> ()
            | Exit -> ()

        let mutable running = true

        while running do
            let msg = readKey ()
            let m, es = update msg model
            model <- m

            for e in es do
                match e with
                | Draw mm -> draw mm
                | ReadKey -> ()
                | Exit -> running <- false
