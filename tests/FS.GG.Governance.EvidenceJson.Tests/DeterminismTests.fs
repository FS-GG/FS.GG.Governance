module FS.GG.Governance.EvidenceJson.Tests.DeterminismTests

open Expecto
open FS.GG.Governance.Kernel
open FS.GG.Governance.EvidenceJson
open FS.GG.Governance.EvidenceJson.Tests.Support

// US1 — `ofReport` is byte-deterministic: identical documents yield identical bytes, and collections are
// rendered in a stable order independent of input order (FR-006, SC-002, contract C5, INV-4).

let private sampleNodes =
    [ mkNode "speckit:T2" Real AutoSynthetic NodeFreshness.Fresh "speckit"
      mkNode "speckit:T1" Synthetic Synthetic NodeFreshness.Unknown "speckit"
      mkNode "design:m1" Real Real NodeFreshness.Fresh "design-system" ]

[<Tests>]
let tests =
    testList
        "Determinism"
        [ test "identical documents render byte-identical output" {
              let doc = wellFormed sampleNodes [ "speckit:T2", "speckit:T1" ] [ "R", "j" ]
              Expect.equal (EvidenceJson.ofReport doc) (EvidenceJson.ofReport doc) "byte-identical on re-run"
          }

          test "node input order does not change the bytes (sorted by id)" {
              let a = wellFormed sampleNodes [] []
              let b = wellFormed (List.rev sampleNodes) [] []
              Expect.equal (EvidenceJson.ofReport a) (EvidenceJson.ofReport b) "order-independent nodes"
          }

          test "dependency input order does not change the bytes (sorted by (dependent, dependency))" {
              let deps = [ "b", "a"; "a", "b"; "a", "a" ]
              let a = wellFormed sampleNodes deps []
              let b = wellFormed sampleNodes (List.rev deps) []
              Expect.equal (EvidenceJson.ofReport a) (EvidenceJson.ofReport b) "order-independent dependencies"

              // Confirm the realized order is the sort.
              let root = parse a

              let pairs =
                  [ for d in root.GetProperty("dependencies").EnumerateArray() -> strProp "dependent" d, strProp "dependency" d ]

              Expect.equal pairs (List.sort deps) "dependencies sorted by (dependent, dependency)"
          }

          test "disclosure input order does not change the bytes (sorted by (rule, justification))" {
              let disc = [ "B", "y"; "A", "z"; "A", "a" ]
              let a = wellFormed sampleNodes [] disc
              let b = wellFormed sampleNodes [] (List.rev disc)
              Expect.equal (EvidenceJson.ofReport a) (EvidenceJson.ofReport b) "order-independent disclosures"

              let root = parse a

              let pairs =
                  [ for d in root.GetProperty("disclosures").EnumerateArray() -> strProp "rule" d, strProp "justification" d ]

              Expect.equal pairs (List.sort disc) "disclosures sorted by (rule, justification)"
          }

          test "no clock / absolute-path / environment leakage in the bytes" {
              let json = EvidenceJson.ofReport (wellFormed sampleNodes [] [])
              let lower = json.ToLowerInvariant()

              for token in [ "/home/"; "c:\\"; "timestamp"; "utc"; "machine" ] do
                  Expect.isFalse (lower.Contains token) (sprintf "no leaked token '%s'" token)
          } ]
