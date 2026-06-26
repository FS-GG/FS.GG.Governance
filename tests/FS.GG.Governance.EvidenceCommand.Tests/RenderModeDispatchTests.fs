module FS.GG.Governance.EvidenceCommand.Tests.RenderModeDispatchTests

open Expecto
open FS.GG.Governance.Kernel
open FS.GG.Governance.EvidenceJson
open FS.GG.Governance.EvidenceCommand
open FS.GG.Governance.EvidenceCommand.Tests.Support

// US1 — `--format json` emits the contracted document bytes; `--format human`/`--plain` emit a plain digest.
// The human view exposes no field the JSON document lacks and adds no verdict / exit-code / timestamp / path
// (parity + information-only).

let private projectedModel (format: Loop.OutputFormat) =
    let r = report [ reportNode "speckit:T1" Real Real (Some Freshness.Fresh) "speckit" ] []
    let m0 = Loop.init (requestWith "o.json" format) |> fst
    Loop.update (Loop.Reported(Ok r)) m0 |> fst

[<Tests>]
let tests =
    testList
        "RenderModeDispatch"
        [ test "--format json renders exactly the contracted evidence.json bytes" {
              let model = projectedModel Loop.Json
              Expect.equal (Loop.render model Loop.Json) (Option.get model.Doc) "json render is the document bytes"
          }

          test "--format human renders a plain digest of the same document" {
              let model = projectedModel Loop.Human
              let text = Loop.render model Loop.Human
              Expect.stringContains text "speckit:T1" "names the node"
              Expect.stringContains text "declared=Real" "shows declared state"
              Expect.stringContains text "effective=Real" "shows effective state"
          }

          test "the human view adds no verdict / exit-code / timestamp / path (information-only)" {
              let text = (Loop.render (projectedModel Loop.Human) Loop.Human).ToLowerInvariant()

              for banned in [ "verdict"; "exit"; "ship"; "merge"; "timestamp"; "/home/"; "blocking" ] do
                  Expect.isFalse (text.Contains banned) (sprintf "human view must not carry '%s'" banned)
          }

          test "via the interpreter, the json summary emitted to stdout equals the written document bytes" {
              let r = report [ reportNode "speckit:T1" Real Real (Some Freshness.Fresh) "speckit" ] []
              let ports, cap = fakePorts (Ok r)
              let _model = Interpreter.run ports (requestWith "o.json" Loop.Json)

              let _, written = List.head cap.Writes
              Expect.equal cap.Out [ written ] "the json stdout line equals the written artifact bytes"
          } ]
