module FS.GG.Governance.ReleaseFactsSensing.Tests.HandoffTests

open Expecto
open FS.GG.Governance.ReleaseRules
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseFactsSensing
open FS.GG.Governance.ReleaseFactsSensing.Model
open FS.GG.Governance.ReleaseFactsSensing.Tests.Support

// US1 (acc. 3, FR-002, SC-001): `sensed.Facts` IS the F053 `ReleaseFacts` type — it feeds `Release.evaluate`
// with NO adaptation and yields exactly one finding per declared rule. Exercised for both the pure
// `deriveFacts` and the edge `senseRelease` (over a real fake port), confirming the sensing output is exactly
// the F053 input shape.

[<Tests>]
let tests =
    testList
        "HandoffTests"
        [ test "deriveFacts.Facts feeds Release.evaluate unchanged ⇒ one finding per declared rule" {
              let sensed = Sensing.deriveFacts expectations recoveredMet
              let rules = rulesForFamilies Sensing.releaseFamilies

              // No adaptation: `sensed.Facts` (the F053 ReleaseFacts) is the second arg verbatim.
              let findings = Release.evaluate rules sensed.Facts

              Expect.equal findings.Length rules.Length "exactly one finding per declared rule (SC-001)"

              Expect.isTrue
                  (findings |> List.forall (fun f -> f.Outcome = Satisfied))
                  "all-met facts ⇒ every finding Satisfied"
          }

          test "senseRelease.Facts (edge) feeds Release.evaluate unchanged ⇒ one finding per rule" {
              let sensed = Interpreter.senseRelease metPort expectations
              let rules = rulesForFamilies Sensing.releaseFamilies
              let findings = Release.evaluate rules sensed.Facts

              Expect.equal findings.Length rules.Length "one finding per rule from the edge output too"
          }

          test "an Unmet/Unrecoverable family ⇒ a Violated finding (fail-safe carried into F053)" {
              // Provenance unrecoverable + version unmet; F053 classifies both Violated (FR-005).
              let recovered =
                  { recoveredMet with
                      Version = Ok { Declared = "1.2.0" }
                      Provenance = Error "absent" }

              let sensed = Sensing.deriveFacts expectations recovered
              let rules = rulesForFamilies Sensing.releaseFamilies
              let findings = Release.evaluate rules sensed.Facts

              let violatedKinds =
                  findings |> List.filter (fun f -> f.Outcome = Violated) |> List.map (fun f -> f.Kind) |> List.sort

              Expect.equal violatedKinds (List.sort [ VersionBump; Provenance ]) "both the Unmet and Unrecoverable family are Violated"
          } ]
