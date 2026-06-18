module FS.GG.Governance.Kernel.Tests.RouteTests

open Expecto
open FsCheck
open FS.GG.Governance.Kernel

// ── The light routing layer (F07 · 007-routing-severity-modes) — V40–V47 ──
//
// EVIDENCE-OBLIGATIONS NOTE (Principle IV / V): F07 is a PURE DERIVATION — Principle IV
// (Elmish/MVU) is N/A (no Model/Msg/Effect/interpreter; stakesOf/route/renderRoute map
// supplied values to a Stakes/Route/string with no I/O, no probe, no agent, no clock).
// All evidence here is REAL: real Fence/CheckRule/Check values authored through the
// public CheckRule.rule/blocking constructors over real reified checks; FsCheck for the
// order-independence/determinism/light-by-default properties. No synthetic fixtures,
// no mocks/stubs, hence no `// SYNTHETIC:` disclosures.

// ── Real, domain-neutral fixtures: a 'change is a Set<string> of changed "paths" ──
let private mergeFence: Fence<Set<string>> =
    { Name = "merge-boundary"
      Trips = fun c -> c |> Set.exists (fun p -> p.StartsWith "src/") }

let private secFence: Fence<Set<string>> =
    { Name = "security-surface"
      Trips = fun c -> c.Contains "src/Auth.fs" }

let private fences = [ mergeFence; secFence ]

let private spec doc sec : SpecSource = { Document = doc; Section = sec }
let private chk name : Check<string> = Check.probe name [] [] (fun _ -> Met)

let private okRule =
    function
    | Ok r -> r
    | Error e -> failtestf "rule authoring expected Ok, got Error %A" e

let private advisoryRule id s c =
    CheckRule.rule (RuleId id) Deterministic s c |> okRule

let private blockingRule id s c =
    CheckRule.rule (RuleId id) Deterministic s c |> Result.map CheckRule.blocking |> okRule

let private nonEmpty (s: string) = not (System.String.IsNullOrWhiteSpace s)

// A change that trips merge-boundary (under src/) but NOT security-surface.
let private srcChange = set [ "src/Api.fs"; "README.md" ]
// A change tripping no declared fence.
let private docsChange = set [ "README.md"; "docs/guide.md" ]
// A change tripping BOTH fences (under src/ AND the security file).
let private bothChange = set [ "src/Auth.fs" ]

let private allModes = [ Sandbox; Inner; Gate ]

let private propConfig =
    { FsCheckConfig.defaultConfig with
        maxTest = 300
        replay = Some(4242, 2424) } // fixed seed → reproducible (cf. F02 V6)

