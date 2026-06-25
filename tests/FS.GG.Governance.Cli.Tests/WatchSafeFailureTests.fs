module FS.GG.Governance.Cli.Tests.WatchSafeFailureTests

open Expecto
open FS.GG.Governance.HumanText
open FS.GG.Governance.HumanRender
open FS.GG.Governance.HumanRender.Watch

// T036 [US3]: a Rerendered (InputUnreadable …) from a transiently-unreadable/partial tree surfaces a
// clear INPUT signal in LastSignal (distinct from a tool defect) — no swallowed error, no crash, no
// fabricated report — and is SUPERSEDED by the next settled re-render (FR-012).

[<Tests>]
let tests =
    testList
        "WatchSafeFailure"
        [ test "an InputUnreadable signal is recorded as a clear input signal, not a crash" {
              let model0, _ = init "/repo" RenderMode.Plain
              let m1, effects = update (Rerendered(InputUnreadable "partial write in progress")) model0

              Expect.equal m1.LastSignal (InputUnreadable "partial write in progress") "input signal recorded"
              Expect.isEmpty effects "recording a signal requests no effect (no crash, no contract)"
          }

          test "the input signal is superseded by the next settled re-render" {
              let model0, _ = init "/repo" RenderMode.Plain
              let m1, _ = update (Rerendered(InputUnreadable "transient")) model0
              let m2, _ = update (Rerendered Rendered) m1
              Expect.equal m2.LastSignal Rendered "a later settled render supersedes the input signal"
          }

          test "an input signal never fabricates a verdict or pending change" {
              let model0, _ = init "/repo" RenderMode.Rich
              let m1, _ = update (Rerendered(InputUnreadable "x")) model0
              Expect.equal m1.PendingSince None "no spurious pending change is invented"
          } ]
