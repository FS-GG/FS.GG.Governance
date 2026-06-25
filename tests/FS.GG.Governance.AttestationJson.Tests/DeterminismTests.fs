module FS.GG.Governance.AttestationJson.Tests.DeterminismTests

open Expecto
open FS.GG.Governance.CommandKind.Model
open FS.GG.Governance.AttestationJson
open FS.GG.Governance.AttestationJson.Tests.Support

// FR-010 / FR-011 / SC-005: ofAttestation is byte-identical for identical input; a duration-only change leaves
// every identity field unchanged (only durationNanos differs); the compliance marker is always present.

[<Tests>]
let tests =
    testList
        "ofAttestation-determinism"
        [ test "byte-identical for identical input" {
              Expect.equal (AttestationJson.ofAttestation baseSummary) (AttestationJson.ofAttestation baseSummary) ""
          }

          test "a duration-only change leaves everything but durationNanos byte-identical" {
              let fast = summaryWith [ { Kind = Pack; Record = makeRecord 0 1L } ] twoPacked
              let slow = summaryWith [ { Kind = Pack; Record = makeRecord 0 999999L } ] twoPacked
              Expect.equal fast.Identity slow.Identity "the summary's identity is duration-invariant"

              // Normalize the only field that legitimately differs (the sensed durationNanos): the rest of
              // the document — incl. the `identity` field — must be byte-identical.
              let normalize (s: string) =
                  System.Text.RegularExpressions.Regex.Replace(s, "\"durationNanos\":\\d+", "\"durationNanos\":N")

              Expect.equal
                  (normalize (AttestationJson.ofAttestation fast))
                  (normalize (AttestationJson.ofAttestation slow))
                  "only durationNanos differs; identity is byte-stable"
          } ]
