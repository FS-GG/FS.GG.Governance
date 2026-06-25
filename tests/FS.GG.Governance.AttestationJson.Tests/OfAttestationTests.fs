module FS.GG.Governance.AttestationJson.Tests.OfAttestationTests

open Expecto
open FS.GG.Governance.AttestationJson
open FS.GG.Governance.AttestationJson.Tests.Support

// SC-005 / FR-015: ofAttestation emits fsgg.attestation/v1 in a fixed field order, subjects sorted by name,
// artifactDigests sorted (set), runs in carried order, the compliance marker always present.

let private indexOf (haystack: string) (needle: string) = haystack.IndexOf(needle)

[<Tests>]
let tests =
    testList
        "ofAttestation"
        [ test "schemaVersion is fsgg.attestation/v1" {
              Expect.equal AttestationJson.schemaVersion "fsgg.attestation/v1" ""
              let json = AttestationJson.ofAttestation baseSummary
              Expect.stringContains json "\"schemaVersion\":\"fsgg.attestation/v1\"" ""
          }

          test "fixed field order: schemaVersion < compliance < complianceNote < identity < builder < subjects < materials < invocation" {
              let json = AttestationJson.ofAttestation baseSummary
              let order =
                  [ "\"schemaVersion\""
                    "\"compliance\""
                    "\"complianceNote\""
                    "\"identity\""
                    "\"builder\""
                    "\"subjects\""
                    "\"materials\""
                    "\"invocation\"" ]
              let positions = order |> List.map (indexOf json)
              Expect.allEqual (positions |> List.filter (fun p -> p < 0)) -1 "all fields present"
              Expect.equal positions (List.sort positions) "fields appear in the fixed order"
          }

          test "subjects sorted by name; each carries name/version/digest" {
              let json = AttestationJson.ofAttestation baseSummary
              Expect.isLessThan (indexOf json "out/A.nupkg") (indexOf json "out/B.nupkg") "subjects sorted by name"
              Expect.stringContains json "\"name\":\"out/A.nupkg\"" ""
              Expect.stringContains json "\"version\":\"1.1.0\"" ""
          }

          test "materials.artifactDigests rendered sorted (set semantics)" {
              let json = AttestationJson.ofAttestation baseSummary
              Expect.isLessThan (indexOf json "\"a1\"") (indexOf json "\"z9\"") "digests sorted ordinally"
          }

          test "compliance token + human note always present" {
              let json = AttestationJson.ofAttestation baseSummary
              Expect.stringContains json "\"compliance\":\"compatible-shape-not-formal-compliance\"" ""
              Expect.stringContains json "NOT a claim of formal" "the not-formal-compliance note is present"
          }

          test "invocation.runs carry kind/identity/exitCode/durationNanos; a failed run keeps its sentinel" {
              let summary = summaryWith [ failedPackRun ] (Support.twoPacked)
              let json = AttestationJson.ofAttestation summary
              Expect.stringContains json "\"kind\":\"pack\"" ""
              Expect.stringContains json "\"exitCode\":137" "the failed run keeps its sentinel exit"
              Expect.stringContains json "\"durationNanos\":" "duration emitted as sensed metadata"
          }

          test "a failed-build summary ⇒ subjects: [] (no attested subject)" {
              let failedPack = Support.twoPacked // packed subjects come from pack evidence; use an empty pack here
              let emptyPack = FS.GG.Governance.PackEvidence.Pack.evaluatePack Map.empty [ ]
              let summary = summaryWith [ failedPackRun ] emptyPack
              let json = AttestationJson.ofAttestation summary
              ignore failedPack
              Expect.stringContains json "\"subjects\":[]" "no subject when nothing was produced"
          } ]
