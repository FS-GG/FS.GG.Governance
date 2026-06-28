module FS.GG.Governance.PackEvidence.Tests.ApiCompatCoverageTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.PackEvidence
open FS.GG.Governance.PackEvidence.Model

// 088 US1 (T011, FR-007/SC-001): the per-package coverage projection has ZERO silent passes — every package
// is Checked / NoBaselineYet / NotCovered, deterministically SurfaceId-sorted. Plus US3 (T028, FR-013): an
// inherited (transitive / re-exported upstream) break is reported distinctly from a local break, and the
// cross-package rollup (the worst-of the host overlays) is fail-safe.

let private sid s = SurfaceId s
let private localBreak = { Member = "A.foo"; Kind = MemberRemoved; Origin = ApiBreakOrigin.Local }

let private inheritedBreak =
    { Member = "A.reexported"
      Kind = MemberSignatureChanged
      Origin = ApiBreakOrigin.Inherited(SurfaceId "FS.GG.Contracts") }

[<Tests>]
let tests =
    testList
        "apiCompatCoverage / rollup / attribution"
        [ test "mixed package set ⇒ every package covered-or-reported, SurfaceId-sorted (SC-001)" {
              let packages =
                  [ sid "Zed", ApiBreakSignal.NoBreakingChanges, MinorOrPatchBump
                    sid "Alpha", ApiBreakSignal.NoBaseline, NoBaselineDelta
                    sid "Mid", ApiBreakSignal.BreakingChanges [ localBreak ], MinorOrPatchBump
                    sid "Beta", ApiBreakSignal.NotPackable, NoBaselineDelta
                    sid "Cor", ApiBreakSignal.Indeterminate "feed unreachable", NoBaselineDelta ]

              let coverage = Pack.apiCompatCoverage packages

              Expect.equal coverage.Length 5 "every package present — zero silent passes"

              Expect.equal
                  (coverage |> List.map (fun c -> let (SurfaceId s) = c.Surface in s))
                  [ "Alpha"; "Beta"; "Cor"; "Mid"; "Zed" ]
                  "deterministically SurfaceId-sorted (FR-007)"

              let outcomeFor s =
                  coverage |> List.find (fun c -> c.Surface = sid s) |> fun c -> c.Outcome

              Expect.equal (outcomeFor "Zed") (Checked Met) "compared, no break ⇒ Checked Met"
              Expect.equal (outcomeFor "Mid") (Checked Unmet) "breaking under minor ⇒ Checked Unmet"
              Expect.equal (outcomeFor "Alpha") NoBaselineYet "never published ⇒ NoBaselineYet (not silently clean)"
              Expect.equal (outcomeFor "Beta") (NotCovered "not a packable target") "not packable ⇒ NotCovered"

              match outcomeFor "Cor" with
              | NotCovered reason -> Expect.stringContains reason "indeterminate" "tool error ⇒ NotCovered, reason carried"
              | other -> failtestf "expected NotCovered, got %A" other
          }

          test "T028/FR-013: an inherited break is attributed distinctly from a local break" {
              let signal = ApiBreakSignal.BreakingChanges [ localBreak; inheritedBreak ]

              let origins =
                  match signal with
                  | ApiBreakSignal.BreakingChanges bs -> bs |> List.map (fun b -> b.Origin)
                  | _ -> []

              Expect.contains origins ApiBreakOrigin.Local "the local break is labelled Local"

              Expect.contains
                  origins
                  (ApiBreakOrigin.Inherited(SurfaceId "FS.GG.Contracts"))
                  "the re-exported break is labelled Inherited with its upstream surface"

              // The verdict treats both as breaks (a break is a break, regardless of origin).
              Expect.equal (Pack.apiCompatibilityFact signal MinorOrPatchBump) (Some Unmet) "inherited+local under minor ⇒ Unmet"
          }

          test "rollup is the fail-safe worst-of (Unrecoverable ≻ Unmet ≻ Met)" {
              // Any indeterminate ⇒ Unrecoverable, even alongside clean/met packages.
              Expect.equal
                  (Pack.apiCompatibilityRollup
                      [ ApiBreakSignal.NoBreakingChanges, MinorOrPatchBump
                        ApiBreakSignal.Indeterminate "x", NoBaselineDelta ])
                  Unrecoverable
                  "indeterminate dominates"

              // Any breaking-under-bump ⇒ Unmet when nothing is indeterminate.
              Expect.equal
                  (Pack.apiCompatibilityRollup
                      [ ApiBreakSignal.NoBreakingChanges, MinorOrPatchBump
                        ApiBreakSignal.BreakingChanges [ localBreak ], MinorOrPatchBump ])
                  Unmet
                  "breaking-under-bump dominates clean"

              // All clean / major-bumped / no-baseline ⇒ Met.
              Expect.equal
                  (Pack.apiCompatibilityRollup
                      [ ApiBreakSignal.NoBreakingChanges, MinorOrPatchBump
                        ApiBreakSignal.BreakingChanges [ localBreak ], MajorBump
                        ApiBreakSignal.NoBaseline, NoBaselineDelta ])
                  Met
                  "covered + major-bumped + never-published ⇒ Met"

              // NotPackable contributes nothing; an all-uncovered/empty set is vacuously Met.
              Expect.equal (Pack.apiCompatibilityRollup []) Met "empty set vacuously Met"

              Expect.equal
                  (Pack.apiCompatibilityRollup [ ApiBreakSignal.NotPackable, NoBaselineDelta ])
                  Met
                  "only-NotPackable contributes no fact ⇒ vacuously Met"
          } ]
