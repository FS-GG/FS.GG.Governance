module FS.GG.Governance.CacheEligibilityCommand.Tests.RenderModeDispatchTests

open Expecto
open FS.GG.Governance.HumanText
open FS.GG.Governance.CacheEligibilityCommand
open FS.GG.Governance.CacheEligibilityCommand.Tests.Support

// F27 wiring (063) US2: the render-mode dispatch at the cache-eligibility host's interpreter edge. A sensed
// `ColorCapability` (Synthetic — disclosed) feeds the pure `RenderMode.selectMode`: a TTY ⇒ `Rich` (the
// `RenderReport` rich path, ANSI never via the plain `Out` sink); non-TTY / `NO_COLOR` / `--plain` ⇒ `Plain`
// (the ANSI-free `HumanText` projection via `Out`); `--format json` ⇒ `Json` and the rich path is NEVER
// reached (SC-003, SC-004). The mode is decided ONLY at the edge (FR-004).

// SYNTHETIC: a forced capability record standing in for a real sensed terminal — the real sensor is covered
// indirectly; here we drive each branch deterministically (Constitution V).
let private cap (isTty: bool) (noColor: bool) : bool -> RenderMode.ColorCapability =
    fun explicitPlain ->
        { IsTty = isTty
          NoColorEnv = noColor
          ExplicitPlain = explicitPlain
          Width = None }

let private git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]

// Drive the interpreter with an injected capability + a capturing rich renderer; return the model, the
// capture and the list of views the rich path received.
let private runDispatch (capability: bool -> RenderMode.ColorCapability) (format) =
    let req = requestFor Loop.DefaultRange format
    let cap0 = newCapture ()
    let richViews = ResizeArray<ReportView.ReportView>()
    let basePorts = fakePorts validCatalog git fixedSensor (storeReaderOf (Ok None)) cap0 req

    let ports =
        { basePorts with
            SenseCapability = capability
            RenderReport = fun view -> richViews.Add view }

    let model = Interpreter.run ports req
    model, cap0, List.ofSeq richViews

let private esc = string (char 27) // ANSI/CSI escape introducer

[<Tests>]
let tests =
    testList
        "RenderModeDispatch"
        [ test "Synthetic TTY ⇒ Rich: the rich RenderReport path is taken, the plain projection is NOT written to the Out sink (SC-003)" {
              let model, cap0, richViews = runDispatch (cap true false) Loop.Human
              Expect.equal model.Exit Loop.Success "run succeeds"
              Expect.equal (List.length richViews) 1 "the rich renderer received exactly one report view"

              // The rich path goes through RenderReport, not Out; only the operational `wrote` lines reach Out.
              let emitted = String.concat "\n" cap0.Emits
              Expect.stringContains emitted "wrote " "operational lines still emitted after the rich render"
              Expect.isFalse (emitted.Contains "evidence:") "the plain HumanText projection is NOT on the Out sink in Rich mode"
              Expect.isFalse (emitted.Contains esc) "no ANSI on the plain Out sink"
          }

          test "Synthetic non-TTY ⇒ Plain: the ANSI-free HumanText projection is written to Out, rich path NOT taken (SC-003)" {
              let _, cap0, richViews = runDispatch (cap false false) Loop.Human
              Expect.isEmpty richViews "non-TTY ⇒ the rich renderer is never invoked"
              let emitted = Expect.wantSome (List.tryHead cap0.Emits) "the plain summary is emitted via Out"
              Expect.stringContains emitted "evidence:" "Out carries the HumanText projection in Plain mode"
              Expect.isFalse (emitted.Contains esc) "Plain output is ANSI-free"
          }

          test "Synthetic NO_COLOR on a TTY ⇒ Plain (color suppressed)" {
              let _, _, richViews = runDispatch (cap true true) Loop.Human
              Expect.isEmpty richViews "NO_COLOR ⇒ Plain, the rich path is never taken"
          }

          test "--plain on a TTY ⇒ Plain even though the terminal is rich-capable (FR-012)" {
              // requestFor sets ExplicitPlain=false; force it on to mirror a parsed --plain.
              let req = { requestFor Loop.DefaultRange Loop.Human with ExplicitPlain = true }
              let cap0 = newCapture ()
              let richViews = ResizeArray<ReportView.ReportView>()
              let basePorts = fakePorts validCatalog git fixedSensor (storeReaderOf (Ok None)) cap0 req
              let ports = { basePorts with SenseCapability = cap true false; RenderReport = fun v -> richViews.Add v }
              Interpreter.run ports req |> ignore
              Expect.isEmpty richViews "--plain forces Plain regardless of the TTY"
          }

          test "--format json always wins: Json selected, the rich path NEVER reached, output ANSI-free (SC-004)" {
              let _, cap0, richViews = runDispatch (cap true false) Loop.Json
              Expect.isEmpty richViews "Json ⇒ the rich renderer is never invoked, even on a TTY"
              let emitted = Expect.wantSome (List.tryHead cap0.Emits) "the JSON summary is emitted via Out"
              Expect.isFalse (emitted.Contains esc) "JSON output is ANSI-free"
              Expect.isFalse (emitted.Contains "evidence:") "JSON is not the human text"
          }

          test "Json contract does NOT leak host output paths (CLI-4, FR-003)" {
              let req = requestFor Loop.DefaultRange Loop.Json
              let cap0 = newCapture ()
              let ports = fakePorts validCatalog git fixedSensor (storeReaderOf (Ok None)) cap0 req
              Interpreter.run ports req |> ignore
              let emitted = Expect.wantSome (List.tryHead cap0.Emits) "the JSON summary is emitted via Out"
              Expect.isFalse (emitted.Contains "\"wrote\"") "the `wrote` path object is dropped from the JSON contract"
              Expect.isFalse (emitted.Contains req.CacheOut) "the caller-supplied cache path never reaches the JSON"
              Expect.isFalse (emitted.Contains req.UnresolvedOut) "the caller-supplied unresolved path never reaches the JSON"
          }

          test "--plain parses into the request (FR-012)" {
              match Loop.parse [ "cache-eligibility"; "--plain" ] with
              | Ok req -> Expect.isTrue req.ExplicitPlain "--plain sets ExplicitPlain"
              | Error e -> failtestf "parse failed: %A" e
          } ]
