module FS.GG.Governance.EvidenceJson.Tests.GraphFailureTests

open System.Text.Json
open Expecto
open FS.GG.Governance.Kernel
open FS.GG.Governance.EvidenceJson
open FS.GG.Governance.EvidenceJson.Tests.Support

// US3 — a malformed graph surfaces the named `GraphError` and OMITS the per-node map entirely: no nodes, no
// dependencies, never a partial/guessed effective map (FR-004, SC-003, contract C3, INV-3).

let private hasNoNodeOrDependencyKeys (root: JsonElement) =
    let names = [ for p in root.EnumerateObject() -> p.Name ]
    Expect.isFalse (List.contains "nodes" names) "no nodes key in a malformed document"
    Expect.isFalse (List.contains "dependencies" names) "no dependencies key in a malformed document"
    // graphFailure and disclosures and schemaVersion are the only keys.
    Expect.equal (List.sort names) [ "disclosures"; "graphFailure"; "schemaVersion" ] "exactly the malformed key set"

[<Tests>]
let tests =
    testList
        "GraphFailure"
        [ test "Cycle renders graphFailure.kind=cycle with the witness node order, no per-node map" {
              let root = parse (malformed (Cycle [ "a"; "b"; "a" ]) [])
              let gf = root.GetProperty("graphFailure")
              Expect.equal (strProp "kind" gf) "cycle" "cycle kind"

              let nodes = [ for n in gf.GetProperty("nodes").EnumerateArray() -> str n ]
              Expect.equal nodes [ "a"; "b"; "a" ] "cycle witness order preserved"
              hasNoNodeOrDependencyKeys root
          }

          test "UnknownNode renders graphFailure.kind=unknownNode naming the offending node" {
              let root = parse (malformed (UnknownNode "ghost") [])
              let gf = root.GetProperty("graphFailure")
              Expect.equal (strProp "kind" gf) "unknownNode" "unknownNode kind"
              Expect.equal (strProp "node" gf) "ghost" "offending node named"
              hasNoNodeOrDependencyKeys root
          }

          test "AutoSyntheticDeclared renders graphFailure.kind=autoSyntheticDeclared naming the node" {
              let root = parse (malformed (AutoSyntheticDeclared "x") [])
              let gf = root.GetProperty("graphFailure")
              Expect.equal (strProp "kind" gf) "autoSyntheticDeclared" "autoSyntheticDeclared kind"
              Expect.equal (strProp "node" gf) "x" "offending node named"
              hasNoNodeOrDependencyKeys root
          }

          test "a malformed document still carries schemaVersion and disclosures" {
              let root = parse (malformed (UnknownNode "ghost") [ "R", "j" ])
              Expect.equal (strProp "schemaVersion" root) "fsgg.evidence/v1" "schemaVersion present"
              Expect.equal (root.GetProperty("disclosures").GetArrayLength()) 1 "disclosures carried"
          } ]
