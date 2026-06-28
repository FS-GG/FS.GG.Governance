module FS.GG.Governance.ReleaseRules.Tests.ApiCompatibilityRuleTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.ReleaseRules
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseRules.Tests.Support

// 088 US1 (T008): the ADDITIVE ApiCompatibility rule flows through the EXISTING evaluate→rollup verbatim — a
// declared rule yields exactly one finding whose RuleOutcome follows Met→Satisfied, Unmet/Unrecoverable→
// Violated (fail-safe), and partitions under its declared Maturity (advisory → Warnings; required → Blockers
// in US2). The release-core finding Reason names the kind token + surface + basis; the member-level
// remediation text ("requires MAJOR bump or revert" / "API comparison indeterminate: <reason>", FR-003) is
// carried by the per-package COVERAGE projection (PackEvidence.coverageOutcome) + the detector `.fsx` output,
// because the pure release core sees only the FactState, never the break list.

let private sid = SurfaceId "FS.GG.Governance.Sample"

[<Tests>]
let tests =
    testList
        "ApiCompatibility rule (evaluate/rollup, additive)"
        [ test "Met ⇒ exactly one Satisfied finding ⇒ Passing" {
              let rules = [ relaxed ApiCompatibility "FS.GG.Governance.Sample" ]
              let decision = Release.evaluateRelease rules (factsOf [ ApiCompatibility, Met ])

              Expect.equal decision.Passing.Length 1 "one passing finding"
              Expect.isEmpty decision.Blockers "no blockers when Met"
              Expect.isEmpty decision.Warnings "no warnings when Met"
              Expect.equal decision.Verdict Pass "Met ⇒ Pass"

              let f = decision.Passing.Head.Finding
              Expect.equal f.Kind ApiCompatibility "the finding is the ApiCompatibility rule"
              Expect.equal f.Outcome Satisfied "Met ⇒ Satisfied"
              Expect.stringContains f.Reason "apiCompatibility" "reason names the kind token (FR-003)"
              Expect.stringContains f.Reason "FS.GG.Governance.Sample" "reason names the governed surface"
          }

          test "Unmet under ADVISORY maturity ⇒ one Violated finding in Warnings (visible, non-blocking)" {
              // advisory = base Blocking relaxed to effective Advisory → the Warnings bucket (US1).
              let rules = [ relaxed ApiCompatibility "FS.GG.Governance.Sample" ]
              let decision = Release.evaluateRelease rules (factsOf [ ApiCompatibility, Unmet ])

              Expect.equal decision.Warnings.Length 1 "the breaking-under-bump violation is a visible Warning"
              Expect.isEmpty decision.Blockers "advisory ⇒ never a blocker (Verdict unaffected)"
              Expect.equal decision.Verdict Pass "advisory violation does not fail the verdict (US1)"

              let f = decision.Warnings.Head.Finding
              Expect.equal f.Outcome Violated "Unmet ⇒ Violated"
              Expect.stringContains f.Reason "is not met" "reason states the expectation was not met"
          }

          test "Unrecoverable (indeterminate / un-overlaid) ⇒ Violated, fail-safe (FR-008)" {
              let rules = [ relaxed ApiCompatibility "FS.GG.Governance.Sample" ]
              let decision = Release.evaluateRelease rules (factsOf [ ApiCompatibility, Unrecoverable ])

              Expect.equal decision.Warnings.Length 1 "advisory ⇒ Warning"

              let f = decision.Warnings.Head.Finding
              Expect.equal f.Outcome Violated "Unrecoverable ⇒ Violated (never silently satisfied)"
              Expect.stringContains f.Reason "no recoverable evidence" "reason states no recoverable evidence"
          }

          test "an ABSENT ApiCompatibility fact ⇒ Unrecoverable ⇒ Violated (foundational fail-safe by construction)" {
              // No fact supplied for the kind ⇒ factFor returns Unrecoverable ⇒ Violated.
              let rules = [ relaxed ApiCompatibility "FS.GG.Governance.Sample" ]
              let decision = Release.evaluateRelease rules (factsOf [])

              Expect.equal decision.Warnings.Length 1 "an un-overlaid declared rule is a visible advisory violation"
              Expect.equal decision.Warnings.Head.Finding.Outcome Violated "absent fact ⇒ Violated"
          }

          // 088 US2 (T021): once promoted to BlockOnRelease, the SAME fact lands in Blockers with Verdict=Fail.
          test "US2: Unmet under BlockOnRelease ⇒ Blockers + Verdict Fail (the required phase)" {
              let rules = [ blocking ApiCompatibility "FS.GG.Governance.Sample" ] // base Blocking + BlockOnRelease
              let decision = Release.evaluateRelease rules (factsOf [ ApiCompatibility, Unmet ])

              Expect.equal decision.Blockers.Length 1 "breaking-under-bump BLOCKS at BlockOnRelease"
              Expect.isEmpty decision.Warnings "required ⇒ not a mere warning"
              Expect.equal decision.Verdict Fail "Verdict = Fail"
              Expect.equal decision.ExitCodeBasis ExitCodeBasis.Blocked "ExitCodeBasis = Blocked"
          }

          test "US2: Unrecoverable under BlockOnRelease ⇒ Blockers (fail-safe required)" {
              let rules = [ blocking ApiCompatibility "FS.GG.Governance.Sample" ]
              let decision = Release.evaluateRelease rules (factsOf [ ApiCompatibility, Unrecoverable ])
              Expect.equal decision.Blockers.Length 1 "indeterminate BLOCKS at BlockOnRelease (FR-008)"
              Expect.equal decision.Verdict Fail "Verdict = Fail"
          }

          test "US2: Met under BlockOnRelease ⇒ Passing, Verdict Pass (a major-bumped break / no break)" {
              let rules = [ blocking ApiCompatibility "FS.GG.Governance.Sample" ]
              let decision = Release.evaluateRelease rules (factsOf [ ApiCompatibility, Met ])
              Expect.equal decision.Passing.Length 1 "Met ⇒ Passing even when required"
              Expect.equal decision.Verdict Pass "no-break / major-bump ⇒ Pass"
          }

          test "the additive case never disturbs the other rules' findings (additivity)" {
              let rules =
                  [ blocking VersionBump "pkg"
                    relaxed ApiCompatibility "pkg" ]

              let decision =
                  Release.evaluateRelease rules (factsOf [ VersionBump, Met; ApiCompatibility, Unmet ])

              Expect.equal (decision.Passing.Length + decision.Warnings.Length + decision.Blockers.Length) 2 "one finding per rule"
              Expect.equal decision.Passing.Length 1 "VersionBump Met ⇒ Passing"
              Expect.equal decision.Warnings.Length 1 "ApiCompatibility Unmet (advisory) ⇒ Warnings"
              ignore sid
          } ]
