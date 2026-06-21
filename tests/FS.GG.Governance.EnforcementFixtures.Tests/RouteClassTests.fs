module FS.GG.Governance.EnforcementFixtures.Tests.RouteClassTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Routing.Model
open FS.GG.Governance.Routing.Routing
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Findings.Findings
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Ship.Model
open FS.GG.Governance.Ship.Ship
open FS.GG.Governance.EnforcementFixtures.Tests.Support

// The route-class section asserted against the GENUINE F015/F017 cores over real `TypedFacts` (FR-003,
// Principle V — no mocks). These pin the routine-vs-fenced-vs-unknown-governed-path dimension the plan
// row requires, including the routine "never default-deny even under the strictest dials" edge.

/// Route one already-normalized candidate path against `routeClassFacts` and classify it: returns the
/// `(routingResult, findingId option)` the genuine cores produce.
let private classify (rawPath: string) : RoutingResult * FindingId option =
    let report = route routeClassFacts [ normalizePath rawPath ]

    let outcome =
        match report.Routings with
        | [ pr ] -> pr.Result
        | _ -> failwith "expected exactly one routing"

    let finding =
        match (findUnknownGovernedPaths routeClassFacts report).Findings with
        | f :: _ -> Some f.Id
        | [] -> None

    outcome, finding

[<Tests>]
let tests =
    testList
        "F028 route classes"
        [ test "routine — out-of-scope, no finding, never default-denies even under the strictest dials" {
              let outcome, finding = classify "docs/readme.md"
              Expect.equal outcome OutOfScope "a path outside the governed root is out-of-scope"
              Expect.equal finding None "an out-of-scope path is never a finding (no global default-deny)"

              // A routine path selects nothing, so the whole-change rollup at the STRICTEST dials is a
              // clean pass — never a default-deny (Edge: routine under strictest dials).
              let decision = rollup (mkRoute [] []) RunMode.Release Profile.Release
              Expect.equal decision.Verdict Pass "an empty change passes even at release/release"
              Expect.isEmpty decision.Blockers "a routine change yields no blockers at any dial"
          }

          test "fenced — routes into its capability domain, no finding" {
              let outcome, finding = classify "src/build/Main.fs"

              match outcome with
              | Routed(DomainId d, _, _) -> Expect.equal d "build" "a fenced path routes into its declared domain"
              | other -> failtestf "expected Routed, got %A" other

              Expect.equal finding None "a routed path is never an unknown-governed-path finding"
          }

          test "unknown-governed-path — unmatched in root, ordinary explicit finding" {
              let outcome, finding = classify "src/new/Thing.fs"
              Expect.equal outcome UnmatchedInRoot "an in-root path matching no glob is unmatched-in-root"
              Expect.equal finding (Some UnknownGovernedPath) "an ordinary in-root unknown is an explicit finding"
          }

          test "protected-surface unknown — unmatched in root, escalated finding" {
              let outcome, finding = classify "src/boundary/Api.fs"
              Expect.equal outcome UnmatchedInRoot "an unknown on a protected boundary is still unmatched-in-root"
              Expect.equal finding (Some UnknownProtectedBoundaryPath) "an unknown on a declared protected surface escalates"
          } ]
