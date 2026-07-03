module FS.GG.Governance.Kernel.Tests.FreshnessTests

open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.Kernel

// ── Evidence freshness (F06 · US3) — V37–V38 ──
//
// EVIDENCE-OBLIGATIONS NOTE (Principle IV / V): F06 is a PURE DERIVATION — Principle IV
// (Elmish/MVU) is N/A (discovering real artifact modification times and recording the
// result are the F08 edge's job; decide reads no clock/filesystem). All evidence here is
// REAL: plain comparable instants supplied as values, with an FsCheck purity property.
// No synthetic fixtures, no mocks/stubs, hence no `// SYNTHETIC:` disclosures.

let private propConfig =
    { FsCheckConfig.defaultConfig with
        maxTest = 300
        replay = Some(1234UL, 5678UL, None) } // fixed seed → reproducible

[<Tests>]
let tests =
    testList
        "Freshness"
        [ test "V37 inclusive boundary, empty-covered, and multi-artifact freshness" {
              Expect.equal (Freshness.decide 10 [ 9 ]) Fresh "recorded after the only change ⇒ Fresh"
              Expect.equal (Freshness.decide 10 [ 11 ]) Stale "covered artifact changed after recording ⇒ Stale"
              Expect.equal (Freshness.decide 10 [ 10 ]) Fresh "tie at the same instant ⇒ Fresh (inclusive boundary, FR-009)"
              Expect.equal (Freshness.decide 10 []) Fresh "covers no artifacts ⇒ Fresh (FR-009)"
              Expect.equal (Freshness.decide 10 [ 3; 10; 7 ]) Fresh "recorded ≥ latest covered instant ⇒ Fresh"
              Expect.equal (Freshness.decide 10 [ 3; 11; 7 ]) Stale "any covered instant later than recorded ⇒ Stale"
              Expect.isTrue (Freshness.isFresh 10 [ 10 ]) "isFresh agrees with decide = Fresh"
              Expect.isFalse (Freshness.isFresh 10 [ 11 ]) "isFresh is false when Stale"
          }

          testPropertyWithConfig propConfig "V38 decide is a pure function of the instants; isFresh agrees"
          <| fun (recorded: int) (covered: int list) ->
              let expected =
                  if List.isEmpty covered then Fresh
                  elif recorded >= List.max covered then Fresh
                  else Stale

              Freshness.decide recorded covered = expected // correct & inclusive (SC-007)
              && Freshness.decide recorded covered = Freshness.decide recorded covered // pure / deterministic (SC-008)
              && Freshness.isFresh recorded covered = (Freshness.decide recorded covered = Fresh) ]
