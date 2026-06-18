module FS.GG.Governance.Adapters.SpecKit.Tests.CatalogTests

open Expecto
open FS.GG.Governance.Kernel
open FS.GG.Governance.Adapters.Spi
open FS.GG.Governance.Adapters.SpecKit
open FS.GG.Governance.Adapters.SpecKit.Tests.ExampleAdapters

// US3 (advisory inner / single merge fence, SC-003), US4 (the constitution dial is the
// blocking set, SC-005), US5 (the evidence/taint model is a kernel derivation, SC-004).
// Everything is evaluated through the BUILT adapter + Spi + Kernel libraries.

let private isFail (v: Verdict) =
    match v with
    | Fail _ -> true
    | _ -> false

let private mergeChange: SpecKitChange =
    { Phase = Phase.Merge; Surfaces = Set.ofList [ SpecKitArtifact.Tasks ] }

/// The merge-time blocking set (by RuleId) the dial produces — the dial IS this set.
let private mergeBlockingIds (dial: ConstitutionDial) : Set<RuleId> =
    let adapter = Catalog.adapter judge dial
    let route = Route.route adapter.Fences adapter.Rules Gate mergeChange
    route.Blocking |> List.map (fun e -> e.Id) |> Set.ofList

let private innerPhases =
    [ Phase.Constitution
      Phase.Specify
      Phase.Clarify
      Phase.Plan
      Phase.Tasks
      Phase.Analyze
      Phase.Implement ]

