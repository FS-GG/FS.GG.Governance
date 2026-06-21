module FS.GG.Governance.RouteExplain.Tests.AlternativeTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Route.Model
open FS.GG.Governance.RouteExplain
open FS.GG.Governance.RouteExplain.Model
open FS.GG.Governance.RouteExplain.Tests.Support

// US2 — each high-cost finding's `Alternative` is resolved against the catalog: a same-domain, strictly
// cheaper, locally-runnable gate (cheapest, ties by `GateId`) as `CheaperLocalAlternative`, else the
// explicit `NoCheaperLocalAlternative` — always present (SC-003/SC-004, plan D4/D6; laws L3/L4/L5).

/// A single-finding route selecting one high-cost gate `h`; `explain` against `registry` ⇒ that finding's
/// resolved alternative.
let private alternativeFor (h: Gate) (registry: GateRegistry) : AlternativeOutcome =
    let route = routeOf [ selGate h [ sp "src/a.fs" "src/**" ] ]
    let explanation = RouteExplain.explain route registry
    (explanation.Findings |> List.exactlyOne).Alternative

[<Tests>]
let tests =
    testList
        "Alternative"
        [ test "names the cheapest same-domain strictly-cheaper local gate — the worked example (L4/L5)" {
              // contracts/explanation-semantics.md §2: build:full (Exhaustive Ci) over the worked catalog ⇒
              // build:unit (Cheap Local), the cheapest local same-domain candidate.
              let h = gate "build" "full" Exhaustive Ci
              let outcome = alternativeFor h (catalog workedExampleGates)

              match outcome with
              | CheaperLocalAlternative g ->
                  Expect.equal (gateIdValue g.Id) "build:unit" "the cheapest local same-domain candidate is named"
              | NoCheaperLocalAlternative -> failtest "expected a named alternative (build:unit)"
          }

          test "removing the cheapest candidate falls back to the next-cheapest local candidate (L5)" {
              let h = gate "build" "full" Exhaustive Ci
              // Drop build:unit; build:integration (Medium LocalOrCi) becomes the cheapest candidate.
              let withoutUnit =
                  workedExampleGates |> List.filter (fun g -> gateIdValue g.Id <> "build:unit")

              match alternativeFor h (catalog withoutUnit) with
              | CheaperLocalAlternative g ->
                  Expect.equal (gateIdValue g.Id) "build:integration" "next-cheapest local candidate is named"
              | NoCheaperLocalAlternative -> failtest "expected build:integration"
          }

          test "equal-cost same-domain local gate is NOT a candidate ⇒ none (L4, strictly-cheaper)" {
              let h = gate "build" "full" High Local
              let registry = catalog [ gate "build" "peer" High Local ] // same cost, not strictly cheaper
              Expect.equal (alternativeFor h registry) NoCheaperLocalAlternative "equal cost never qualifies"
          }

          test "strictly-cheaper same-domain but Ci/Release gates are NOT candidates ⇒ none (L4, local)" {
              let h = gate "build" "full" Exhaustive Local

              let registry =
                  catalog
                      [ gate "build" "ci-cheap" Cheap Ci // cheaper, same domain, but Ci
                        gate "build" "rel-cheap" Cheap Release ] // cheaper, same domain, but Release

              Expect.equal
                  (alternativeFor h registry)
                  NoCheaperLocalAlternative
                  "non-local cheaper same-domain gates never qualify"
          }

          test "strictly-cheaper local but different-domain gate is NOT a candidate ⇒ none (L4, same-domain)" {
              let h = gate "build" "full" Exhaustive Local
              let registry = catalog [ gate "docs" "links" Cheap Local ] // cheaper + local, wrong domain
              Expect.equal (alternativeFor h registry) NoCheaperLocalAlternative "cross-domain never qualifies"
          }

          test "local-permission truth table: Local/LocalOrCi qualify; Ci/Release do not (D6)" {
              let h = gate "build" "full" Exhaustive Local

              for env, qualifies in [ Local, true; LocalOrCi, true; Ci, false; Release, false ] do
                  let registry = catalog [ gate "build" "alt" Cheap env ]

                  match alternativeFor h registry, qualifies with
                  | CheaperLocalAlternative g, true ->
                      Expect.equal (gateIdValue g.Id) "build:alt" (sprintf "%A is locally runnable" env)
                  | NoCheaperLocalAlternative, false -> () // correct: not local
                  | outcome, _ -> failtestf "env %A: unexpected outcome %A (expected qualifies=%b)" env outcome qualifies
          }

          test "tie-break: among equally-cheapest candidates the least GateId ordinal is named (L5, FR-007)" {
              let h = gate "build" "full" Exhaustive Local
              // Two Cheap Local candidates: tie on cost ⇒ least GateId "build:aaa" wins.
              let registry =
                  catalog [ gate "build" "bbb" Cheap Local; gate "build" "aaa" Cheap Local ]

              match alternativeFor h registry with
              | CheaperLocalAlternative g -> Expect.equal (gateIdValue g.Id) "build:aaa" "least GateId breaks the cost tie"
              | NoCheaperLocalAlternative -> failtest "expected build:aaa"
          }

          test "cheapest wins over a same-domain local but more-expensive candidate (L5)" {
              let h = gate "build" "full" Exhaustive Local

              let registry =
                  catalog
                      [ gate "build" "high-local" High Local // cheaper than Exhaustive, but not the cheapest
                        gate "build" "cheap-local" Cheap Local ] // the cheapest candidate

              match alternativeFor h registry with
              | CheaperLocalAlternative g -> Expect.equal (gateIdValue g.Id) "build:cheap-local" "the cheapest candidate is named"
              | NoCheaperLocalAlternative -> failtest "expected build:cheap-local"
          }

          testPropertyWithConfig fscheckConfig "every finding carries a present Alternative — never omitted (L3, SC-003)" (fun
                                                                                                                                (route: RouteResult)
                                                                                                                                (registry: GateRegistry) ->
              let explanation = RouteExplain.explain route registry
              // Every finding's Alternative is one of the two constructors — always present (no-hide).
              explanation.Findings
              |> List.forall (fun f ->
                  match f.Alternative with
                  | CheaperLocalAlternative _
                  | NoCheaperLocalAlternative -> true)) ]
