module FS.GG.Governance.EvidenceCommand.Tests.ExitInformationTests

open Expecto
open FS.GG.Governance.EvidenceCommand
open FS.GG.Governance.EvidenceCommand.Tests.Support

// US1 (FR-007 / Principle VI) — the host's exit code is OPERATIONAL ONLY, and an operational failure surfaces a
// diagnostic distinguishing absent/bad input from a tool defect, never a fabricated "all effective" document.

[<Tests>]
let tests =
    testList
        "ExitInformation"
        [ test "the exit-code mapping is operational only (0 / 2 / 3 / 4), never a ship/merge code" {
              Expect.equal (Loop.exitCode Loop.Success) 0 "success 0"
              Expect.equal (Loop.exitCode Loop.UsageError') 2 "usage 2"
              Expect.equal (Loop.exitCode Loop.InputUnavailable) 3 "input-unavailable 3"
              Expect.equal (Loop.exitCode Loop.ToolError) 4 "tool-error 4"
          }

          test "an absent/unreadable input maps to InputUnavailable (3) with a diagnostic, and writes NOTHING" {
              let ports, cap = fakePorts (Error(Loop.InputMissing "root does not exist"))
              let model = Interpreter.run ports (requestWith "out.json" Loop.Json)

              Expect.equal model.Exit Loop.InputUnavailable "input-unavailable"
              Expect.equal (Loop.exitCode model.Exit) 3 "exit 3"
              Expect.isEmpty cap.Writes "no artifact fabricated on missing input"
              Expect.isNonEmpty model.Diagnostics "a diagnostic is surfaced"
              Expect.equal (List.head model.Diagnostics).Category Loop.InputUnavailable "diagnostic categorized as input-unavailable"
          }

          test "an interpreter/tool defect maps to ToolError (4), distinct from missing input" {
              let ports, cap = fakePorts (Error(Loop.ToolFault "host blew up"))
              let model = Interpreter.run ports (requestWith "out.json" Loop.Json)

              Expect.equal model.Exit Loop.ToolError "tool-error"
              Expect.equal (Loop.exitCode model.Exit) 4 "exit 4"
              Expect.isEmpty cap.Writes "no artifact on tool defect"
              Expect.equal (List.head model.Diagnostics).Category Loop.ToolError "diagnostic categorized as tool-error"
          }

          test "a write failure maps to ToolError (4)" {
              let r = report [ reportNode "a" FS.GG.Governance.Kernel.Real FS.GG.Governance.Kernel.Real None "speckit" ] []
              let ports, _ = fakePortsFailingWrite (Ok r)
              let model = Interpreter.run ports (requestWith "out.json" Loop.Json)
              Expect.equal model.Exit Loop.ToolError "write failure ⇒ tool-error"
          } ]
