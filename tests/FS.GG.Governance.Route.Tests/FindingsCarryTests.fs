module FS.GG.Governance.Route.Tests.FindingsCarryTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Routing.Model
open FS.GG.Governance.Route.Tests.Support

// US3 (P2): the route carries the F017 `FindingReport` UNCHANGED alongside the selected gates —
// one value explaining both what runs and what is unclassified; an empty report stays empty (a
// success); a finding-bearing `UnmatchedInRoot` path selects no gate yet its finding is present
// (FR-005, SC-003). Inputs are REAL: a `Findings.findUnknownGovernedPaths` report.

let private fixtureFacts =
    facts
        "src"
        [ "src/build/**", "build" ]
        []
        [ check "build" "tests" None Cheap ]
        []

[<Tests>]
let tests =
    testList
        "FindingsCarry"
        [ test "a non-empty F017 report is carried UNCHANGED alongside the selected gates (AS1, SC-003)" {
              // `src/loose/x.fs` is in-root but matches no glob → F017 yields an UnknownGovernedPath.
              let report = reportOf fixtureFacts [ "src/build/Core.fs"; "src/loose/x.fs" ]
              let findings = findingsOf fixtureFacts report

              // sanity: the fixture really produced a finding
              Expect.isNonEmpty findings.Findings "fixture must produce at least one finding"

              let r = FS.GG.Governance.Route.Route.select (registryOf fixtureFacts) report findings
              Expect.equal r.Findings findings "the F017 report is carried byte-identical (same value, unchanged)"
              Expect.isNonEmpty r.SelectedGates "and gates are still selected alongside the findings"
          }

          test "an empty F017 report → an empty finding list, a success not an error (AS2)" {
              // A change touching only a routed path → no unknown-governed-path finding.
              let r = selectOf fixtureFacts [ "src/build/Core.fs" ]
              Expect.isEmpty r.Findings.Findings "empty findings stay empty — never a fabricated 'all clear'"
              Expect.isNonEmpty r.SelectedGates "the route still succeeds with selected gates"
          }

          test "a routed gate and a finding-bearing UnmatchedInRoot path coexist (AS3)" {
              let report = reportOf fixtureFacts [ "src/build/Core.fs"; "src/loose/x.fs" ]
              let findings = findingsOf fixtureFacts report
              let r = FS.GG.Governance.Route.Route.select (registryOf fixtureFacts) report findings

              // the finding's path selects nothing...
              let findingPaths = r.Findings.Findings |> List.map (fun f -> f.Path)
              Expect.contains findingPaths (gp "src/loose/x.fs") "the unmatched path produced a finding"

              let selectingPaths =
                  r.SelectedGates |> List.collect (fun sg -> sg.SelectingPaths |> List.map (fun p -> p.Path))
              Expect.isFalse
                  (selectingPaths |> List.contains (gp "src/loose/x.fs"))
                  "the finding-bearing path selected no gate — the two facts coexist"

              // ...while the routed path still selects its gate
              Expect.equal
                  (r.SelectedGates |> List.map (fun sg -> gateIdValue sg.Gate.Id))
                  [ "build:tests" ]
                  "the routed path's gate is selected"
          } ]
