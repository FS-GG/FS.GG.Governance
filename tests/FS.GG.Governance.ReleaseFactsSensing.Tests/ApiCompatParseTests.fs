module FS.GG.Governance.ReleaseFactsSensing.Tests.ApiCompatParseTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.PackEvidence.Model
open FS.GG.Governance.ReleaseFactsSensing

// 088 US1 (T009): the PURE `parseApiCompatOutput : string -> ApiBreakSignal`. FAIL-SAFE (FR-008): an empty /
// unrecognized / tool-error input is Indeterminate, NEVER NoBreakingChanges; an absent baseline ⇒ NoBaseline
// (FR-009), distinct from a tool error.
//
// SYNTHETIC DISCLOSURE (Principle V): no real .NET ApiCompat / Package-Validation run against a published
// baseline package is available in this environment (research D5: most FS.GG.Governance.* packages have no
// prior published version). The `CPxxxx` samples below are REPRESENTATIVE of ApiCompat's raw diagnostic line
// format (CP0001 type removed, CP0002 member removed, CP0003–CP0008 signature/accessibility), and the
// `BREAK …` / control-marker samples are the detector `.fsx`'s normalized protocol. Tests exercising those
// raw-tool-format samples carry the `Synthetic` token; the real-evidence path is the detector `.fsx` against
// an org-feed baseline once H4 lands. The control-marker + fail-safe behaviour is real (no tool needed).

[<Tests>]
let tests =
    testList
        "parseApiCompatOutput"
        [ test "empty / whitespace output ⇒ Indeterminate (fail-safe, NEVER NoBreakingChanges)" {
              Expect.equal (Sensing.parseApiCompatOutput "") (ApiBreakSignal.Indeterminate "empty detector output") ""
              Expect.equal (Sensing.parseApiCompatOutput "   \n  \n") (ApiBreakSignal.Indeterminate "empty detector output") ""
          }

          test "NOTPACKABLE marker ⇒ NotPackable" {
              Expect.equal (Sensing.parseApiCompatOutput "NOTPACKABLE") ApiBreakSignal.NotPackable ""
          }

          test "NOBASELINE marker ⇒ NoBaseline (FR-009, distinct from a tool error)" {
              Expect.equal (Sensing.parseApiCompatOutput "NOBASELINE") ApiBreakSignal.NoBaseline ""
          }

          test "ERROR marker ⇒ Indeterminate carrying the reason (FR-008)" {
              Expect.equal
                  (Sensing.parseApiCompatOutput "ERROR: feed unreachable")
                  (ApiBreakSignal.Indeterminate "feed unreachable")
                  ""
          }

          test "OK / NOBREAKINGCHANGES marker ⇒ NoBreakingChanges" {
              Expect.equal (Sensing.parseApiCompatOutput "OK") ApiBreakSignal.NoBreakingChanges ""
              Expect.equal (Sensing.parseApiCompatOutput "NOBREAKINGCHANGES") ApiBreakSignal.NoBreakingChanges ""
          }

          test "normalized BREAK lines ⇒ BreakingChanges with kind + origin + member (FR-013 attribution)" {
              let out =
                  "BREAK removed local FS.GG.Foo.bar\nBREAK signature inherited:FS.GG.Contracts FS.GG.Foo.baz"

              match Sensing.parseApiCompatOutput out with
              | ApiBreakSignal.BreakingChanges [ b1; b2 ] ->
                  Expect.equal b1 { Member = "FS.GG.Foo.bar"; Kind = MemberRemoved; Origin = ApiBreakOrigin.Local } "local member removed"

                  Expect.equal
                      b2
                      { Member = "FS.GG.Foo.baz"
                        Kind = MemberSignatureChanged
                        Origin = ApiBreakOrigin.Inherited(SurfaceId "FS.GG.Contracts") }
                      "inherited signature change attributed to its upstream surface"
              | other -> failtestf "expected two breaks, got %A" other
          }

          test "Synthetic: raw ApiCompat CP0002 (member removed) ⇒ BreakingChanges MemberRemoved" {
              // SYNTHETIC: representative ApiCompat raw line, not a captured real-tool run (see header).
              let out = "error CP0002: Member 'System.Void FS.GG.Foo.Bar()' exists on the left but not the right"

              match Sensing.parseApiCompatOutput out with
              | ApiBreakSignal.BreakingChanges [ b ] ->
                  Expect.equal b.Kind MemberRemoved "CP0002 ⇒ MemberRemoved"
                  Expect.stringContains b.Member "FS.GG.Foo.Bar" "the quoted member is extracted"
              | other -> failtestf "expected one break, got %A" other
          }

          test "Synthetic: raw ApiCompat CP0001 (type removed) ⇒ BreakingChanges TypeRemoved" {
              // SYNTHETIC: representative ApiCompat raw line (see header).
              let out = "error CP0001: Type 'FS.GG.Foo.Gone' exists on the left but not the right"

              match Sensing.parseApiCompatOutput out with
              | ApiBreakSignal.BreakingChanges [ b ] -> Expect.equal b.Kind TypeRemoved "CP0001 ⇒ TypeRemoved"
              | other -> failtestf "expected one break, got %A" other
          }

          test "Synthetic: raw ApiCompat CP0006 (signature change) ⇒ MemberSignatureChanged" {
              // SYNTHETIC: representative ApiCompat raw line (see header).
              let out = "CP0006: Member 'FS.GG.Foo.Quux(System.Int32)' is present but the signature differs"

              match Sensing.parseApiCompatOutput out with
              | ApiBreakSignal.BreakingChanges [ b ] -> Expect.equal b.Kind MemberSignatureChanged "CP0006 ⇒ signature changed"
              | other -> failtestf "expected one break, got %A" other
          }

          test "a raw 'error CPxxxx' break line is a break, NOT swallowed by the ERROR marker" {
              // The CP line must classify as a break even though it begins with 'error'.
              match Sensing.parseApiCompatOutput "error CP0002: Member 'X' removed" with
              | ApiBreakSignal.BreakingChanges _ -> ()
              | other -> failtestf "expected BreakingChanges, got %A" other
          }

          test "unrecognized non-empty output ⇒ Indeterminate (fail-safe)" {
              Expect.equal
                  (Sensing.parseApiCompatOutput "some banner text with no recognizable markers")
                  (ApiBreakSignal.Indeterminate "unrecognized detector output")
                  ""
          } ]
