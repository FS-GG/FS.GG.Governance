module FS.GG.Governance.Adapters.SpecKit.Tests.SpecKitTests

open Expecto
open FS.GG.Governance.Kernel
open FS.GG.Governance.Adapters.Spi
open FS.GG.Governance.Adapters.SpecKit
open FS.GG.Governance.Adapters.SpecKit.Tests.ExampleAdapters

// SC-001 (five-component / observer-only / 100% kernel reuse), SC-006 (render & explain),
// and SC-002 (the phase guard). The adapter is exercised through its BUILT public surface
// and the kernel's entry points only (Principle I). Structural claims — "no inference/
// arbitration/evidence/render/hash/severity/routing code and no artifact-authoring
// operation in the adapter module" — are verified by INSPECTION of SpecKit.fs/Catalog.fs:
// every binding calls only FS.GG.Governance.Kernel + Spi APIs (Evidence.build/effective,
// CheckRule.rule/blocking/asking, Check.*, Route via the kernel); there is NO System.IO, NO
// Model/Msg/Effect, and NO write-spec/write-plan/write-tasks function anywhere (FR-003/
// FR-004). The dependency-hygiene half of that claim is enforced reflectively in
// SurfaceDriftTests (SpecKit references only BCL/FSharp.Core/Spi/Kernel).

let private allArtifacts: SpecKitArtifact list =
    [ SpecKitArtifact.Constitution
      SpecKitArtifact.Spec
      SpecKitArtifact.Plan
      SpecKitArtifact.Research
      SpecKitArtifact.DataModel
      SpecKitArtifact.Contracts
      SpecKitArtifact.Quickstart
      SpecKitArtifact.Tasks
      SpecKitArtifact.TaskDeps ]

/// A reified leaf that DEFINITELY fails if it contributes — so a vacuous `Pass` proves the
/// guard suppressed it (rather than the check happening to pass).
let private alwaysFail: Check<SpecKitFact> =
    Check.probe "always-fail" [] [] (fun _ -> Unmet "boom")

let private opaqueCheck: Check<SpecKitFact> =
    Opaque("subjective", fun _ -> Unknown "a judge must rule")

let private phasesBelow (required: Phase) =
    allPhases |> List.filter (fun p -> not (Phase.reached p required))

let private phasesAtOrAbove (required: Phase) =
    allPhases |> List.filter (fun p -> Phase.reached p required)

