module FS.GG.Governance.Adapters.SddHandoff.Tests.MappingTests

open Expecto
open FS.GG.Governance.Kernel
open FS.GG.Governance.Adapters.SddHandoff
open FS.GG.Governance.Adapters.SddHandoff.Model

// US1 — ADR-0002 evidence-mapping rows, EACH case named for / commented with its row (SC-002).
// SC-002's "100% of rows" is satisfied jointly by T012 (unknown major) + T015 (these) + T016
// (governedReferences optional) + T024 (readiness.* as a gate).

let private node id state = { Id = id; State = state; Stale = false; Rationale = None }

let private mapped block =
    match Mapping.mapEvidence "readiness/x/governance-handoff.json" block with
    | Ok(ns, _), _ -> ns
    | Error d, _ -> failtestf "expected Ok mapping, got %A" d

[<Tests>]
let tests =
    testList
        "Mapping"
        [ test "ADR-0002 row: pending/real/synthetic/failed/skipped map straight through (FR-003)" {
              let block =
                  { Nodes =
                      [ node "a" DeclaredState.Pending
                        node "b" DeclaredState.Real
                        node "c" DeclaredState.Synthetic
                        node "d" DeclaredState.Failed
                        node "e" DeclaredState.Skipped ]
                    Dependencies = [] }

              let ns = mapped block |> Map.ofList
              Expect.equal ns.["a"] EvidenceState.Pending "pending → Pending"
              Expect.equal ns.["b"] EvidenceState.Real "real → Real"
              Expect.equal ns.["c"] EvidenceState.Synthetic "synthetic → Synthetic"
              Expect.equal ns.["d"] EvidenceState.Failed "failed → Failed"
              Expect.equal ns.["e"] EvidenceState.Skipped "skipped → Skipped"
          }

          test "ADR-0002 row: deferred / accepted-deferral map to Skipped, not Pending (FR-004)" {
              let block =
                  { Nodes = [ node "x" DeclaredState.Deferred; node "y" DeclaredState.AcceptedDeferral ]
                    Dependencies = [] }

              let ns = mapped block |> Map.ofList
              Expect.equal ns.["x"] EvidenceState.Skipped "deferred → Skipped (a [-] skip, not [ ] pending)"
              Expect.equal ns.["y"] EvidenceState.Skipped "accepted-deferral → Skipped"
          }

          test "ADR-0002 row: a stale node carries its underlying state PLUS a StaleEvidence diagnostic (FR-006)" {
              let block =
                  { Nodes = [ { Id = "perf"; State = DeclaredState.Real; Stale = true; Rationale = None } ]
                    Dependencies = [] }

              match Mapping.mapEvidence "readiness/x/governance-handoff.json" block with
              | Ok(ns, _), diags ->
                  Expect.equal (ns |> List.map snd) [ EvidenceState.Real ] "underlying state carried (Real)"
                  Expect.equal (diags |> List.map (fun d -> d.Cause)) [ StaleEvidence ] "exactly one StaleEvidence diagnostic"
              | Error d, _ -> failtestf "expected Ok with stale diagnostic, got %A" d
          }

          test "ADR-0002 row: a declared AutoSynthetic is rejected by Evidence.build (FR-005, defence in depth)" {
              // The DeclaredState union cannot represent autoSynthetic (rejected on read), so this exercises
              // the kernel's independent refusal directly.
              let r = Mapping.effectiveStates [ ("n", EvidenceState.AutoSynthetic) ] []

              match r with
              | Error d -> Expect.equal d.Cause AutoSyntheticDeclared "AutoSynthetic declared is rejected"
              | Ok _ -> failtest "expected Evidence.build to refuse a declared AutoSynthetic node"
          }

          test "ADR-0002 row: Evidence.effective taints a Real node resting on Synthetic → AutoSynthetic (blocking-capable, research D4)" {
              // test:unit (Real) rests on a Synthetic build → effective AutoSynthetic, which makes the
              // derived evidence gate blocking-capable.
              let r =
                  Mapping.effectiveStates
                      [ ("build:lib", EvidenceState.Synthetic); ("test:unit", EvidenceState.Real) ]
                      [ ("test:unit", "build:lib") ]

              match r with
              | Ok states ->
                  Expect.equal states.["build:lib"] EvidenceState.Synthetic "the synthetic root stays Synthetic"
                  Expect.equal states.["test:unit"] EvidenceState.AutoSynthetic "the dependent Real node is tainted AutoSynthetic"
              | Error d -> failtestf "expected Ok effective states, got %A" d
          }

          test "ADR-0002 row: a Failed effective state survives the closure (blocking-capable)" {
              let r = Mapping.effectiveStates [ ("test:unit", EvidenceState.Failed) ] []

              match r with
              | Ok states -> Expect.equal states.["test:unit"] EvidenceState.Failed "Failed is carried into the effective map"
              | Error d -> failtestf "expected Ok, got %A" d
          } ]
