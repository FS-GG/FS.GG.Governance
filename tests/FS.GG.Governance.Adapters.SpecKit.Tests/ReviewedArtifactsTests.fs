module FS.GG.Governance.Adapters.SpecKit.Tests.ReviewedArtifactsTests

open Expecto
open FS.GG.Governance.Kernel
open FS.GG.Governance.Adapters.SpecKit
open FS.GG.Governance.Adapters.SpecKit.Tests.ExampleAdapters

// M-ADPT-2: the agent-review rules must DECLARE the artifacts they review, so a changed plan.md/spec.md moves
// the F04 agent-review cache key and re-opens the review instead of reusing a stale verdict. Before the fix
// these `Opaque` judgements declared NO reads, so the key's artifact half was empty and never moved.

let private planRef = SpecKit.toRef SpecKitArtifact.Plan
let private specRef = SpecKit.toRef SpecKitArtifact.Spec
let private tasksRef = SpecKit.toRef SpecKitArtifact.Tasks

// Replicate the kernel's `AgentReviewed` key derivation (`CheckRule.toRule`) for a rule, given a per-ref hash
// standing in for the sensed artifact content the real composition-root bridge folds in.
let private keyOf (rule: CheckRule<SpecKitFact>) (hashOf: ArtifactRef -> string) =
    CheckRule.cacheKey judge (Check.hash rule.Check) (Check.reads rule.Check |> List.map hashOf) rule.Question

[<Tests>]
let tests =
    testList
        "ReviewedArtifacts"
        [ test "plan-satisfies-spec declares plan.md and spec.md as reviewed artifacts" {
              let reads = Check.reads Catalog.planSatisfiesSpec.Check
              Expect.contains reads planRef "reviews plan.md"
              Expect.contains reads specRef "reviews spec.md"
          }

          test "tasks-complete-ordered declares tasks.md and plan.md as reviewed artifacts" {
              let reads = Check.reads Catalog.tasksCompleteOrdered.Check
              Expect.contains reads tasksRef "reviews tasks.md"
              Expect.contains reads planRef "reviews plan.md"
          }

          test "a changed reviewed-artifact hash moves the review cache key (no stale reuse)" {
              let baseHash (_: ArtifactRef) = "h0"
              let changedPlan (r: ArtifactRef) = if r = planRef then "h-CHANGED" else "h0"
              let k0 = keyOf Catalog.planSatisfiesSpec baseHash
              Expect.notEqual (keyOf Catalog.planSatisfiesSpec changedPlan) k0 "changing plan.md's content hash must change the key"
              Expect.equal (keyOf Catalog.planSatisfiesSpec baseHash) k0 "identical inputs ⇒ identical key"
          }

          test "the reviewing wrapper is verdict-neutral — eval matches the bare Opaque judgement at every phase" {
              // The SAME guarded Opaque WITHOUT the reviewing wrapper: its verdict must be preserved.
              let bare = SpecKit.whenPhase Phase.Plan (Opaque("plan-satisfies-spec", fun _ -> Unknown "judgement"))
              let wrapped = Catalog.planSatisfiesSpec.Check

              for phase in allPhases do
                  let facts = [ fact (PhaseReached phase) ]

                  Expect.equal
                      (Check.eval facts wrapped)
                      (Check.eval facts bare)
                      (sprintf "the reads wrapper must not change the verdict at phase %A" phase)
          }

          test "the reviewing wrapper keeps the check non-reified (stays AgentReviewed, never Deterministic)" {
              Expect.isFalse (Check.isReified Catalog.planSatisfiesSpec.Check) "still carries an Opaque judgement"
          } ]
