module FS.GG.Governance.Cli.Tests.WatchTuiHostWiringTests

open System
open System.IO
open Expecto
open FS.GG.Governance.HumanText
open FS.GG.Governance.HumanRender
open FS.GG.Governance.Cli
open FS.GG.Governance.RouteCommand.Tests.Support // withTempRepo / validCatalog — the REAL temp-git harness

// F27 wiring (063) US3/US4: the DISPATCHER's read-only `watch`/`tui` surfaces. The dispatcher's one-shot
// `route`/`evidence` commands carry the Kernel route/evidence whose JSON contract stays byte-identical, so
// they CANNOT be projected through HumanText. Instead the new interactive surfaces COMPOSE a real F19
// `RouteResult` by reusing the RouteCommand pipeline (`Program.composeRouteView`) over the repo root and
// project it to the SAME `ReportView` the route surfaces use. These tests drive the dispatcher edge over a
// REAL temp git tree + real `.fsgg` catalog (Constitution V — no fakes). The pure debounce/navigation MVUs
// are reused from F27 (covered there); here we prove the dispatcher WIRING.

let private esc = string (char 27) // ANSI/CSI escape introducer

[<Tests>]
let tests =
    testList
        "WatchTuiHostWiring"
        [ test "dispatcher parses the watch/tui commands and the --plain flag (US3/US4 vocabulary)" {
              Expect.equal (Cli.parse [ "watch" ] |> Result.map (fun r -> r.Command)) (Ok WatchCommand) "watch ⇒ WatchCommand"
              Expect.equal (Cli.parse [ "tui" ] |> Result.map (fun r -> r.Command)) (Ok TuiCommand) "tui ⇒ TuiCommand"

              match Cli.parse [ "route"; "--plain" ] with
              | Ok r -> Expect.isTrue r.ExplicitPlain "--plain sets ExplicitPlain"
              | Error e -> failtestf "parse failed: %A" e
          }

          test "JSON always wins over --plain at the dispatcher: Json format selected, render goes to JSON, never rich (SC-004)" {
              match Cli.parse [ "route"; "--json"; "--plain" ] with
              | Ok r ->
                  Expect.equal r.Format Json "--json selects Json even with --plain present"
                  Expect.isTrue r.ExplicitPlain "--plain is recorded but does not override Json"
              | Error e -> failtestf "parse failed: %A" e
          }

          test "tui host parity: the ReportView the dispatcher composes IS the view Tui.init navigates, over the SAME report object (SC-006)" {
              withTempRepo (fun dir ->
                  let req =
                      match Cli.parse [ "tui"; "--root"; dir ] with
                      | Ok r -> r
                      | Error e -> failtestf "parse failed: %A" e

                  match Program.composeRouteView dir req with
                  | Some view ->
                      let model, _ = Tui.init view
                      Expect.equal model.View view "Tui.init navigates the SAME ReportView the dispatcher composed (report-object parity)"
                  | None -> failtest "the dispatcher composed no report view over the real temp repo")
          }

          test "tui host read-only: navigation never mutates the composed view, and the compose path writes no contract artifact (FR-009, SC-006)" {
              withTempRepo (fun dir ->
                  let req =
                      match Cli.parse [ "tui"; "--root"; dir ] with
                      | Ok r -> r
                      | Error e -> failtestf "parse failed: %A" e

                  let view =
                      Program.composeRouteView dir req
                      |> Option.defaultWith (fun () -> failtest "no view composed")

                  // Drive the same pure navigation the dispatcher's tuiKeyReader feeds Tui.run.
                  let model0, _ = Tui.init view
                  let model1, _ = Tui.update Tui.MoveDown model0
                  let model2, _ = Tui.update Tui.Expand model1
                  let model3, _ = Tui.update Tui.Collapse model2
                  Expect.equal model3.View view "navigation changes only Path/Expanded — the ReportView is never mutated"

                  // composeRouteView is read-only: no contract artifact appears under the tree.
                  Expect.isFalse (File.Exists(Path.Combine(dir, "readiness", "route.json"))) "tui compose wrote no route.json"
                  Expect.isFalse (File.Exists(Path.Combine(dir, ".fsgg", "gates.json"))) "tui compose wrote no gates.json")
          }

          test "watch host wiring: the dispatcher's read-only re-render composes an ANSI-free plain projection and writes no contract artifact (FR-009)" {
              withTempRepo (fun dir ->
                  let req =
                      match Cli.parse [ "watch"; "--root"; dir ] with
                      | Ok r -> r
                      | Error e -> failtestf "parse failed: %A" e

                  // The dispatcher's watch reRender path IS composeRouteView; emulate one settled re-render.
                  match Program.composeRouteView dir req with
                  | Some view ->
                      let plain = HumanText.render view
                      Expect.isFalse (plain.Contains esc) "the composed plain projection is ANSI-free (the Plain mode the dispatcher selects off-TTY)"
                  | None -> failtest "no view composed"

                  Expect.isFalse (File.Exists(Path.Combine(dir, "readiness", "route.json"))) "watch compose writes no contract artifact")
          }

          test "headless (CLI-1): the dispatcher's `tuiKeyReader` swallows an unreadable console (redirected stdin) and quits, never crashing" {
              // `Console.ReadKey` throws `InvalidOperationException` when stdin is redirected / no console is
              // attached — the state of the Expecto runner, and of every CI/piped `fsgg-governance tui`
              // invocation. `tuiKeyReader` must be TOTAL: map that to `Quit` (clean exit), the navigation-surface
              // sibling of `Watch.safeKeyPoll` (H3/#47). Without the guard the real `tui` entry crashes headless
              // instead of exiting cleanly. Guarded on `IsInputRedirected` so this never blocks on a real console
              // (where `ReadKey` would wait for a keypress) — it asserts exactly the headless path the fix targets.
              if Console.IsInputRedirected then
                  let mutable threw = false
                  let mutable msg = Tui.MoveDown
                  try msg <- Program.tuiKeyReader () with _ -> threw <- true
                  Expect.isFalse threw "tuiKeyReader never propagates a console exception"
                  Expect.equal msg Tui.Quit "an unreadable console maps to Quit (clean exit), not a crash"
              else
                  skiptest "console is interactive here; the headless guard path is not exercised (ReadKey would block)"
          }

          test "headless (H3 / #47): the dispatcher's `runWatch` over a nonexistent root fails to arm the watcher and exits input-unavailable (66), never crashing" {
              // The FileSystemWatcher cannot be constructed for a missing root, so `Watch.run` returns
              // `InputUnreadable` BEFORE the poll loop consults the console. `runWatch` maps that to the CLI's
              // input-unavailable exit (66). Deterministic regardless of the test host's stdin state, while
              // exercising the real dispatcher watch entry (shared `safeKeyPoll` stop-poll) end-to-end.
              let missing = Path.Combine(Path.GetTempPath(), "fsgg-cli-watch-missing-" + Guid.NewGuid().ToString("N"))

              match Cli.parse [ "watch"; "--root"; missing ] with
              | Ok request -> Expect.equal (Program.runWatch request) 66 "a nonexistent watch root ⇒ InputUnavailable exit (66), not a FileSystemWatcher crash"
              | Error e -> failtestf "parse failed: %A" e
          } ]
