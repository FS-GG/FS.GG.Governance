module FS.GG.Governance.CacheEligibilityCommand.Tests.HumanTextParityTests

open Expecto
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.FreshnessResolution
open FS.GG.Governance.CacheEligibility
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.HumanText
open FS.GG.Governance.CacheEligibilityCommand
open FS.GG.Governance.CacheEligibilityCommand.Tests.Support

// F27 wiring (063) US1: the `cache-eligibility` host's human render branch delegates to the SHARED
// `HumanText.ofCacheEligibilityReport` projection over the SAME `CacheEligibilityReport` the
// cache-eligibility.json path serializes (FR-001, report-object identity SC-001), is ANSI-free (SC-003),
// keeps the host's operational `wrote` lines (FR-003), and never touches the JSON contract (SC-002 — proven
// byte-identical against the genuine F041/F042/F043 cores).

let private git = gitWithChanges [ 'M', "src/Lib/Thing.fs" ]
let private candidates = candidatesOf git defaultOpts
let private gates = selectedGatesOf validCatalog candidates
let private baseHead = baseHeadOfSnapshot (snapshotOf git defaultOpts)
let private sensed = assembleSensed fixedSensor gates baseHead

let private esc = "" // an ANSI/CSI escape introducer — must NEVER appear in plain text

let private runWith sensor store format =
    let req = requestFor Loop.DefaultRange format
    let cap = newCapture ()
    let model = Interpreter.run (fakePorts validCatalog git sensor store cap req) req
    req, cap, model

// The CacheEligibilityReport the host's delegated render recomputes from the model (mirrors Loop.cacheReportOf).
let private cacheReportOf (model: Loop.Model) : CacheEligibilityReport option =
    match model.Sensed, model.Store with
    | Some s, Some st ->
        let r = FreshnessResolution.resolve model.SelectedGates s
        let cands = FreshnessResolution.entries r |> List.choose FreshnessResolution.candidate
        Some(CacheEligibility.evaluate cands st)
    | _ -> None

[<Tests>]
let tests =
    testList
        "HumanTextParity"
        [ test "human summary contains the HumanText.ofCacheEligibilityReport projection of the resolved report, ANSI-free, with host `wrote` lines (US1)" {
              Expect.isNonEmpty gates "the src change selects gates (fixture sanity)"
              // A store that makes the first gate reusable ⇒ the projection carries both verdict shapes.
              let store = storeMakingReusable [ List.head gates ] sensed (fun _ -> "ev-1")
              let req, cap, model = runWith fixedSensor (storeReaderOf (Ok(Some store))) Loop.Human
              let summary = Expect.wantSome (List.tryHead cap.Emits) "a human summary was emitted"

              // Report-object identity (SC-001): the value handed to HumanText.of* is the SAME
              // CacheEligibilityReport the host holds — not a separately-computed summary.
              let report = Expect.wantSome (cacheReportOf model) "both senses arrived ⇒ a recomputable report"
              let projection = HumanText.ofCacheEligibilityReport report
              Expect.stringContains summary projection "summary embeds the shared HumanText projection verbatim"

              // ANSI-free (SC-003): no escape introducer anywhere in the plain summary.
              Expect.isFalse (summary.Contains esc) "plain summary carries no ANSI/CSI escape"

              // Host operational lines preserved and distinct from the report facts (FR-003).
              Expect.stringContains summary "wrote " "host `wrote` operational line preserved"
              Expect.stringContains summary req.CacheOut "names the cache-eligibility.json path it wrote"
              Expect.stringContains summary req.UnresolvedOut "names the unresolved sidecar path it wrote"
          }

          test "all-must-recompute (empty store): the human summary is still the HumanText projection, ANSI-free (US1)" {
              let _, cap, model = runWith fixedSensor (storeReaderOf (Ok None)) Loop.Human
              let summary = Expect.wantSome (List.tryHead cap.Emits) "a human summary was emitted"

              let report = Expect.wantSome (cacheReportOf model) "report recomputable"
              let projection = HumanText.ofCacheEligibilityReport report
              Expect.stringContains summary projection "empty-store summary is the HumanText projection"
              Expect.isFalse (summary.Contains esc) "empty-store plain summary carries no ANSI escape"
          }

          // ── JSON byte-identity golden (SC-002) — the wiring touches the human branch only ──

          test "JsonGolden: cache-eligibility.json is byte-identical to the genuine F041/F042 projection (SC-002)" {
              let store = storeMakingReusable [ List.head gates ] sensed (fun _ -> "ev-1")
              let _, cap, _ = runWith fixedSensor (storeReaderOf (Ok(Some store))) Loop.Human

              Expect.equal
                  (writtenOf cap Loop.CacheArtifact |> Option.map snd)
                  (Some(expectedCacheDoc gates sensed store))
                  "cache-eligibility.json bytes unchanged by the human-branch wiring"

              // The persisted contract is stable across runs.
              let _, cap2, _ = runWith fixedSensor (storeReaderOf (Ok(Some store))) Loop.Human
              Expect.equal (writtenOf cap2 Loop.CacheArtifact) (writtenOf cap Loop.CacheArtifact) "cache-eligibility.json byte-identical across runs"
          } ]
