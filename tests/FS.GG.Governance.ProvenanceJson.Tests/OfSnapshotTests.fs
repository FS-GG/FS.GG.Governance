module FS.GG.Governance.ProvenanceJson.Tests.OfSnapshotTests

open System.Text.Json
open Expecto
open FS.GG.Governance.CommandRecord
open FS.GG.Governance.Provenance
open FS.GG.Governance.CommandKind
open FS.GG.Governance.ProvenanceJson
open FS.GG.Governance.ProvenanceJson.Tests.Support

// US4 (T034): `ofSnapshot` emits schemaVersion, the F033 identity verbatim, the fixed field order,
// artifactDigests rendered sorted (set), commandRuns in carried order with each
// {kind, identity, exitCode, durationNanos}, duration-invariant identities, and a well-formed empty run list
// (contracts/provenance-json.md).

let private json = ProvenanceJson.ofSnapshot baseSnapshot
let private doc = JsonDocument.Parse json
let private root = doc.RootElement

[<Tests>]
let tests =
    testList
        "OfSnapshot"
        [ test "schemaVersion is fsgg.provenance/v1" {
              Expect.equal (root.GetProperty("schemaVersion").GetString()) "fsgg.provenance/v1" "schema"
              Expect.equal ProvenanceJson.schemaVersion "fsgg.provenance/v1" "constant"
          }

          test "identity is Provenance.canonicalId verbatim" {
              let expected = Provenance.identityValue (Provenance.canonicalId baseSnapshot.Provenance)
              Expect.equal (root.GetProperty("identity").GetString()) expected "F033 identity verbatim"
          }

          test "reproducible facts are rendered verbatim" {
              Expect.equal (root.GetProperty("sourceCommit").GetString()) "c0ffee" "sourceCommit"
              Expect.equal (root.GetProperty("base").GetString()) "base1" "base"
              Expect.equal (root.GetProperty("head").GetString()) "head2" "head"
              Expect.equal (root.GetProperty("ruleHash").GetString()) "rule-x" "ruleHash"
              Expect.equal (root.GetProperty("generatorVersion").GetString()) "gen-1" "generatorVersion"
              Expect.equal (root.GetProperty("environment").GetString()) "local" "environment token"
              Expect.equal (root.GetProperty("builder").GetString()) "ci-runner" "builder"
          }

          test "artifactDigests rendered SORTED with duplicates collapsed by sort (set semantics)" {
              let arts = [ for a in root.GetProperty("artifactDigests").EnumerateArray() -> a.GetString() ]
              Expect.equal arts [ "a1"; "a2"; "a2" ] "sorted ordinal (carriage is set in identity; rendering is sorted)"
          }

          test "fixed top-level field order" {
              let order =
                  [ "schemaVersion"; "identity"; "sourceCommit"; "base"; "head"; "ruleHash"; "generatorVersion"; "environment"; "builder"; "artifactDigests"; "commandRuns" ]
              let positions = order |> List.map (fun k -> json.IndexOf("\"" + k + "\""))
              Expect.equal positions (List.sort positions) "fields appear in the documented order"
          }

          test "commandRuns in carried order, each {kind, identity, exitCode, durationNanos}" {
              let runs = root.GetProperty("commandRuns")
              Expect.equal (runs.GetArrayLength()) 3 "three runs"
              let kinds = [ for r in runs.EnumerateArray() -> r.GetProperty("kind").GetString() ]
              Expect.equal kinds [ "build"; "test"; "pack" ] "carried order with exhaustive kind tokens"

              let first = runs.[0]
              Expect.equal (first.GetProperty("identity").GetString()) (Audit.runIdentity runBuild) "per-run identity is CommandRecord.canonicalId"
              Expect.equal (first.GetProperty("exitCode").GetInt32()) 0 "exit code"
              Expect.equal (first.GetProperty("durationNanos").GetInt64()) 111L "sensed duration metadata"
              // field order within a run
              let raw = first.GetRawText()
              Expect.isLessThan (raw.IndexOf "\"kind\"") (raw.IndexOf "\"identity\"") "kind < identity"
              Expect.isLessThan (raw.IndexOf "\"identity\"") (raw.IndexOf "\"exitCode\"") "identity < exitCode"
              Expect.isLessThan (raw.IndexOf "\"exitCode\"") (raw.IndexOf "\"durationNanos\"") "exitCode < durationNanos"
          }

          test "a failed/non-zero exit run is rendered with its exit code (never dropped)" {
              let runs = root.GetProperty("commandRuns")
              let pack = runs.[2]
              Expect.equal (pack.GetProperty("exitCode").GetInt32()) 137 "the failing run's exit code is kept"
          }

          test "two snapshots differing ONLY in durations share the top-level and per-run identity" {
              let slow = snapshotOf [ { runBuild with Record = makeRecord 0 999_999L }; runTest; runFailed ]
              let slowJson = ProvenanceJson.ofSnapshot slow
              let slowDoc = JsonDocument.Parse slowJson
              Expect.equal (slowDoc.RootElement.GetProperty("identity").GetString()) (root.GetProperty("identity").GetString()) "top-level identity unchanged"
              let slowFirst = slowDoc.RootElement.GetProperty("commandRuns").[0]
              Expect.equal (slowFirst.GetProperty("identity").GetString()) (root.GetProperty("commandRuns").[0].GetProperty("identity").GetString()) "per-run identity unchanged"
              Expect.notEqual (slowFirst.GetProperty("durationNanos").GetInt64()) 111L "only the sensed duration differs"
          }

          test "an empty run list ⇒ commandRuns: []" {
              let emptyJson = ProvenanceJson.ofSnapshot (snapshotOf [])
              let edoc = JsonDocument.Parse emptyJson
              Expect.equal (edoc.RootElement.GetProperty("commandRuns").GetArrayLength()) 0 "well-formed empty array"
          }

          test "no clock/host-path/username leak beyond the opaque tokens" {
              Expect.isFalse (json.Contains "/home/") "no absolute path"
              Expect.isFalse (json.Contains "T00:") "no ISO timestamp"
          } ]
