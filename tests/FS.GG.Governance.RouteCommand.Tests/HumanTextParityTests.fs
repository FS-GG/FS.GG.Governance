module FS.GG.Governance.RouteCommand.Tests.HumanTextParityTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates
open FS.GG.Governance.Route.Model
open FS.GG.Governance.FreshnessResolution
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.HumanText
open FS.GG.Governance.RouteCommand
open FS.GG.Governance.RouteCommand.Tests.Support

// F27 wiring (063) US1: the `route` host's text render branch delegates to the SHARED `HumanText.ofRouteResult`
// projection over the SAME `RouteResult` the *Json path serializes (FR-001, report-object identity SC-001),
// is ANSI-free (SC-003), keeps the host's operational `wrote` lines (FR-003), and never touches the JSON
// contract (SC-002 — proven byte-identical against the genuine F020/F021 projections).

let private runWith files git scope format =
    let req = requestFor scope format
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
        [ test "no-`--json`: the text summary contains the HumanText.ofRouteResult projection of the resolved RouteResult, ANSI-free, with host `wrote` lines (US1)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let req, cap, model = runWith validCatalog git Loop.DefaultRange Loop.Text
              let summary = Expect.wantSome (List.tryHead cap.Emits) "a text summary was emitted"

              let result = Option.get model.Result
              Expect.isNonEmpty result.SelectedGates "the src change selects gates"

              // Report-object identity (SC-001): the value handed to HumanText.of* is the SAME RouteResult
              // (+ cache report + outcomes) the host holds — not a separately-computed summary.
              let projection = HumanText.ofRouteResult result (cacheReportOf model) model.Outcomes
              Expect.stringContains summary projection "summary embeds the shared HumanText projection verbatim"

              // ANSI-free (SC-003): no escape introducer anywhere in the plain summary.
              Expect.isFalse (summary.Contains esc) "plain summary carries no ANSI/CSI escape"

              // Host operational lines preserved and distinct from the report facts (FR-003).
              Expect.stringContains summary "wrote " "host `wrote` operational line preserved"
              Expect.stringContains summary req.RouteOut "names the route.json path it wrote"
              Expect.stringContains summary req.GatesOut "names the gates.json path it wrote"
          }

          test "clean / nothing-to-report: a routine-only change still renders the HumanText projection ANSI-free and writes byte-identical JSON (US1 clean state, SC-002)" {
              // A change under a non-governed path selects no gates — the "clean" human view per spec Edge Cases.
              let git = gitWithChanges [ 'M', "notes.txt" ]
              let _, cap, model = runWith validCatalog git Loop.DefaultRange Loop.Text
              let summary = Expect.wantSome (List.tryHead cap.Emits) "a text summary was emitted even when nothing routes"

              let result = Option.get model.Result
              Expect.isEmpty result.SelectedGates "a routine path selects nothing (clean state)"

              let projection = HumanText.ofRouteResult result (cacheReportOf model) model.Outcomes
              Expect.stringContains summary projection "clean-state summary is the HumanText projection"
              Expect.isFalse (summary.Contains esc) "clean-state plain summary carries no ANSI escape"

              // Clean-state JSON stays byte-identical to the genuine F020/F021 projection (SC-002 anchor).
              let candidates = candidatesOf git defaultOpts
              let expectedGates, expectedRoute = projectExpected validCatalog candidates (Some(snapshotOf git defaultOpts))
              Expect.equal (writtenOf cap Loop.RouteArtifact |> Option.map snd) (Some expectedRoute) "clean route.json byte-identical to RouteJson projection"
              Expect.equal (writtenOf cap Loop.GatesArtifact |> Option.map snd) (Some expectedGates) "clean gates.json byte-identical to GatesJson projection"
          }

          // ── JSON byte-identity goldens (T008 / SC-002) — the wiring touches the human branch only ──

          test "JsonGolden: route.json + gates.json are byte-identical to the F020/F021 projections for identical repo state (SC-002)" {
              let git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
              let _, cap, _ = runWith validCatalog git Loop.DefaultRange Loop.Json
              let candidates = candidatesOf git defaultOpts
              let expectedGates, expectedRoute = projectExpected validCatalog candidates (Some(snapshotOf git defaultOpts))

              Expect.equal (writtenOf cap Loop.RouteArtifact |> Option.map snd) (Some expectedRoute) "route.json bytes unchanged by the human-branch wiring"
              Expect.equal (writtenOf cap Loop.GatesArtifact |> Option.map snd) (Some expectedGates) "gates.json bytes unchanged by the human-branch wiring"

              // The persisted contract and the `--json` stdout summary are stable across runs.
              let _, cap2, _ = runWith validCatalog git Loop.DefaultRange Loop.Json
              Expect.equal (writtenOf cap2 Loop.RouteArtifact) (writtenOf cap Loop.RouteArtifact) "route.json byte-identical across runs"
          } ]