[<Tests>]
let tests =
    testList
        "SpecKit"
        [ test "V1 the adapter is fully specified by the five SPI components + the Bridge, and governs synthetic facts through the kernel only" {
              let adapter = specKitAdapter

              // The five components + the Bridge are all present (a record is total — an
              // adapter that omits one does not compile, F09 FR-014). Govern synthetic
              // facts end-to-end through kernel entry points ONLY.
              let rules = Adapter.toRules adapter

              Expect.equal rules.Length adapter.Rules.Length "toRules translates exactly the catalog (no extra/missing rules)"

              let supplied =
                  [ fact (PhaseReached Phase.Merge)
                    fact (ArtifactPresent SpecKitArtifact.Contracts)
                    fact (ArtifactPresent SpecKitArtifact.TaskDeps) ]

              let result = FixedPoint.evaluate adapter.Identify rules supplied

              // Every rule asserts a governance outcome embedded via the Bridge → SpecKitGov.
              let governanceFacts =
                  result.Facts
                  |> List.choose (fun f ->
                      match f.Value with
                      | SpecKitGov o -> Some o
                      | _ -> None)

              Expect.isNonEmpty governanceFacts "the kernel derived governance facts via the adapter's Bridge"

              // Routing is the kernel's — the adapter only supplies fences/rules.
              let change = { Phase = Phase.Tasks; Surfaces = Set.ofList [ SpecKitArtifact.Tasks ] }
              let route = Route.route adapter.Fences adapter.Rules Inner change
              Expect.isEmpty route.Blocking "the inner loop is advisory (kernel routing, not adapter code)"
          }

          test "V1 toRef is injective over all nine artifact kinds (distinct kinds → distinct ArtifactRef)" {
              let refs = allArtifacts |> List.map SpecKit.toRef
              Expect.equal (refs |> List.distinct |> List.length) allArtifacts.Length "every artifact maps to a distinct ref"
          }

          test "V1 identify keys TaskState/ConstitutionArea by entity (a later fact supersedes) and is injective on value-bearing facts" {
              // Entity-keyed: same entity, different payload → SAME id (dedup / supersede).
              Expect.equal
                  (SpecKit.identify (TaskState("T1", Real)))
                  (SpecKit.identify (TaskState("T1", Synthetic)))
                  "TaskState keyed by task id"

              Expect.equal
                  (SpecKit.identify (ConstitutionArea("scope", true)))
                  (SpecKit.identify (ConstitutionArea("scope", false)))
                  "ConstitutionArea keyed by area"

              // Distinct entities / distinct values → distinct ids (injective).
              let valueBearing =
                  [ PhaseReached Phase.Plan
                    PhaseReached Phase.Tasks
                    ArtifactPresent SpecKitArtifact.Spec
                    ArtifactPresent SpecKitArtifact.Plan
                    TaskState("T1", Real)
                    TaskState("T2", Real)
                    TaskDependsOn("T2", "T1")
                    TaskDependsOn("T3", "T1")
                    SkillBound("T1", "skill-a")
                    SkillBound("T1", "skill-b")
                    ConstitutionArea("scope", true)
                    ConstitutionArea("evidence", true) ]

              let ids = valueBearing |> List.map SpecKit.identify
              Expect.equal (ids |> List.distinct |> List.length) ids.Length "distinct value-bearing facts get distinct ids"
          }

          test "V6 every catalog rule renders to a non-empty sentence and explains itself (top verdict = eval)" {
              // A representative, non-trivial fact set so explain/eval exercise real outcomes.
              let facts =
                  [ fact (PhaseReached Phase.Merge)
                    fact (ConstitutionArea("scope", false))
                    fact (TaskState("T1", Synthetic))
                    fact (TaskState("T2", Real))
                    fact (TaskDependsOn("T2", "T1")) ]

              for r in Catalog.catalog do
                  let (RuleId id) = r.Id
                  Expect.isNotEmpty (Check.render r.Check) (sprintf "%s renders to a non-empty sentence" id)

                  Expect.equal
                      (Explanation.verdict (Check.explain facts r.Check))
                      (Check.eval facts r.Check)
                      (sprintf "%s: explain top verdict = eval" id)
          }

          // ── US2: the phase guard (SC-002) ──

          test "V2(P1) a whenPhase rule is a definite not-applicable (vacuous Pass) before its phase — including with NO PhaseReached at all" {
              let g = SpecKit.whenPhase Phase.Plan alwaysFail

              // No PhaseReached at all ⇒ a missing phase is not a silent default to Merge.
              Expect.equal (Check.eval [] g) Pass "no PhaseReached ⇒ vacuous Pass"

              // Every supplied phase strictly before Plan ⇒ vacuous Pass, never Fail/Uncertain.
              for p in phasesBelow Phase.Plan do
                  Expect.equal (Check.eval [ fact (PhaseReached p) ] g) Pass (sprintf "before Plan (at %A) ⇒ Pass" p)
          }

          test "V2(P2) a whenPhase rule is transparent at/after its phase (adds nothing once the phase holds)" {
              let g = SpecKit.whenPhase Phase.Plan alwaysFail

              for p in phasesAtOrAbove Phase.Plan do
                  let facts = [ fact (PhaseReached p) ]

                  Expect.equal
                      (Check.eval facts g)
                      (Check.eval facts alwaysFail)
                      (sprintf "at/after Plan (at %A) ⇒ reduces to the guarded check" p)
          }

          test "V2(P3/P4) the guard is reified-ness preserving and render/hash distinguish the guarded phase" {
              // P3: reified stays reified; opaque stays opaque.
              Expect.isTrue (Check.isReified alwaysFail) "precondition: alwaysFail is reified"
              Expect.equal (Check.isReified (SpecKit.whenPhase Phase.Plan alwaysFail)) true "guarded reified stays reified"
              Expect.isFalse (Check.isReified opaqueCheck) "precondition: opaque is not reified"
              Expect.equal (Check.isReified (SpecKit.whenPhase Phase.Plan opaqueCheck)) false "guarded opaque stays opaque"

              // P4: the required phase is part of the contract and the cache key.
              let gPlan = SpecKit.whenPhase Phase.Plan alwaysFail
              let gTasks = SpecKit.whenPhase Phase.Tasks alwaysFail
              Expect.notEqual (Check.render gPlan) (Check.render gTasks) "render distinguishes the guarded phase"
              Expect.notEqual (Check.hash gPlan) (Check.hash gTasks) "hash (cache key) distinguishes the guarded phase"
          } ]
