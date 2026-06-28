module FS.GG.Governance.PackEvidence.Tests.ApiCompatibilityFactTests

open Expecto
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.PackEvidence
open FS.GG.Governance.PackEvidence.Model

// 088 US1 (T007): the PURE break × version-delta → FactState verdict table (data-model D4). Real evidence:
// real ApiBreakSignal + VersionDelta values, asserting the governing FactState. THIS module IS the project's
// break-detection / additive-change verdict corpus referenced by SC-002 (no false negatives — a real break
// under an inadequate bump is ALWAYS graded Unmet/Violated) and SC-003 (no false positives — an additive
// change, a major-bumped break, or a never-published package is ALWAYS Met). Each `test` is one D4 row.

let private aBreak = { Member = "Foo.bar"; Kind = MemberRemoved; Origin = ApiBreakOrigin.Local }
let private breaks = ApiBreakSignal.BreakingChanges [ aBreak ]

[<Tests>]
let tests =
    testList
        "apiCompatibilityFact (SC-002/SC-003 verdict corpus)"
        [ test "NoBreakingChanges + any delta ⇒ Met (SC-003: additive change never blocks)" {
              Expect.equal (Pack.apiCompatibilityFact ApiBreakSignal.NoBreakingChanges MajorBump) (Some Met) ""
              Expect.equal (Pack.apiCompatibilityFact ApiBreakSignal.NoBreakingChanges MinorOrPatchBump) (Some Met) ""
              Expect.equal (Pack.apiCompatibilityFact ApiBreakSignal.NoBreakingChanges NoForwardChange) (Some Met) ""
          }

          test "BreakingChanges + MajorBump ⇒ Met (the break is covered by a major bump)" {
              Expect.equal (Pack.apiCompatibilityFact breaks MajorBump) (Some Met) ""
          }

          test "BreakingChanges + MinorOrPatchBump ⇒ Unmet (SC-002: breaking under a minor/patch bump)" {
              Expect.equal (Pack.apiCompatibilityFact breaks MinorOrPatchBump) (Some Unmet) ""
          }

          test "BreakingChanges + NoForwardChange ⇒ Unmet (SC-002: breaking with no/equal/down bump)" {
              Expect.equal (Pack.apiCompatibilityFact breaks NoForwardChange) (Some Unmet) ""
          }

          test "NoBaseline + NoBaselineDelta ⇒ Met (FR-009 vacuous — never published)" {
              Expect.equal (Pack.apiCompatibilityFact ApiBreakSignal.NoBaseline NoBaselineDelta) (Some Met) ""
          }

          test "Indeterminate ⇒ Unrecoverable (FR-008 fail-safe — NEVER silently Met)" {
              Expect.equal
                  (Pack.apiCompatibilityFact (ApiBreakSignal.Indeterminate "feed unreachable") MajorBump)
                  (Some Unrecoverable)
                  ""

              Expect.equal
                  (Pack.apiCompatibilityFact (ApiBreakSignal.Indeterminate "assembly unreadable") NoBaselineDelta)
                  (Some Unrecoverable)
                  ""
          }

          test "NotPackable ⇒ None (FR-007 not covered — no rule fact emitted)" {
              Expect.equal (Pack.apiCompatibilityFact ApiBreakSignal.NotPackable NoBaselineDelta) None ""
          }

          test "versionDelta classifies a major bump distinctly from a minor/patch bump" {
              Expect.equal (Pack.versionDelta (Some "1.2.0") (Some "2.0.0")) MajorBump "major segment incremented"
              Expect.equal (Pack.versionDelta (Some "1.2.0") (Some "1.3.0")) MinorOrPatchBump "minor only"
              Expect.equal (Pack.versionDelta (Some "1.2.0") (Some "1.2.1")) MinorOrPatchBump "patch only"
              Expect.equal (Pack.versionDelta (Some "1.2.0") (Some "1.2.0")) NoForwardChange "equal ⇒ no forward change"
              Expect.equal (Pack.versionDelta (Some "2.0.0") (Some "1.9.9")) NoForwardChange "downgrade ⇒ no forward change"
              Expect.equal (Pack.versionDelta None (Some "1.0.0")) NoBaselineDelta "no baseline"
              Expect.equal (Pack.versionDelta (Some "1.0.0") None) NoBaselineDelta "no packed version"
          }

          test "semantic (not lexical) major classification: 9.x → 10.x is a major bump" {
              Expect.equal (Pack.versionDelta (Some "9.5.0") (Some "10.0.0")) MajorBump "10 > 9 numerically"
          }

          test "end-to-end SC-002: a real removed member under a patch bump grades Unmet" {
              // The exact scenario the gate exists to catch: a breaking change shipped under a patch bump.
              let signal =
                  ApiBreakSignal.BreakingChanges [ { Member = "PublicApi.removed"; Kind = MemberRemoved; Origin = ApiBreakOrigin.Local } ]

              let delta = Pack.versionDelta (Some "1.4.2") (Some "1.4.3")
              Expect.equal (Pack.apiCompatibilityFact signal delta) (Some Unmet) "breaking under patch ⇒ Unmet"
          }

          test "total over arbitrary version shapes — never throws" {
              Pack.versionDelta (Some "abc") (Some "x.y.z") |> ignore
              Pack.versionDelta (Some "") (Some "") |> ignore
              Expect.isTrue true ""
          } ]
