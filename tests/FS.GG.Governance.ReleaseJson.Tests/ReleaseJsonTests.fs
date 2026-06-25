module FS.GG.Governance.ReleaseJson.Tests.ReleaseJsonTests

open System.Text.Json
open Expecto
open FS.GG.Governance.ReleaseJson
open FS.GG.Governance.ReleaseJson.Tests.Support

// `ofRelease` shape (release.schema.md): fixed top-level field order; six rules in F053 composite order;
// each rule carries the documented fields; an `unrecoverable` family ⇒ a `null` evidence object.

let private parse (s: string) = JsonDocument.Parse(s).RootElement

[<Tests>]
let tests =
    testList
        "ReleaseJson"
        [ test "schemaVersion is the fixed fsgg.release/v2 literal (F26 additive bump)" {
              Expect.equal ReleaseJson.schemaVersion "fsgg.release/v2" "schema version"
          }

          test "top-level field order keeps the v1 prefix then the three F26 additive fields" {
              let json = ReleaseJson.ofRelease decisionMet sensedMet
              let root = parse json
              let names = root.EnumerateObject() |> Seq.map (fun p -> p.Name) |> List.ofSeq
              Expect.equal
                  names
                  [ "schemaVersion"; "verdict"; "exitCodeBasis"; "rules"; "evidence"; "packageEvidence"; "versionPolicy"; "attestation" ]
                  "v1 fields unchanged in order; the three additive fields appended"
          }

          test "rules has exactly six entries in F053 composite order with the documented fields" {
              let root = parse (ReleaseJson.ofRelease decisionMixed sensedMixed)
              let rules = root.GetProperty("rules").EnumerateArray() |> List.ofSeq
              Expect.equal (List.length rules) 6 "six rules"

              let kinds = rules |> List.map (fun r -> r.GetProperty("kind").GetString())

              Expect.equal
                  kinds
                  [ "versionBump"; "packageMetadata"; "templatePins"; "publishPlan"; "trustedPublishing"; "provenance" ]
                  "composite (kind-ordinal) order"

              let first = List.head rules
              let fieldNames = first.EnumerateObject() |> Seq.map (fun p -> p.Name) |> List.ofSeq

              Expect.equal
                  fieldNames
                  [ "kind"; "surface"; "factState"; "outcome"; "baseSeverity"; "effectiveSeverity"; "reason" ]
                  "per-rule field order"
          }

          test "fact states reflect met / unmet / unrecoverable from the sensed facts" {
              let root = parse (ReleaseJson.ofRelease decisionMixed sensedMixed)

              let stateOf kind =
                  root.GetProperty("rules").EnumerateArray()
                  |> Seq.find (fun r -> r.GetProperty("kind").GetString() = kind)
                  |> fun r -> r.GetProperty("factState").GetString()

              Expect.equal (stateOf "versionBump") "met" "version met"
              Expect.equal (stateOf "packageMetadata") "unmet" "metadata unmet"
              Expect.equal (stateOf "templatePins") "unrecoverable" "pins unrecoverable"
          }

          test "an unrecoverable family renders a null evidence object" {
              let root = parse (ReleaseJson.ofRelease decisionMixed sensedMixed)
              let pins = root.GetProperty("evidence").GetProperty("pins")
              Expect.equal (pins.ValueKind) JsonValueKind.Null "pins evidence is null (unrecoverable)"

              // A recovered family renders an object, not null.
              let version = root.GetProperty("evidence").GetProperty("version")
              Expect.equal (version.ValueKind) JsonValueKind.Object "version evidence is an object"
              Expect.equal (version.GetProperty("baseline").GetString()) "1.2.0" "baseline carried"
          }

          test "verdict/basis are carried verbatim from the decision" {
              let met = parse (ReleaseJson.ofRelease decisionMet sensedMet)
              Expect.equal (met.GetProperty("verdict").GetString()) "pass" "met ⇒ pass"
              Expect.equal (met.GetProperty("exitCodeBasis").GetString()) "clean" "met ⇒ clean"

              let mixed = parse (ReleaseJson.ofRelease decisionMixed sensedMixed)
              Expect.equal (mixed.GetProperty("verdict").GetString()) "fail" "mixed ⇒ fail"
              Expect.equal (mixed.GetProperty("exitCodeBasis").GetString()) "blocked" "mixed ⇒ blocked"
          } ]
