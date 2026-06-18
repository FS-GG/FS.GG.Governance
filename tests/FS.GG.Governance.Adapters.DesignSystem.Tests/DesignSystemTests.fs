module FS.GG.Governance.Adapters.DesignSystem.Tests.DesignSystemTests

open Expecto
open FS.GG.Governance.Kernel
open FS.GG.Governance.Adapters.Spi
open FS.GG.Governance.Adapters.DesignSystem
open FS.GG.Governance.Adapters.DesignSystem.Tests.FixtureFacts
open FS.GG.Governance.Adapters.DesignSystem.Tests.ProjectFact

// US1 (the five-component / observer-only / no-F10-shape adoption, SC-001) and US3 (the
// fixture token tree; three-valued probes, SC-003). Everything is evaluated through the
// BUILT FS.GG.Governance.Adapters.DesignSystem + Spi + Kernel libraries (Principle I).
//
// STRUCTURAL CLAIM (verified by inspection, not a runtime assertion — T018, FR-003/004/005):
// the adapter's definitions call only FS.GG.Governance.Kernel + Spi APIs. There is no
// System.IO, no rendering type, no write-token / write-capture / write-spec function (it is an
// OBSERVER, not an author), and — the keystone — NO Phase type, NO whenPhase guard, NO merge
// fence, NO dial, and NO reference to F10. Those absences are what prove this domain did not
// copy domain #1; the machine-checkable half is the dependency-hygiene test (SurfaceDriftTests
// T040: the shipped assembly references neither FS.GG.Governance.Adapters.SpecKit nor any
// rendering library).

let private isPass =
    function
    | Pass -> true
    | _ -> false

let private isFail =
    function
    | Fail _ -> true
    | _ -> false

let private isUncertain =
    function
    | Uncertain _ -> true
    | _ -> false

let private allArtifacts =
    [ TokenDocument
      GeneratedTokenSurface
      RenderedCapture
      InteractionStateSpec
      PagePatternSpec ]

