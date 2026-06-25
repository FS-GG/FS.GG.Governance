module FS.GG.Governance.VerifyCommand.Tests.HumanTextParityTests

open Expecto
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.FreshnessResolution
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.HumanText
open FS.GG.Governance.VerifyCommand
open FS.GG.Governance.VerifyCommand.Tests.Support

// F27 wiring (063) US1: the `verify` host's text render branch delegates to the SHARED `HumanText.ofVerifyDecision`
// projection over the SAME `Ship.ShipDecision` the verify.json path serializes (FR-001, report-object identity),
// is ANSI-free (SC-003), keeps the host's operational `wrote` line (FR-003), and never touches the verify.json
// contract (SC-002 — proven byte-identical against the genuine F056 `VerifyJson.ofVerifyDecision`). The
// deliberate `--json` rejection / `--format` semantics are NOT changed by this US1 step.

// A `--since` scope so the fake git port supplies a base/head Range ⇒ gates resolve their freshness facts and the
// cache report carries entries (mirrors CurrencyTests).
let private srcScope = Loop.Since "HEAD~1"
let private srcCandidates = [ gp "src/Lib/Thing.fs" ]

let private esc = "" // an ANSI/CSI escape introducer — must NEVER appear in plain text

// The CacheEligibilityReport the host's delegated render recomputes from the model (mirrors Loop.cacheReportOf).
let private cacheReportOf (model: Loop.Model) : CacheEligibilityReport option =
    match model.Sensed, model.Store with
    | Some sensed, Some store ->
        let r = FreshnessResolution.resolve model.SelectedGates sensed
        let cands = FreshnessResolution.entries r |> List.choose FreshnessResolution.candidate
        Some(CacheEligibility.evaluate cands store)
    | _ -> None

[<Tests>]
let tests =
    testList
        "HumanTextParity (US1)"
        [ test "no-`--json`: the text summary contains the HumanText.ofVerifyDecision projection of the resolved ShipDecision, ANSI-free, with the host `wrote` line" {
              let cap = newCapture ()
              let req = requestForProfile srcScope Loop.Text Standard
              let model = Interpreter.run (fakePortsExec validCatalog gitSrcChange fakeSensor absentStoreReader fakeExecPortFail cap) req
              let summary = Expect.wantSome (List.tryHead cap.Emits) "a text summary was emitted"

              let decision = Expect.wantSome model.Decision "the run resolved a verify decision"
              Expect.isNonEmpty model.SelectedGates "the src change selects gates"

              // Report-object identity: the value handed to HumanText.of* is the SAME ShipDecision (+ cache
              // report + outcomes) the host holds — not a separately-computed summary.
              let projection = HumanText.ofVerifyDecision decision (cacheReportOf model) model.Outcomes
              Expect.stringContains summary projection "summary embeds the shared HumanText projection verbatim"

              // ANSI-free (SC-003): no escape introducer anywhere in the plain summary.
              Expect.isFalse (summary.Contains esc) "plain summary carries no ANSI/CSI escape"

              // Host operational line preserved and distinct from the report facts (FR-003).
              Expect.stringContains summary "wrote " "host `wrote` operational line preserved"
              Expect.stringContains summary req.VerifyOut "names the verify.json path it wrote"
          }

          // ── verify.json byte-identity golden (SC-002) — the wiring touches the human branch only ──

          test "JsonGolden: verify.json is byte-identical to the F056 VerifyJson projection for identical repo state (SC-002)" {
              let cap = newCapture ()
              let req = requestForProfile srcScope Loop.Json Strict
              Interpreter.run (fakePortsExec validCatalog gitSrcChange fakeSensor absentStoreReader fakeExecPortFail cap) req |> ignore

              let snap = snapshotOf gitSrcChange (sinceOpts "HEAD~1")
              let expected = verifyExpectedWith fakeExecPortFail validCatalog srcCandidates Strict (Some snap)

              Expect.equal (writtenVerify cap |> Option.map snd) (Some expected) "verify.json bytes unchanged by the human-branch wiring"

              // The `--json` stdout summary stays the persisted document verbatim, byte-identical across runs.
              let cap2 = newCapture ()
              Interpreter.run (fakePortsExec validCatalog gitSrcChange fakeSensor absentStoreReader fakeExecPortFail cap2) req |> ignore
              Expect.equal (writtenVerify cap2 |> Option.map snd) (writtenVerify cap |> Option.map snd) "verify.json byte-identical across runs"
          } ]