[<Tests>]
let tests =
    testList
        "Catalog"
        [ // ── US3 ──
          test "V3 the inner loop is advisory across the whole catalog — Blocking = [] for every inner phase, even when a deterministic check fails" {
              let adapter = specKitAdapter

              for p in innerPhases do
                  let change = { Phase = p; Surfaces = Set.ofList [ SpecKitArtifact.Tasks ] }
                  let route = Route.route adapter.Fences adapter.Rules Inner change
                  Expect.isEmpty route.Blocking (sprintf "inner phase %A is advisory (nothing blocks)" p)

              // A failing deterministic check (synthetic evidence) at an inner phase still
              // does not block — the route partitions by severity/mode, not by verdict.
              let tainted = [ fact (TaskState("T1", Synthetic)) ]
              Expect.isTrue (isFail (Check.eval tainted Catalog.evidenceNotSynthetic.Check)) "evidence check fails on synthetic"
              let innerRoute = Route.route adapter.Fences adapter.Rules Inner { Phase = Phase.Implement; Surfaces = Set.empty }
              Expect.isEmpty innerRoute.Blocking "a failing deterministic check still does not block the inner loop"
          }

          test "V3 merge is the single fence that flips to a blocking gate; a failing blocking rule refuses the merge" {
              let adapter = specKitAdapter
              let route = Route.route adapter.Fences adapter.Rules Gate mergeChange

              match route.Stakes with
              | Fenced name -> Expect.stringContains name "feature-merge" "the merge fence tripped"
              | Routine -> failtest "merge change must be Fenced"

              Expect.equal
                  (route.Blocking |> List.map (fun e -> e.Id) |> Set.ofList)
                  (Set.ofList
                      [ RuleId "constitution-complete"
                        RuleId "contracts-current"
                        RuleId "fenced-surfaces-verified"
                        RuleId "evidence-not-synthetic" ])
                  "the merge blocking set is the dial's blocking rules"

              // A failing blocking rule (synthetic evidence) refuses the merge.
              let tainted =
                  [ fact (TaskState("T1", Synthetic)); fact (TaskState("T2", Real)); fact (TaskDependsOn("T2", "T1")) ]

              Expect.isTrue (isFail (Check.eval tainted Catalog.evidenceNotSynthetic.Check)) "the blocking rule fails ⇒ merge refused"
          }

          test "V3 an earlier hard-stop is one opt-in EarlyFences entry — no kernel change" {
              // NOTE: "recompute from base" is the HOST's (F08) job — the merge route is
              // evaluated over base-branch facts, not adapter logic (data-model D7). This
              // test only proves the opt-in fence assembles and trips at its phase.
              let dial = { Catalog.defaultDial with EarlyFences = [ ("no-cyclic-tasks", Phase.Tasks) ] }
              let fences = Catalog.fences dial
              let tasksChange = { Phase = Phase.Tasks; Surfaces = Set.empty }

              Expect.isTrue
                  (fences |> List.exists (fun f -> f.Trips tasksChange))
                  "an EarlyFences entry adds a fence that trips at its phase"

              Expect.isFalse
                  (Catalog.fences Catalog.defaultDial |> List.exists (fun f -> f.Trips tasksChange))
                  "the default dial does not fence the inner loop"
          }

          // ── US4 ──
          test "V5 constitutionComplete is a definite Unmet on a placeholder area, advisory inner, blocking at merge under the dial" {
              let placeholder = [ fact (PhaseReached Phase.Merge); fact (ConstitutionArea("scope", false)) ]

              let verdict = Check.eval placeholder Catalog.constitutionComplete.Check
              Expect.isTrue (isFail verdict) "a placeholder area ⇒ a definite Fail (not Uncertain)"

              // advisory inner …
              let adapter = specKitAdapter
              let inner = Route.route adapter.Fences adapter.Rules Inner { Phase = Phase.Constitution; Surfaces = Set.empty }
              Expect.isEmpty inner.Blocking "constitution check is advisory in the inner loop"

              // … blocking at merge under the default dial.
              Expect.isTrue
                  ((mergeBlockingIds Catalog.defaultDial).Contains(RuleId "constitution-complete"))
                  "constitution check blocks at merge under defaultDial"
          }

          test "V5 the merge blocking set is a function of the dial (asserted by specific RuleId, not by count)" {
              Expect.equal
                  (mergeBlockingIds Catalog.defaultDial)
                  (Set.ofList
                      [ RuleId "constitution-complete"
                        RuleId "contracts-current"
                        RuleId "fenced-surfaces-verified"
                        RuleId "evidence-not-synthetic" ])
                  "defaultDial promotes exactly these (guards finding I1 — a string mismatch would promote nothing)"

              Expect.equal
                  (mergeBlockingIds { Catalog.defaultDial with BlockingAtMerge = Set.empty })
                  (Set.ofList [ RuleId "evidence-not-synthetic" ])
                  "the 'light' posture blocks only evidence-not-synthetic"

              Expect.equal
                  (mergeBlockingIds { Catalog.defaultDial with BlockingAtMerge = Set.ofList [ RuleId "plan-satisfies-spec" ] })
                  (Set.ofList [ RuleId "plan-satisfies-spec"; RuleId "evidence-not-synthetic" ])
                  "promoting a single arbitrary id makes exactly that rule block (plus the non-negotiable evidence rule)"
          }

          test "V5 the dial assembles the fences — mergeFence always present plus one per EarlyFences entry" {
              let dial = { Catalog.defaultDial with EarlyFences = [ ("no-cyclic-tasks", Phase.Tasks) ] }
              let adapter = Catalog.adapter judge dial

              Expect.equal
                  (adapter.Fences |> List.map (fun f -> f.Name))
                  (Catalog.fences dial |> List.map (fun f -> f.Name))
                  "(adapter judge dial).Fences = Catalog.fences dial (compared by name — Fence carries a function)"

              Expect.equal adapter.Fences.Length 2 "mergeFence + one early fence"
              Expect.isTrue (adapter.Fences |> List.exists (fun f -> f.Name = "feature-merge")) "the merge fence is always present"
          }

          // ── US5 ──
          test "V4 AutoSynthetic propagates down a TaskDependsOn chain via the kernel's Evidence.effective (not adapter code)" {
              let facts =
                  [ fact (TaskState("T1", Synthetic)); fact (TaskState("T2", Real)); fact (TaskDependsOn("T2", "T1")) ]

              Expect.isTrue
                  (isFail (Check.eval facts Catalog.evidenceNotSynthetic.Check))
                  "T2 is AutoSynthetic via T1 through the kernel fixed point ⇒ the check fails"
          }

          test "V4 evidence-not-synthetic is non-negotiable — Blocking under every dial, and no flag flips it" {
              Expect.equal Catalog.evidenceNotSynthetic.Severity Blocking "evidence-not-synthetic is Blocking by default"

              for dial in [ Catalog.defaultDial; { Catalog.defaultDial with BlockingAtMerge = Set.empty } ] do
                  Expect.isTrue
                      ((mergeBlockingIds dial).Contains(RuleId "evidence-not-synthetic"))
                      "evidence-not-synthetic blocks at merge regardless of the dial"

                  let adapter = Catalog.adapter judge dial
                  let ev = adapter.Rules |> List.find (fun r -> r.Id = RuleId "evidence-not-synthetic")
                  Expect.equal ev.Severity Blocking "the rule stays Blocking under the light dial too"
          }

          test "V4 tasksGraphWellFormed: malformed ≠ tainted; definite Unmet; advisory inner, blockable at merge only if promoted" {
              let atTasks extra = fact (PhaseReached Phase.Tasks) :: extra
              let check = Catalog.tasksGraphWellFormed.Check

              // cyclic graph (Evidence.build → Cycle)
              let cyclic =
                  atTasks
                      [ fact (TaskState("A", Real))
                        fact (TaskState("B", Real))
                        fact (TaskDependsOn("A", "B"))
                        fact (TaskDependsOn("B", "A")) ]

              Expect.isTrue (isFail (Check.eval cyclic check)) "a cyclic task graph is a definite Unmet (not Unknown)"

              // unresolved dependency target
              let unresolvedDep = atTasks [ fact (TaskState("A", Real)); fact (TaskDependsOn("A", "Z")) ]
              Expect.isTrue (isFail (Check.eval unresolvedDep check)) "an unresolved dependency is a definite Unmet"

              // task missing deps (undeclared dependent)
              let missingDeps = atTasks [ fact (TaskState("A", Real)); fact (TaskDependsOn("Q", "A")) ]
              Expect.isTrue (isFail (Check.eval missingDeps check)) "an undeclared dependent task is a definite Unmet"

              // unresolved skill id
              let unresolvedSkill = atTasks [ fact (TaskState("A", Real)); fact (SkillBound("Q", "skill-x")) ]
              Expect.isTrue (isFail (Check.eval unresolvedSkill check)) "a skill bound to an undeclared task is a definite Unmet"

              // advisory inner / blockable at merge only if the dial promotes it
              Expect.isFalse
                  ((mergeBlockingIds Catalog.defaultDial).Contains(RuleId "tasks-graph"))
                  "tasks-graph is advisory by default — not in the default merge blocking set"

              Expect.isTrue
                  ((mergeBlockingIds { Catalog.defaultDial with BlockingAtMerge = Set.ofList [ RuleId "tasks-graph" ] })
                      .Contains(RuleId "tasks-graph"))
                  "tasks-graph blocks at merge when the dial promotes it"
          } ]