[<Tests>]
let tests =
    testList
        "DesignSystem"
        [ // ── US1 (SC-001) ──
          test "V1 the adapter is fully specified by the five SPI components + the Bridge — 15 rules, 1 fence (token-surface only, NO merge fence)" {
              let adapter = designAdapter

              // The Adapter record TYPE forces all five components + the bridge to be present
              // (a missing component does not compile, F09). Here we pin the SHAPE numbers.
              Expect.equal adapter.Rules.Length 15 "the catalog is the fifteen reified rules"
              Expect.equal adapter.Fences.Length 1 "exactly one fence — the token surface; there is NO merge fence (no lifecycle)"
              Expect.equal (adapter.Fences |> List.map (fun f -> f.Name)) [ "token-surface" ] "the single fence is the token-surface fence"
              Expect.isNonEmpty adapter.Probes "the declared probe vocabulary is carried for the contract/testing"
          }

          test "V1 the adapter governs fixture-drawn facts end-to-end through the KERNEL entry points only" {
              let adapter = designAdapter

              // Adapter.toRules → FixedPoint.evaluate derives governance facts via the kernel.
              let rules = Adapter.toRules adapter
              let result = FixedPoint.evaluate adapter.Identify rules conformingFacts

              Expect.isGreaterThan
                  result.Facts.Length
                  conformingFacts.Length
                  "the adapter derives governance facts through the kernel's fixed point (no adapter evaluation code)"

              // Route.route partitions the catalog for a fenced change — kernel routing, reused.
              let surfaceChange = { Surfaces = Set.ofList [ GeneratedTokenSurface ] }
              let route = Route.route adapter.Fences adapter.Rules Gate surfaceChange

              match route.Stakes with
              | Fenced name -> Expect.stringContains name "token-surface" "the token-surface fence tripped"
              | Routine -> failtest "a change touching the generated token surface must be Fenced"
          }

          test "V1 toRef is injective over all five DesignArtifactRef cases (a faithful-lift precondition, FR-002)" {
              let refs = allArtifacts |> List.map DesignSystem.toRef
              Expect.equal (List.distinct refs).Length allArtifacts.Length "distinct artifact kinds map to distinct ArtifactRefs"
          }

          test "V1 identify is injective on value-bearing facts, and keys entity facts so a later fact supersedes (law L0)" {
              // value-bearing facts are distinguished by full value …
              Expect.notEqual (DesignSystem.identify (DesignRule "a")) (DesignSystem.identify (DesignRule "b")) "DesignRule keyed by value"
              Expect.notEqual
                  (DesignSystem.identify (VerdictRestsOn("v", "m1")))
                  (DesignSystem.identify (VerdictRestsOn("v", "m2")))
                  "VerdictRestsOn keyed by value"
              Expect.notEqual
                  (DesignSystem.identify (ArtifactPresent TokenDocument))
                  (DesignSystem.identify (ArtifactPresent GeneratedTokenSurface))
                  "ArtifactPresent keyed by value"

              // … entity-keyed facts collapse to one id so a later fact supersedes (dedup) …
              Expect.equal
                  (DesignSystem.identify (PolicySelected "AntDesign"))
                  (DesignSystem.identify (PolicySelected "Material"))
                  "PolicySelected keyed by a fixed entity — one selected policy"
              Expect.equal
                  (DesignSystem.identify (MeasurementState("m", Real)))
                  (DesignSystem.identify (MeasurementState("m", Synthetic)))
                  "MeasurementState keyed by id"
              Expect.equal
                  (DesignSystem.identify (SurfaceObservation("contrast-meets", GeneratedTokenSurface, true)))
                  (DesignSystem.identify (SurfaceObservation("contrast-meets", GeneratedTokenSurface, false)))
                  "SurfaceObservation keyed by (probe, subject)"
          }

          // ── US3 (SC-003) ──
          test "V3 the full catalog evaluates AND explains over the fixture token tree (no rendering library on the path)" {
              for r in Catalog.catalog do
                  let (RuleId id) = r.Id
                  let verdict = Check.eval conformingFacts r.Check
                  let explanation = Check.explain conformingFacts r.Check

                  Expect.equal
                      (Explanation.verdict explanation)
                      verdict
                      (sprintf "%s: explain top verdict equals eval" id)

              // The conforming tree satisfies every DETERMINISTIC rule (the green fixtures).
              let deterministic =
                  [ Catalog.tokenDrift
                    Catalog.contrastPolicy
                    Catalog.tokenSurfaceGate
                    Catalog.evidenceMeasured
                    Catalog.spacingScale
                    Catalog.controlHeightDefaults
                    Catalog.intentCoverage
                    Catalog.visualStateResolution ]

              for r in deterministic do
                  let (RuleId id) = r.Id
                  Expect.isTrue (isPass (Check.eval conformingFacts r.Check)) (sprintf "%s passes over the conforming fixtures" id)
          }

          test "V3 the probes are three-valued over fixtures — Met / Unmet / Unknown, never a silent Met for a missing observation" {
              let surfaceMatches = DesignSystem.surfaceMatches GeneratedTokenSurface TokenDocument
              let contrast = DesignSystem.contrastMeets "ant-aa" GeneratedTokenSurface
              let spacing = DesignSystem.surfaceObserved "spacing-scale" GeneratedTokenSurface

              // Met (a present `true` observation)
              Expect.isTrue
                  (isPass (Check.eval [ fact (SurfaceObservation("surface-matches", GeneratedTokenSurface, true)) ] surfaceMatches))
                  "surface-matches: a satisfied observation ⇒ Pass"

              // Unmet (a present `false` observation — a definite failure, the drift edge case)
              Expect.isTrue
                  (isFail (Check.eval [ fact (SurfaceObservation("surface-matches", GeneratedTokenSurface, false)) ] surfaceMatches))
                  "surface-matches: a violated observation ⇒ Fail (drift)"

              // Unknown (NO observation — a missing fixture is undecided, never a silent Met)
              Expect.isTrue (isUncertain (Check.eval [] surfaceMatches)) "surface-matches: a missing observation ⇒ Uncertain"
              Expect.isTrue (isUncertain (Check.eval [] contrast)) "contrast: a missing contrast fixture ⇒ Uncertain (never a silent Pass)"
              Expect.isTrue (isUncertain (Check.eval [] spacing)) "spacing-scale: a missing observation ⇒ Uncertain"

              // The contrast probe carries `policy` so distinct policies render/hash differently.
              Expect.notEqual
                  (Check.hash (DesignSystem.contrastMeets "ant-aa" GeneratedTokenSurface))
                  (Check.hash (DesignSystem.contrastMeets "wcag-aaa" GeneratedTokenSurface))
                  "contrastMeets distinguishes policies in its hash (Pr4)"
          } ]
