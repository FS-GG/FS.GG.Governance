module FS.GG.Governance.Adapters.SpecKit.Tests.LiftTests

open Expecto
open FS.GG.Governance.Kernel
open FS.GG.Governance.Adapters.Spi
open FS.GG.Governance.Adapters.SpecKit
open FS.GG.Governance.Adapters.SpecKit.Tests.ExampleAdapters

// SC-007 (the faithful lift — the M3 milestone proof). The Spec Kit adapter (the REAL
// adopter) is composed with a SECOND, SYNTHETIC example domain (the "memo" domain in
// ExampleAdapters.fs) at a test root. For 100% of the catalog the lifted rule's
// (verdict, provenance) over coproduct-wrapped facts is byte-for-byte identical to the
// standalone original over the projected facts, and render/hash are invariant under the
// lift. Every test name carries the `Synthetic` token because the proof asserts via the
// synthetic composition partner (Principle V).

/// A representative SpecKitFact set that makes every catalog rule fire (PhaseReached Merge
/// keeps the phase guards transparent; the task/area/artifact facts drive the deterministic
/// checks). Evaluated standalone and — coproduct-wrapped — through the lift.
let private skFacts: FactSet<SpecKitFact> =
    [ fact (PhaseReached Phase.Merge)
      fact (ConstitutionArea("scope", false))
      fact (ArtifactPresent SpecKitArtifact.Contracts)
      fact (TaskState("T1", Synthetic))
      fact (TaskState("T2", Real))
      fact (TaskDependsOn("T2", "T1"))
      fact (SkillBound("T1", "skill-x")) ]

/// The SAME facts wrapped into the coproduct, keeping each assertion's Id and Provenance
/// and re-mapping only its Value (`Sk`).
let private projFacts: FactSet<ProjectFact> =
    skFacts
    |> List.map (fun a ->
        { Id = a.Id
          Value = injectSk a.Value
          Provenance = a.Provenance })

[<Tests>]
let tests =
    testList
        "Lift"
        [ test "V7 Synthetic — render & hash are invariant under the lift for 100% of the catalog (the cache key does not move)" {
              for r in Catalog.catalog do
                  let (RuleId id) = r.Id
                  let lifted = Lift.check (|SkP|_|) r.Check
                  Expect.equal (Check.render lifted) (Check.render r.Check) (sprintf "%s: render invariant" id)
                  Expect.equal (Check.hash lifted) (Check.hash r.Check) (sprintf "%s: hash invariant" id)
                  Expect.equal (Check.isReified lifted) (Check.isReified r.Check) (sprintf "%s: reified-ness invariant" id)
          }

          test "V7 Synthetic — verdict & explanation (provenance proof tree) are identical standalone vs lifted, for 100% of the catalog" {
              for r in Catalog.catalog do
                  let (RuleId id) = r.Id
                  let lifted = Lift.check (|SkP|_|) r.Check

                  Expect.equal
                      (Check.eval projFacts lifted)
                      (Check.eval skFacts r.Check)
                      (sprintf "%s: verdict identical" id)

                  Expect.equal
                      (Check.explain projFacts lifted)
                      (Check.explain skFacts r.Check)
                      (sprintf "%s: explanation (proof tree) byte-identical" id)
          }

          test "V7 Synthetic — the executable lifted rule preserves Id and Provenance verbatim (law L3) for 100% of the catalog" {
              for r in Catalog.catalog do
                  let (RuleId id) = r.Id
                  let standaloneRule = CheckRule.toRule specKitAdapter.Bridge r
                  let liftedRule = Lift.rule injectSk (|SkP|_|) standaloneRule

                  let stdOut = standaloneRule.Apply skFacts
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
                      (stdOut |> List.map (fun a -> injectSk a.Value))
                      (sprintf "%s: lifted value = inject (standalone value)" id)
          }

          test "V7 Synthetic — the adapter composes with the second domain and drops out cleanly (independence, law L3)" {
              let liftedSk = Composition.lift (|SkP|_|) narrowSk specKitAdapter
              let liftedMemo = Composition.lift (|MemoP|_|) narrowMemo memoAdapter

              let composed = Composition.compose [ liftedSk; liftedMemo ] []

              Expect.equal
                  composed.Catalog.Length
                  (Catalog.catalog.Length + memoAdapter.Rules.Length)
                  "the composed catalog is both domains' lifted rules"

              // Dropping the Spec Kit adapter removes it cleanly — the memo domain is intact.
              let withoutSk = Composition.compose [ liftedMemo ] []
              Expect.equal withoutSk.Catalog.Length memoAdapter.Rules.Length "dropping SpecKit leaves only the memo domain"

              // The composed project still evaluates through the kernel with no adapter code.
              let supplied: FactSet<ProjectFact> =
                  [ { Id = projIdentify (Sk(PhaseReached Phase.Merge)); Value = Sk(PhaseReached Phase.Merge); Provenance = [] }
                    { Id = projIdentify (Memo(MemoApproved true)); Value = Memo(MemoApproved true); Provenance = [] } ]

              let result =
                  FixedPoint.evaluate projIdentify (Composition.toRules projBridge composed) supplied

              Expect.isGreaterThan result.Facts.Length supplied.Length "the composed project derives governance facts through the kernel"
          } ]