[<Tests>]
let tests =
    testList
        "Route"
        [
          // ── User Story 1 (P1): light by default — an ordinary change earns no gates ──

          test "V40 light by default: no matching fence (and empty fence set) ⇒ Routine + empty Blocking in every mode (FR-006, SC-001)" {
              // stakesOf: empty fence set and non-matching fences both ⇒ Routine.
              Expect.equal (Route.stakesOf [] docsChange) Routine "empty fence set ⇒ Routine"
              Expect.equal (Route.stakesOf fences docsChange) Routine "no fence trips ⇒ Routine"

              let rule = blockingRule "peer-review" (spec "constitution.md" "I") (chk "peer-reviewed")

              for mode in allModes do
                  // non-empty fences, none trip
                  let r = Route.route fences [ rule ] mode docsChange
                  Expect.equal r.Stakes Routine (sprintf "Routine in %A — run mode never manufactures stakes" mode)
                  Expect.isEmpty r.Blocking (sprintf "no blocking gates for a routine change in %A" mode)
                  Expect.isTrue (nonEmpty r.Reason) (sprintf "routine route carries a non-empty reason in %A" mode)
                  Expect.stringContains r.Reason "fence" "reason names why it is light (no fence matched)"

                  // empty fence set ⇒ same light outcome
                  let r0 = Route.route [] [ rule ] mode docsChange
                  Expect.equal r0.Stakes Routine (sprintf "empty fence set ⇒ Routine in %A" mode)
                  Expect.isEmpty r0.Blocking (sprintf "empty fence set ⇒ no blocking gates in %A" mode)
          }

          // ── User Story 2 (P1): a fenced change produces a blocking gate naming rule, fence & check ──

          test "V41 single fence trips ⇒ Fenced carrying its name (FR-004, SC-002)" {
              Expect.equal (Route.stakesOf fences srcChange) (Fenced "merge-boundary") "one matching fence ⇒ Fenced with its name"
          }

          test "V42 fenced gate explained: non-empty Blocking; renderRoute names rule + fence + rendered check (FR-012, SC-006/010)" {
              let rule = blockingRule "peer-review" (spec "constitution.md" "I") (chk "peer-reviewed")
              let r = Route.route fences [ rule ] Gate srcChange

              Expect.equal r.Stakes (Fenced "merge-boundary") "fenced at gate carries the fence name"
              Expect.isNonEmpty r.Blocking "a blocking-severity rule on a fenced change at Gate is a blocking gate"

              let rendered = Route.renderRoute r
              Expect.stringContains rendered "peer-review" "render names the rule id"
              Expect.stringContains rendered "merge-boundary" "render names the fence that raised the stakes"
              Expect.stringContains rendered (Check.render rule.Check) "render contains the rendered check text (no drift)"
          }

          test "V46 drift-proof gate: gate.Statement = Check.render rule.Check; route + render run NO probe/review (FR-012, SC-006/010)" {
              // A probe whose Eval would flip a cell / raise if EVER executed — proves
              // route + renderRoute are execution-free (SC-010).
              let executed = ref false

              let landmine: Check<string> =
                  Check.probe "landmine" [] [] (fun _ ->
                      executed.Value <- true
                      failwith "probe Eval was executed — routing must never run a probe")

              let rule =
                  CheckRule.rule (RuleId "explosive") Deterministic (spec "constitution.md" "V") landmine
                  |> Result.map CheckRule.blocking
                  |> okRule

              let r = Route.route fences [ rule ] Gate srcChange
              let gate = List.head r.Blocking
              Expect.equal gate.Statement (Check.render rule.Check) "gate Statement is byte-for-byte Check.render (drift-proof, SC-006)"

              let _ = Route.renderRoute r
              Expect.isFalse executed.Value "no probe Eval ran across route + renderRoute (SC-010)"
          }

          // ── User Story 3 (P2): run mode decides WHEN a fence actually blocks ──

          test "V44 run-mode matrix: advisory in Sandbox/Inner, blocking only in Gate; stakes identical across modes (FR-008/009, SC-004)" {
              let rule = blockingRule "peer-review" (spec "constitution.md" "I") (chk "peer-reviewed")

              let routes = allModes |> List.map (fun m -> m, Route.route fences [ rule ] m srcChange)

              for mode, r in routes do
                  Expect.equal r.Stakes (Fenced "merge-boundary") (sprintf "stakes identical (Fenced) in %A — mode changes enforcement, not classification" mode)
                  match mode with
                  | Gate ->
                      Expect.isNonEmpty r.Blocking "Gate enforces the blocking-severity gate"
                      Expect.isEmpty r.Advisory "the enforced gate is not also advisory at Gate"
                  | Sandbox
                  | Inner ->
                      Expect.isEmpty r.Blocking (sprintf "%A blocks nothing — advisory only" mode)
                      Expect.equal (List.length r.Advisory) 1 (sprintf "the requirement surfaces as advisory in %A" mode)

              // light-at-Gate: a Routine change at Gate still has no blocking gates (FR-006).
              let lightAtGate = Route.route fences [ rule ] Gate docsChange
              Expect.isEmpty lightAtGate.Blocking "a routine change at Gate is never escalated"
          }

          // ── User Story 4 (P2): deterministic precedence — forbid trumps permit, never positional ──

          testPropertyWithConfig propConfig "V43 order-independent: permuting fences ⇒ identical Stakes, Route, and render (FR-005, SC-003)" <|
              fun (paths: string list) (mode: RunMode) ->
                  // The demo fences call String.StartsWith/Contains, so drop FsCheck's null
                  // strings (a fixture concern); order-independence is the property under test.
                  let change = set (paths |> List.filter (System.String.IsNullOrEmpty >> not))
                  let rule = blockingRule "peer-review" (spec "constitution.md" "I") (chk "peer-reviewed")
                  let permutations = [ fences; List.rev fences ]

                  permutations
                  |> List.forall (fun p ->
                      Route.stakesOf p change = Route.stakesOf fences change
                      && Route.route p [ rule ] mode change = Route.route fences [ rule ] mode change
                      && Route.renderRoute (Route.route p [ rule ] mode change) = Route.renderRoute (Route.route fences [ rule ] mode change))

          test "V43b multi-match ⇒ Fenced carrying deduped, ordinal-sorted, \"; \"-joined names; order-independent (FR-005, SC-002)" {
              let expected = Fenced "merge-boundary; security-surface" // m < s ordinal
              Expect.equal (Route.stakesOf fences bothChange) expected "both fences trip ⇒ both names, ordinal-sorted"
              Expect.equal (Route.stakesOf (List.rev fences) bothChange) expected "reordering fences ⇒ identical Fenced name"
              // a duplicate fence name is not double-counted
              let dup = [ mergeFence; mergeFence; secFence ]
              Expect.equal (Route.stakesOf dup bothChange) expected "duplicate fence names de-duplicated"
          }

          // ── User Story 5 (P3): every route is short, filterable, and self-explaining ──

          test "V45 reason mandatory: every route (fenced-at-gate, advisory, routine over empty sets) has a non-empty Reason (FR-011, SC-005)" {
              let rule = blockingRule "peer-review" (spec "constitution.md" "I") (chk "peer-reviewed")
              let fencedAtGate = Route.route fences [ rule ] Gate srcChange
              let advisory = Route.route fences [ rule ] Inner srcChange
              let routine = Route.route [] [] Inner srcChange

              Expect.isTrue (nonEmpty fencedAtGate.Reason) "fenced-at-gate route has a reason"
              Expect.isTrue (nonEmpty advisory.Reason) "advisory route has a reason"
              Expect.isTrue (nonEmpty routine.Reason) "routine route over empty fence/rule sets has a reason"
              // renderRoute is non-empty even for a routine route with no requirements.
              Expect.isTrue (nonEmpty (Route.renderRoute routine)) "renderRoute of a bare routine route is still a non-empty block"
          }

          test "V47 short, filterable, deterministic: Blocking = exactly the blocking gates, bounded by applicable rules; render deterministic & execution-free; total over empty rules (FR-013/014/015, SC-007/008/009/010)" {
              let executed = ref false

              let watched name : Check<string> =
                  Check.probe name [] [] (fun _ ->
                      executed.Value <- true
                      Met)

              // A mix of severities, all applicable to the (fenced) change.
              let b1 = blockingRule "b1" (spec "doc" "1") (watched "b1")
              let a1 = advisoryRule "a1" (spec "doc" "2") (watched "a1")
              let b2 = blockingRule "b2" (spec "doc" "3") (watched "b2")
              let rules = [ b1; a1; b2 ]

              let r = Route.route fences rules Gate srcChange

              // Blocking = exactly the Blocking-severity entries; Advisory = the rest.
              Expect.equal (r.Blocking |> List.map (fun e -> e.Id)) [ RuleId "b1"; RuleId "b2" ] "Blocking is exactly the blocking-severity entries, in catalog order"
              Expect.equal (r.Advisory |> List.map (fun e -> e.Id)) [ RuleId "a1" ] "Advisory is exactly the rest"
              Expect.isTrue (r.Blocking |> List.forall (fun e -> e.Severity = Blocking)) "every Blocking entry has Blocking severity"

              // Bounded by the applicable rules (not the catalog): union ⊆ rules.
              let union = r.Blocking @ r.Advisory
              Expect.equal (List.length union) (List.length rules) "the partition is bounded by the applicable rules (FR-013, SC-007)"

              // Deterministic: route and renderRoute byte-for-byte identical on repeat (SC-008).
              Expect.equal (Route.route fences rules Gate srcChange) r "route is byte-for-byte reproducible"
              Expect.equal (Route.renderRoute r) (Route.renderRoute r) "renderRoute is deterministic"

              // Execution-free across all the above (SC-010).
              Expect.isFalse executed.Value "no probe Eval ran across route + renderRoute (SC-010)"

              // Totality over the empty rule set with a Fenced change at Gate (FR-015, SC-009).
              let bare = Route.route fences [] Gate srcChange
              Expect.isEmpty bare.Advisory "empty rule set ⇒ no advisory requirements"
              Expect.isEmpty bare.Blocking "empty rule set ⇒ no blocking gates even for a high-stakes change"
              Expect.equal bare.Stakes (Fenced "merge-boundary") "stakes still classified for a high-stakes change with no applicable rule"
              Expect.isTrue (nonEmpty bare.Reason) "a bare fenced route still carries a reason — the partition is total"
          } ]
