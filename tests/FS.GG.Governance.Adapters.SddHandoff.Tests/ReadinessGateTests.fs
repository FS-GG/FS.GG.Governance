module FS.GG.Governance.Adapters.SddHandoff.Tests.ReadinessGateTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Adapters.SddHandoff
open FS.GG.Governance.Adapters.SddHandoff.Model

// US3 — readiness.* → typed gate (FR-009, SC-005). A non-shippable disposition OR a non-empty
// blockingDiagnosticIds ⇒ BlockOnShip; advisory Warn otherwise. Counts/perViewState carried into the
// gate description (data-model §4).

let private readinessOf name =
    match Reader.parse (Fixtures.read name) with
    | Ok h -> Option.get h.Readiness
    | Error d -> failtestf "fixture %s should parse, got %A" name d

[<Tests>]
let tests =
    testList
        "ReadinessGate"
        [ test "readiness-blocking fixture ⇒ BlockOnShip (non-shippable disposition + blockingDiagnosticIds)" {
              let gate = Readiness.toGate "readiness/blk/governance-handoff.json" (readinessOf "readiness-blocking")
              Expect.equal gate.Maturity BlockOnShip "blocked disposition / blocking diagnostics ⇒ block-on-ship"
          }

          test "readiness-clean fixture ⇒ Warn (advisory)" {
              let gate = Readiness.toGate "readiness/cln/governance-handoff.json" (readinessOf "readiness-clean")
              Expect.equal gate.Maturity Warn "shippable + no blocking diagnostics ⇒ advisory warn"
          }

          test "a non-empty blockingDiagnosticIds forces blocking even on a shippable disposition" {
              let block =
                  { ShipDisposition = "shippable"
                    VerificationReadiness = "complete"
                    BlockingDiagnosticIds = [ "VIEW_STALE" ]
                    Counts = []
                    PerViewState = [] }

              let gate = Readiness.toGate "readiness/x/governance-handoff.json" block
              Expect.equal gate.Maturity BlockOnShip "a blocking diagnostic id ⇒ block-on-ship regardless of disposition"
          }

          test "counts and perViewState are carried into the gate description (FR-009)" {
              let gate = Readiness.toGate "readiness/blk/governance-handoff.json" (readinessOf "readiness-blocking")
              Expect.stringContains gate.Description "VIEW_STALE" "blocking diagnostic id surfaced"
              Expect.stringContains gate.Description "ledger" "perViewState carried"
              Expect.stringContains gate.Description "blocking" "counts carried"
          }

          test "the readiness gate id is domain-qualified and stable" {
              let gate = Readiness.toGate "readiness/wi-42/governance-handoff.json" (readinessOf "readiness-clean")
              Expect.equal (gateIdValue gate.Id) "sdd-handoff:readiness:wi-42" "stable domain-qualified gate id"
          } ]
