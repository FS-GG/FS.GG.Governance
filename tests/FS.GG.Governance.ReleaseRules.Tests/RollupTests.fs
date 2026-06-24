module FS.GG.Governance.ReleaseRules.Tests.RollupTests

open Expecto
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.ReleaseRules
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.ReleaseRules.Tests.Support

// US2 (acc. 1–3, FR-003/FR-004/FR-010, SC-002/SC-006): `rollup` derives effective severity through F023
// `deriveEffectiveSeverity` VERBATIM under `RunMode.Release`/`Profile.Release`, partitions by re-applying
// the F024 rule, and computes the F024 `Verdict`/`ExitCodeBasis` — a blocking violation fails, an
// advisory/relaxed violation warns or passes but stays visible.

/// The EnforcementInput a finding feeds into F023, under the fixed Release mode/profile (research D2/D6).
let private inputOf (f: ReleaseFinding) : EnforcementInput =
    { BaseSeverity = f.BaseSeverity
      Maturity = f.Maturity
      Mode = RunMode.Release
      Profile = Profile.Release }

[<Tests>]
let tests =
    testList
        "RollupTests"
        [ test "a blocking violation ⇒ Fail / Blocked, the violation in Blockers" {
              let rule = blocking VersionBump "pkg"
              let d = Release.evaluateRelease [ rule ] (factsOf [ VersionBump, Unmet ])
              Expect.equal d.Verdict Fail "a blocking violation ⇒ Fail"
              Expect.equal d.ExitCodeBasis Blocked "a Fail verdict ⇒ Blocked exit basis"
              Expect.equal d.Blockers.Length 1 "the violation lands in Blockers"
              Expect.isTrue
                  (d.Blockers |> List.forall (fun b -> b.Finding.Kind = VersionBump))
                  "the blocker is the violated rule"
          }

          test "an all-Satisfied set ⇒ Pass / Clean, no Blockers, every finding in Passing" {
              let rules = allKinds |> List.map (fun k -> blocking k "pkg")
              let d = Release.evaluateRelease rules (allMet rules)
              Expect.equal d.Verdict Pass "all satisfied ⇒ Pass"
              Expect.equal d.ExitCodeBasis Clean "a Pass verdict ⇒ Clean exit basis"
              Expect.isEmpty d.Blockers "no blockers"
              Expect.equal d.Passing.Length rules.Length "every satisfied finding lands in Passing"
          }

          test "a maturity-relaxed blocking violation ⇒ Pass but VISIBLE as a Warning (FR-010)" {
              let rule = relaxed Provenance "pkg" // base Blocking, maturity Warn
              let d = Release.evaluateRelease [ rule ] (factsOf [ Provenance, Unmet ])
              Expect.equal d.Verdict Pass "a relaxed violation does not block ⇒ Pass"
              Expect.isEmpty d.Blockers "no blockers"
              Expect.isTrue
                  (d.Warnings |> List.exists (fun w -> w.Finding.Kind = Provenance))
                  "the relaxed violation is visible as a Warning, never dropped"
          }

          test "a base-Advisory violation lands in Passing (F024 never escalates) and stays visible" {
              let rule = advisory PublishPlan "pkg"
              let d = Release.evaluateRelease [ rule ] (factsOf [ PublishPlan, Unmet ])
              Expect.equal d.Verdict Pass "a base-advisory violation does not block"
              Expect.isEmpty d.Blockers "no blockers"
              Expect.isTrue
                  (d.Passing |> List.exists (fun p -> p.Finding.Kind = PublishPlan && p.Finding.Outcome = Violated))
                  "the base-advisory violation is visible in Passing"
          }

          test "effective severity equals deriveEffectiveSeverity called directly (verbatim reuse)" {
              let rule = blocking TemplatePins "pkg"
              let d = Release.evaluateRelease [ rule ] (factsOf [ TemplatePins, Unmet ])
              let b = List.exactlyOne d.Blockers
              Expect.equal
                  b.Decision
                  (deriveEffectiveSeverity (inputOf b.Finding))
                  "the carried decision is F023's, not a re-derivation"
          }

          test "relaxing only the maturity changes the bucket + blocker count but never the Outcome truth (SC-006)" {
              let strict = blocking TrustedPublishing "pkg"
              let loose = relaxed TrustedPublishing "pkg"
              let facts = factsOf [ TrustedPublishing, Unmet ]
              let dStrict = Release.evaluateRelease [ strict ] facts
              let dLoose = Release.evaluateRelease [ loose ] facts

              // Same violated TRUTH on both sides.
              let strictFinding = List.exactlyOne (Release.evaluate [ strict ] facts)
              let looseFinding = List.exactlyOne (Release.evaluate [ loose ] facts)
              Expect.equal strictFinding.Outcome Violated "strict: violated"
              Expect.equal looseFinding.Outcome Violated "relaxed: still violated (truth unchanged)"

              // Different bucket + verdict.
              Expect.equal dStrict.Verdict Fail "strict blocks"
              Expect.equal dLoose.Verdict Pass "relaxed does not block"
              Expect.equal dStrict.Blockers.Length 1 "strict: one blocker"
              Expect.isEmpty dLoose.Blockers "relaxed: no blockers"
              Expect.isTrue
                  (dLoose.Warnings |> List.exists (fun w -> w.Finding.Kind = TrustedPublishing))
                  "relaxed: violation still visible as a Warning"
          } ]
