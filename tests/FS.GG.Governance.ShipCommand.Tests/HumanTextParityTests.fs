module FS.GG.Governance.ShipCommand.Tests.HumanTextParityTests

open Expecto
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.FreshnessResolution
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.HumanText
open FS.GG.Governance.ShipCommand
open FS.GG.Governance.ShipCommand.Tests.Support

// F27 wiring (063) US1: the `ship` host's text render branch delegates to the SHARED `HumanText.ofShipDecision`
// projection over the SAME `Ship.ShipDecision` the audit.json path serializes (FR-001, report-object identity
// SC-001), is ANSI-free (SC-003), keeps the host's operational `wrote` line (FR-003), and never touches the
// JSON contract (SC-002 — proven byte-identical against the genuine F025 projection).

let private runWith files git scope format =
    let req = requestForLevers scope format Gate Standard
    let cap = newCapture ()
    let model = Interpreter.run (fakePorts files git cap req) req
    req, cap, model

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
        "HumanTextParity"
        [ test "no-`--json`: the text summary contains the HumanText.ofShipDecision projection of the resolved ShipDecision, ANSI-free, with the host `wrote` line (US1)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let req, cap, model = runWith validCatalog git Loop.DefaultRange Loop.Text
              let summary = Expect.wantSome (List.tryHead cap.Emits) "a text summary was emitted"

              let decision = Expect.wantSome model.Decision "the host resolved a ShipDecision"

              // Report-object identity (SC-001): the value handed to HumanText.of* is the SAME ShipDecision
              // (+ cache report + outcomes) the host holds — not a separately-computed summary.
              let projection = HumanText.ofShipDecision decision (cacheReportOf model) model.Outcomes
              Expect.stringContains summary projection "summary embeds the shared HumanText projection verbatim"

              // ANSI-free (SC-003): no escape introducer anywhere in the plain summary.
              Expect.isFalse (summary.Contains esc) "plain summary carries no ANSI/CSI escape"

              // Host operational line preserved and distinct from the report facts (FR-003).
              Expect.stringContains summary "wrote " "host `wrote` operational line preserved"
              Expect.stringContains summary req.AuditOut "names the audit.json path it wrote"
          }

          // ── JSON byte-identity golden (SC-002) — the wiring touches the human branch only ──

          test "JsonGolden: audit.json is byte-identical to the F025 projection for identical repo state (SC-002)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let _, cap, _ = runWith validCatalog git Loop.DefaultRange Loop.Json
              let candidates = candidatesOf git defaultOpts
              let expected = auditExpected validCatalog candidates Gate Standard (Some(snapshotOf git defaultOpts))

              Expect.equal (writtenAudit cap |> Option.map snd) (Some expected) "audit.json bytes unchanged by the human-branch wiring"

              // The `--json` stdout summary equals the persisted contract and is stable across runs.
              let _, capJson, _ = runWith validCatalog git Loop.DefaultRange Loop.Json
              let j = Expect.wantSome (List.tryHead capJson.Emits) "json summary emitted"
              Expect.equal j (writtenAudit capJson |> Option.map snd |> Option.defaultValue "") "--json stdout = the persisted audit.json"
              Expect.equal (writtenAudit capJson) (writtenAudit cap) "audit.json byte-identical across runs"
          } ]
