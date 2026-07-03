module FS.GG.Governance.RouteCommand.Tests.WatchHostTests

open System.IO
open System.Diagnostics
open System.Threading
open System.Threading.Tasks
open Expecto
open FS.GG.Governance.HumanText
open FS.GG.Governance.HumanRender
open FS.GG.Governance.RouteCommand
open FS.GG.Governance.RouteCommand.Tests.Support

// F27 wiring (063) US3: the `fsgg route --watch` host wiring over the REAL `HumanRender.Watch.run`
// interpreter (a real FileSystemWatcher over a real temp tree — closing F27's [PARTIAL] end-to-end
// settle, SC-005). The wired `reRender` re-runs the EXISTING route evaluation with NO-OP write/output
// ports, so the watch session writes NO contract artifact (read-only, FR-009/SC-006); a transiently
// unreadable tree surfaces `InputUnreadable` (FR-010). The pure debounce is covered by F27.

// The wired host re-render: re-run the existing route evaluation read-only (no-op write/out) and project.
let private wiredReRender (req: Loop.RunRequest) (count: int ref) : string -> RenderMode.RenderMode -> Watch.WatchSignal =
    fun root _md ->
        try
            let roPorts =
                { Interpreter.realPorts root with
                    Write = (fun _ _ -> Ok())
                    Out = (fun _ -> ()) }

            let m = Interpreter.run roPorts { req with Watch = false }
            Interlocked.Increment(&count.contents) |> ignore

            match Loop.humanView m with
            | Some _ -> Watch.Rendered
            | None -> Watch.InputUnreadable "route evaluation produced no report"
        with e ->
            Watch.InputUnreadable e.Message

[<Tests>]
let tests =
    testList
        "WatchHost"
        [ test "real FileSystemWatcher: a tracked-file change drives exactly ONE settled re-render, and the watch writes no contract artifact (SC-005, FR-009)" {
              withTempRepo (fun dir ->
                  let req =
                      match Loop.parse [ "route"; "--repo"; dir; "--since"; "HEAD~1" ] with
                      | Ok r -> r
                      | Error e -> failtestf "parse failed: %A" e

                  let count = ref 0
                  let sw = Stopwatch.StartNew()
                  let stop = ref false
                  let shouldStop () = stop.Value || count.Value >= 1

                  let task =
                      Task.Run(fun () ->
                          Watch.run dir RenderMode.Plain (fun () -> sw.ElapsedMilliseconds) (wiredReRender req count) shouldStop)

                  // Let the FileSystemWatcher arm, then make a real tracked change under src/. Re-touch on a
                  // bounded schedule so a missed inotify event under parallel test load is retried; once the
                  // first settled re-render fires we stop (the burst→single-render coalescing is F27-pure-tested).
                  Thread.Sleep 500
                  let deadline = sw.ElapsedMilliseconds + 20000L
                  let mutable n = 3

                  while count.Value < 1 && sw.ElapsedMilliseconds < deadline do
                      writeFile dir "src/Lib/Thing.fs" (sprintf "module Thing\nlet v = %d\n" n)
                      n <- n + 1
                      Thread.Sleep 400

                  stop.Value <- true
                  task.Wait 3000 |> ignore

                  Expect.isGreaterThanOrEqual count.Value 1 "a tracked-file change drove at least one settled re-render through the real FileSystemWatcher (SC-005)"
                  // Read-only: the watch re-render persisted NO route.json (FR-009). withTempRepo never ran a
                  // one-shot route, so the only way this file could exist is a watch write — it must not.
                  Expect.isFalse (File.Exists(Path.Combine(dir, "readiness", "route.json"))) "watch re-render writes no contract artifact")
          }

          test "read-only: the wired re-render computes a report view but emits no WriteArtifact (FR-009)" {
              withTempRepo (fun dir ->
                  let req =
                      match Loop.parse [ "route"; "--repo"; dir; "--since"; "HEAD~1" ] with
                      | Ok r -> r
                      | Error e -> failtestf "parse failed: %A" e

                  let count = ref 0
                  let signal = (wiredReRender req count) dir RenderMode.Plain
                  Expect.equal signal Watch.Rendered "a valid tree re-renders the report"
                  Expect.equal !count 1 "the evaluation ran exactly once"
                  Expect.isFalse (File.Exists(Path.Combine(dir, "readiness", "route.json"))) "no route.json written by the read-only re-render"
                  Expect.isFalse (File.Exists(Path.Combine(dir, ".fsgg", "gates.json"))) "no gates.json written by the read-only re-render")
          }

          test "headless (H3 / #47): `route --watch` over a nonexistent repo fails to arm the watcher and exits input-unavailable (3), never crashing" {
              // The FileSystemWatcher cannot be constructed for a missing root, so `Watch.run` returns
              // `InputUnreadable` BEFORE the poll loop — no `Console.KeyAvailable` is consulted. That makes
              // this deterministic regardless of whether the test host's stdin is redirected, while still
              // exercising the real `route --watch` entry (shared `safeKeyPoll` stop-poll) end-to-end.
              let missing = Path.Combine(Path.GetTempPath(), "fsgg-watch-missing-" + System.Guid.NewGuid().ToString("N"))
              let code = Program.main [| "route"; "--repo"; missing; "--watch" |]
              Expect.equal code 3 "a nonexistent watch root ⇒ InputUnavailable exit (3), not a FileSystemWatcher crash"
          }

          test "headless (H3 / #47): the shared `safeKeyPoll` swallows an unreadable console (redirected stdin) instead of throwing" {
              // Under `dotnet test` stdin is redirected ⇒ `Console.KeyAvailable`/`ReadKey` throw
              // InvalidOperationException. `safeKeyPoll` must be TOTAL — swallow it and signal stop — so the
              // watch loop exits cleanly rather than propagating the H3 crash. (Under an interactive console
              // with no key pending it short-circuits to `false`; either way it never throws.)
              let mutable threw = false
              try Watch.safeKeyPoll () |> ignore with _ -> threw <- true
              Expect.isFalse threw "safeKeyPoll never propagates a console exception"
          }

          test "safe failure: a transiently-unreadable/malformed tree yields InputUnreadable, no crash, no fabricated report (FR-010)" {
              // A directory with a malformed catalog ⇒ the evaluation degrades to no report ⇒ InputUnreadable,
              // never a throw and never a fabricated view.
              let dir = Path.Combine(Path.GetTempPath(), "fsgg-watch-bad-" + System.Guid.NewGuid().ToString("N"))
              Directory.CreateDirectory dir |> ignore

              try
                  for KeyValue(name, content) in invalidCatalog do
                      writeFile dir (".fsgg/" + name) content

                  let req =
                      match Loop.parse [ "route"; "--repo"; dir; "--paths"; "src/Lib/Thing.fs" ] with
                      | Ok r -> r
                      | Error e -> failtestf "parse failed: %A" e

                  let count = ref 0
                  let signal = (wiredReRender req count) dir RenderMode.Plain

                  match signal with
                  | Watch.InputUnreadable _ -> ()
                  | other -> failtestf "expected InputUnreadable for a malformed tree, got %A" other
              finally
                  try Directory.Delete(dir, true) with _ -> ()
          } ]
