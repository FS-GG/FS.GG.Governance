module FS.GG.Governance.Adapters.DesignSystem.Tests.LiftTests

open Expecto
open FS.GG.Governance.Kernel
open FS.GG.Governance.Adapters.Spi
open FS.GG.Governance.Adapters.DesignSystem
open FS.GG.Governance.Adapters.DesignSystem.Tests.FixtureFacts
open FS.GG.Governance.Adapters.DesignSystem.Tests.ProjectFact

// US5 (the faithful lift — the M3 adoption-bar proof, SC-006/SC-007). The design-system
// adapter (the REAL adopter, domain #2) is composed alongside the REAL F10 Spec Kit adapter
// (domain #1) at a test root. For 100% of the catalog the lifted rule's (verdict, provenance)
// over coproduct-wrapped facts is byte-for-byte identical to the standalone original, and
// render/hash/reads/isReified are invariant under the lift. There is NO synthetic domain —
// both composed adapters are real (Principle V), so no `Synthetic` marker is owed.

/// A representative DesignSystemFact set drawn from the conforming fixture token tree.
let private designFacts: FactSet<DesignSystemFact> = conformingFacts

/// The SAME facts wrapped into the coproduct, keeping each assertion's Id and Provenance and
/// re-mapping only its Value (`Design`).
let private projFacts: FactSet<ProjectFact> =
    designFacts
    |> List.map (fun a ->
        { Id = a.Id
          Value = injectDesign a.Value
          Provenance = a.Provenance })

[<Tests>]
let tests =
    testList
        "Lift"
        [ // ── SC-006 ──
          test "V6 render / hash / reads / isReified are invariant under the lift for 100% of the catalog (the cache key does not move)" {
              for r in Catalog.catalog do
                  let (RuleId id) = r.Id
                  let lifted = Lift.check (|DesignP|_|) r.Check
                  Expect.equal (Check.render lifted) (Check.render r.Check) (sprintf "%s: render invariant" id)
                  Expect.equal (Check.hash lifted) (Check.hash r.Check) (sprintf "%s: hash invariant" id)
                  Expect.equal (Check.reads lifted) (Check.reads r.Check) (sprintf "%s: reads invariant" id)
                  Expect.equal (Check.isReified lifted) (Check.isReified r.Check) (sprintf "%s: reified-ness invariant (a lifted Opaque stays opaque)" id)
          }

          test "V6 verdict & explanation (provenance proof tree) are identical standalone vs lifted, for 100% of the catalog" {
              for r in Catalog.catalog do
                  let (RuleId id) = r.Id
                  let lifted = Lift.check (|DesignP|_|) r.Check

                  Expect.equal
                      (Check.eval projFacts lifted)
                      (Check.eval designFacts r.Check)
                      (sprintf "%s: verdict identical" id)

                  Expect.equal
                      (Check.explain projFacts lifted)
                      (Check.explain designFacts r.Check)
                      (sprintf "%s: explanation (proof tree) byte-identical" id)
          }

          test "V6 the executable lifted rule preserves (verdict, provenance) — Id, Provenance, and value verbatim (law L2/L3) for 100% of the catalog" {
              for r in Catalog.catalog do
                  let (RuleId id) = r.Id
                  let standaloneRule = CheckRule.toRule designAdapter.Bridge r
                  let liftedRule = Lift.rule injectDesign (|DesignP|_|) standaloneRule

                  let stdOut = standaloneRule.Apply designFacts
                  let liftOut = liftedRule.Apply projFacts

                  Expect.equal
                      (liftOut |> List.map (fun a -> a.Provenance))
                      (stdOut |> List.map (fun a -> a.Provenance))
                      (sprintf "%s: provenance identical" id)

                  Expect.equal
                      (liftOut |> List.map (fun a -> a.Id))
                      (stdOut |> List.map (fun a -> a.Id))
                      (sprintf "%s: fact ids identical" id)

                  // The lifted value is exactly the standalone value re-targeted by `inject`.
                  Expect.equal
                      (liftOut |> List.map (fun a -> a.Value))
                      (stdOut |> List.map (fun a -> injectDesign a.Value))
                      (sprintf "%s: lifted value = inject (standalone value)" id)
          }

          // ── SC-007 — the adoption bar: two unrelated REAL domains coexist ──
          test "V7 the design-system adapter composes alongside the REAL F10 Spec Kit adapter and drops out cleanly (independence)" {
              let liftedDesign = Composition.lift (|DesignP|_|) narrowDesign designAdapter
              let liftedSpecKit = Composition.lift (|SpecKitP|_|) narrowSpecKit specKitAdapter

              let composed = Composition.compose [ liftedDesign; liftedSpecKit ] []

              Expect.equal
                  composed.Catalog.Length
                  (designAdapter.Rules.Length + specKitAdapter.Rules.Length)
                  "the composed catalog is both domains' lifted rules — two unrelated real domains at one root"

              // The composed fence set unions the design token-surface fence with F10's merge fence.
              Expect.isTrue
                  (composed.Fences |> List.exists (fun f -> f.Name = "token-surface"))
                  "the design token-surface fence is present in the composed root"
              Expect.isTrue
                  (composed.Fences |> List.exists (fun f -> f.Name = "feature-merge"))
                  "the F10 merge fence is present in the composed root (each domain keeps its own shape)"

              // Dropping the design-system adapter removes it cleanly — the Spec Kit domain is intact.
              let withoutDesign = Composition.compose [ liftedSpecKit ] []
              Expect.equal withoutDesign.Catalog.Length specKitAdapter.Rules.Length "dropping the design domain leaves only the Spec Kit domain"

              // The composed project still evaluates through the kernel with no adapter code.
              let supplied: FactSet<ProjectFact> =
                  [ { Id = projIdentify (injectDesign (PolicySelected "AntDesign"))
                      Value = injectDesign (PolicySelected "AntDesign")
                      Provenance = [] }
                    { Id = projIdentify (injectSpecKit sampleSpecKitFact)
                      Value = injectSpecKit sampleSpecKitFact
                      Provenance = [] } ]

              let result =
                  FixedPoint.evaluate projIdentify (Composition.toRules projBridge composed) supplied

              Expect.isGreaterThan
                  result.Facts.Length
                  supplied.Length
                  "the composed project derives governance facts for BOTH domains through the kernel"
          } ]
