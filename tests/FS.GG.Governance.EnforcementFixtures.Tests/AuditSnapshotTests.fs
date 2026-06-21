module FS.GG.Governance.EnforcementFixtures.Tests.AuditSnapshotTests

open System.Text.Json
open Expecto
open FS.GG.Governance.EnforcementFixtures.Tests.Generator
open FS.GG.Governance.EnforcementFixtures.Tests.Support

// Per-scenario byte-equality, partition, no-hide, and coverage guards for the blocking-altering
// `audit.json` snapshots (FR-007..FR-010, SC-004). Each snapshot is the VERBATIM merged projection
// `ofShipDecision (rollup …)`; the tests inspect the emitted bytes via a read-only `JsonDocument`,
// never recomputing the verdict the F025 contract already fixed.

let private relPath (s: Scenario) : string = "audit-snapshots/" + s.Name + ".audit.json"

/// The item objects of a named section, in emitted order.
let private section (doc: JsonDocument) (name: string) : JsonElement list =
    [ for it in doc.RootElement.GetProperty(name).EnumerateArray() -> it ]

let private sectionCount (doc: JsonDocument) (name: string) : int =
    doc.RootElement.GetProperty(name).GetArrayLength()

[<Tests>]
let tests =
    testList
        "F028 audit snapshots"
        [ testList
              "byte-equality (FR-008)"
              [ for s in scenarios ->
                    test s.Name { blessOrCompare (relPath s) (snapshotFor s) } ]

          testList
              "partition lands in the expected section (T020)"
              [ for s in scenarios ->
                    test s.Name {
                        use doc = JsonDocument.Parse(snapshotFor s)

                        let total =
                            sectionCount doc "blockers" + sectionCount doc "warnings" + sectionCount doc "passing"

                        Expect.equal total 1 "each scenario has exactly one enforced item"

                        Expect.equal
                            (sectionCount doc s.ExpectedSection)
                            1
                            (sprintf "the %s-dialed item must land in '%s'" s.DialUnderTest s.ExpectedSection)
                    } ]

          test "no-hide — every relaxed-blocker warning carries both base and effective severity + reason (FR-009, SC-004)" {
              for s in scenarios do
                  if s.ExpectedSection = "warnings" then
                      use doc = JsonDocument.Parse(snapshotFor s)
                      let item = List.exactlyOne (section doc "warnings")
                      let enforcement = item.GetProperty "enforcement"
                      let baseSeverity = enforcement.GetProperty("baseSeverity").GetString()
                      let effectiveSeverity = enforcement.GetProperty("effectiveSeverity").GetString()
                      let reason = enforcement.GetProperty("reason").GetString()

                      Expect.equal baseSeverity "blocking" (sprintf "%s: a relaxed blocker carries base severity 'blocking'" s.Name)
                      Expect.equal effectiveSeverity "advisory" (sprintf "%s: a relaxed blocker shows effective severity 'advisory'" s.Name)
                      Expect.notEqual baseSeverity effectiveSeverity (sprintf "%s: base and effective severity must differ (no-hide)" s.Name)
                      Expect.isFalse (System.String.IsNullOrWhiteSpace reason) (sprintf "%s: the reason must be non-empty" s.Name)
          }

          test "coverage — every blocking-altering dial is represented (FR-010)" {
              let dials = scenarios |> List.map (fun s -> s.DialUnderTest) |> Set.ofList

              for required in [ "maturity"; "base severity"; "profile"; "run mode" ] do
                  Expect.isTrue (Set.contains required dials) (sprintf "no scenario covers the '%s' dial" required)
          } ]
