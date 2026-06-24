module FS.GG.Governance.ReleaseFactsSensing.Tests.SnapshotTests

open Expecto
open FS.GG.Governance.ReleaseRules
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseFactsSensing
open FS.GG.Governance.ReleaseFactsSensing.Model
open FS.GG.Governance.ReleaseFactsSensing.Tests.Support

// US2 (acc. 1–2, FR-005): the `ReleaseSnapshot` names the concrete observed evidence behind each fact —
// present/missing metadata fields, observed-vs-baseline version, resolved-vs-expected pins (+ drift) — and
// each `Unrecoverable` family has a `None` snapshot field plus a diagnostic (ordered by ordinal).

[<Tests>]
let tests =
    testList
        "SnapshotTests"
        [ test "missing required metadata field ⇒ snapshot names present/missing AND PackageMetadata Unmet" {
              let recovered = { recoveredMet with Metadata = Ok { PresentFields = [ "authors" ] } }
              let sensed = Sensing.deriveFacts expectations recovered

              Expect.equal sensed.Facts.States.[PackageMetadata] Unmet "PackageMetadata Unmet"

              match sensed.Snapshot.Metadata with
              | Some m ->
                  Expect.equal m.Present [ "authors" ] "present fields (sorted)"
                  Expect.equal m.Missing [ "license" ] "the specific missing field (sorted)"
              | None -> failtest "expected Some metadata fact for a recovered family"
          }

          test "version equal to baseline ⇒ snapshot carries observed + baseline AND VersionBump Unmet" {
              let recovered = { recoveredMet with Version = Ok { Declared = "1.2.0" } }
              let sensed = Sensing.deriveFacts expectations recovered

              Expect.equal sensed.Facts.States.[VersionBump] Unmet "VersionBump Unmet"

              match sensed.Snapshot.Version with
              | Some v ->
                  Expect.equal v.Observed "1.2.0" "the version observed"
                  Expect.equal v.Baseline "1.2.0" "the baseline compared against"
              | None -> failtest "expected Some version fact"
          }

          test "drifted pins ⇒ snapshot names resolved/expected/drifted AND TemplatePins Unmet" {
              // "base" drifts (8.0.0 vs expected 9.0.0); add an extra resolved pin to check key-sorting.
              let recovered =
                  { recoveredMet with Pins = Ok { Resolved = Map [ "base", "8.0.0"; "aux", "1.0.0" ] } }

              let sensed = Sensing.deriveFacts expectations recovered

              Expect.equal sensed.Facts.States.[TemplatePins] Unmet "TemplatePins Unmet"

              match sensed.Snapshot.Pins with
              | Some p ->
                  Expect.equal p.Resolved [ "aux", "1.0.0"; "base", "8.0.0" ] "resolved pins key-sorted"
                  Expect.equal p.Expected [ "base", "9.0.0" ] "expected pins key-sorted"
                  Expect.equal p.Drifted [ "base" ] "the drifted template name"
              | None -> failtest "expected Some pins fact"
          }

          test "Unrecoverable families ⇒ None snapshot field + a diagnostic, diagnostics ordinal-ordered" {
              // Provenance source errored; Version expectation absent ⇒ both Unrecoverable.
              let recovered = { recoveredMet with Provenance = Error "absent: provenance record not found" }
              let exp = { expectations with VersionBaseline = None }
              let sensed = Sensing.deriveFacts exp recovered

              Expect.equal sensed.Facts.States.[Provenance] Unrecoverable "errored source ⇒ Unrecoverable"
              Expect.equal sensed.Facts.States.[VersionBump] Unrecoverable "absent expectation ⇒ Unrecoverable"
              Expect.isNone sensed.Snapshot.Provenance "no provenance fact when Unrecoverable"
              Expect.isNone sensed.Snapshot.Version "no version fact when Unrecoverable"

              let diagFamilies = sensed.Snapshot.Diagnostics |> List.map (fun d -> d.Family)
              Expect.contains diagFamilies Provenance "a diagnostic names the Provenance family"
              Expect.contains diagFamilies VersionBump "a diagnostic names the VersionBump family"

              let ordinals = diagFamilies |> List.map Release.releaseRuleKindOrdinal
              Expect.equal ordinals (List.sort ordinals) "diagnostics ordered by releaseRuleKindOrdinal (D7)"
          } ]
