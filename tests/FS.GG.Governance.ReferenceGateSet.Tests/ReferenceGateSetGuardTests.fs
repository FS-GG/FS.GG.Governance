module FS.GG.Governance.ReferenceGateSet.Tests.ReferenceGateSetGuardTests

// 079: the FR-010 regression guard. It loads the ON-DISK reference `.fsgg` at
// samples/sdd-reference-gate-set/ through the EXISTING public pipeline an adopter/CLI uses —
// Config.Loader.loadAndValidate -> Gates.buildRegistry -> Routing.route -> Route.select ->
// Enforcement.deriveEffectiveSeverity — and freezes its invariants G1-G7 (contracts/
// regression-guard.contract.md). Real evidence (Principle V): no synthetic facts, no mocked
// domain logic. No new public surface (Tier 2).

open System.IO
open Expecto
open FS.GG.Governance.Config
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Routing.Model
open FS.GG.Governance.Findings.Model
open FS.GG.Governance.Route.Model
open FS.GG.Governance.Enforcement

// ── Shared load fixture (T009) ──
// Resolve the repo root via the shared helper, point at the reference's `.fsgg` PARENT (the
// loader appends `.fsgg/`), and load once through the real config edge for every assertion to share.

let private referenceDir =
    Path.Combine(FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot, "samples", "sdd-reference-gate-set")

let private validation = Loader.loadAndValidate referenceDir

/// The `Valid` facts, or fail the calling test with the diagnostics (one shared load).
let private requireFacts () =
    match validation with
    | Valid f -> f
    | Invalid diags -> failtestf "reference .fsgg did not load Valid: %A" diags

// The candidate paths shaped to the SDD reference skeleton. `App.sln` exercises the `*.sln`
// path-map glob that `src/App/Program.fs` alone would leave untested; `build.fsx` exercises the
// evidence glob even though the skeleton ships no such file on disk (SC-004 is a property of the
// path-map, not of physical files — research D3).
let private candidatePaths =
    [ "src/App/Program.fs"; "App.sln"; "tests/App.Tests/Tests.fs"; "build.fsx" ]

/// Drive the real F018->F015->F017->F019 chain over the loaded facts, exactly as `fsgg route` would.
let private routeResultOf (f: TypedFacts) : RouteResult =
    let registry = FS.GG.Governance.Gates.Gates.buildRegistry f
    let report =
        candidatePaths
        |> List.map normalizePath
        |> FS.GG.Governance.Routing.Routing.route f
    let findings = FS.GG.Governance.Findings.Findings.findUnknownGovernedPaths f report
    FS.GG.Governance.Route.Route.select registry report findings

/// The gates the candidate paths select (the union, deduped by `GateId`).
let private selectedGates (f: TypedFacts) : Gate list =
    routeResultOf f |> fun r -> r.SelectedGates |> List.map (fun sg -> sg.Gate)

// `RunMode.Verify` (ordinal 3) is the everyday inner/verify loop — and the ONLY mode where a
// `block-on-ship` gate is Light-advisory yet Strict-blocking (at Focused/below neither blocks; at
// Gate/above even Light blocks). It is chosen deliberately so G6/G7 demonstrate the ratchet on the
// SAME failing change; it is not arbitrary (research D5).
let private decideUnder (profile: Enforcement.Profile) (gate: Gate) : Enforcement.Severity =
    let decision =
        Enforcement.deriveEffectiveSeverity
            { BaseSeverity = Enforcement.Blocking // the failing-change case
              Maturity = gate.Maturity
              Mode = Enforcement.Verify
              Profile = profile }
    decision.EffectiveSeverity

let private expectedGateIds = set [ "build:build"; "test:test"; "evidence:evidence" ]

[<Tests>]
let guard =
    testList
        "ReferenceGateSetGuard"
        [
          // G1 (FR-007/SC-002) — loads Valid with an EMPTY diagnostics list. Because `loadAndValidate`
          // returns `Valid` only when zero diagnostics (UnknownField included) were produced, asserting
          // `Valid` here also pins "0 unknown-field findings".
          test "G1 reference Loads Valid with empty diagnostics" {
              match validation with
              | Valid _ -> ()
              | Invalid diags -> failtestf "expected Valid with 0 diagnostics; got %d: %A" (List.length diags) diags
          }

          // G2 (FR-002/FR-003/SC-001) — exactly 3 gates: build:build, test:test, evidence:evidence.
          // Surfaces are NOT projected into the registry (buildRegistry reads only Capabilities.Checks).
          test "G2 Routes registry has exactly 3 gates build test evidence" {
              let f = requireFacts ()
              let registry = FS.GG.Governance.Gates.Gates.buildRegistry f
              let ids = registry.Gates |> List.map (fun g -> gateIdValue g.Id) |> Set.ofList
              Expect.equal ids expectedGateIds "registry must hold exactly the 3 reference gates (guards 'rots to empty')"
          }

          // G3 (FR-004/SC-001/SC-007) — every gate's command prerequisite resolves to a declared
          // tooling.yml command; 0 dangling command references.
          test "G3 Routes every gate command prerequisite is declared in tooling" {
              let f = requireFacts ()
              let declaredCommands =
                  match f.Tooling with
                  | Some t -> t.Commands |> List.map (fun c -> let (CommandId i) = c.Id in i) |> Set.ofList
                  | None -> Set.empty
              let registry = FS.GG.Governance.Gates.Gates.buildRegistry f
              let referenced =
                  registry.Gates
                  |> List.collect (fun g -> g.Prerequisites |> List.map (fun (RequiresCommand (CommandId c)) -> c))
              Expect.equal (List.length referenced) 3 "all 3 gates carry a command prerequisite"
              for c in referenced do
                  Expect.isTrue (declaredCommands.Contains c) (sprintf "command '%s' must be declared in tooling.yml (no dangling ref)" c)
          }

          // G4 (FR-005/FR-008/SC-004) — build/test/evidence each selected by a candidate path; 0 orphan
          // checks (every gate selectable), 0 orphan commands (every declared command referenced), 0
          // unreachable domains.
          test "G4 Routes build test evidence each selected, no orphans" {
              let f = requireFacts ()
              let selectedIds = selectedGates f |> List.map (fun g -> gateIdValue g.Id) |> Set.ofList
              Expect.equal selectedIds expectedGateIds "each of build/test/evidence is selected by its candidate path (0 orphan checks/unreachable domains)"

              // 0 orphan commands: every declared tooling command is referenced by a gate prerequisite.
              let registry = FS.GG.Governance.Gates.Gates.buildRegistry f
              let referenced =
                  registry.Gates
                  |> List.collect (fun g -> g.Prerequisites |> List.map (fun (RequiresCommand (CommandId c)) -> c))
                  |> Set.ofList
              let declaredCommands =
                  match f.Tooling with
                  | Some t -> t.Commands |> List.map (fun c -> let (CommandId i) = c.Id in i) |> Set.ofList
                  | None -> Set.empty
              Expect.equal referenced declaredCommands "every declared tooling command is referenced by a gate (0 orphan/dead commands)"
          }

          // G5 (FR-006/SC-007) — defaultProfile is the load-bearing non-blocking `light`.
          test "G5 Profile defaultProfile is light" {
              let f = requireFacts ()
              match f.Policy with
              | Some p ->
                  let (ProfileId dp) = p.DefaultProfile
                  Expect.equal dp "light" "defaultProfile must be 'light' (guards drift to blocking)"
              | None -> failtest "policy.yml must be present and declare defaultProfile: light"
          }

          // G6 (FR-006/SC-003) — under Light @ Verify on a failing change, EVERY selected gate is
          // Advisory (0 blocking outcomes): non-blocking by default.
          test "G6 Profile under Light at Verify all selected gates Advisory" {
              let f = requireFacts ()
              let blocking =
                  selectedGates f
                  |> List.filter (fun g -> decideUnder Enforcement.Light g = Enforcement.Blocking)
              Expect.isEmpty blocking "under Light @ Verify every selected gate must derive Advisory (0 blocking)"
          }

          // G7 (SC-006) — under Strict @ Verify on the SAME failing change, >=1 selected gate is
          // Blocking: the gates CAN block, so `light` is a chosen default, not an inability.
          test "G7 Profile under Strict at Verify at least one Blocking" {
              let f = requireFacts ()
              let blocking =
                  selectedGates f
                  |> List.filter (fun g -> decideUnder Enforcement.Strict g = Enforcement.Blocking)
              Expect.isNonEmpty blocking "under Strict @ Verify >=1 selected gate must derive Blocking on the same change"
          }

          // T017 (US3 / FR-003) — evidence is a first-class governed gate bound to the declared
          // build-evidence command with `warn` maturity, and stays Advisory under BOTH Light and Strict
          // at Verify: evidence never blocks on first touch even when the block-capable gates do.
          test "evidence:evidence is a declared Profile advisory-everywhere gate" {
              let f = requireFacts ()
              let evidence =
                  selectedGates f
                  |> List.tryFind (fun g -> gateIdValue g.Id = "evidence:evidence")
              match evidence with
              | None -> failtest "evidence:evidence must be selected for build.fsx"
              | Some g ->
                  Expect.equal g.Prerequisites [ RequiresCommand(CommandId "build-evidence") ] "evidence gate is bound to the declared build-evidence command"
                  Expect.equal g.Maturity Warn "evidence carries `warn` maturity (advisory everywhere)"
                  Expect.equal (decideUnder Enforcement.Light g) Enforcement.Advisory "evidence stays Advisory under Light @ Verify"
                  Expect.equal (decideUnder Enforcement.Strict g) Enforcement.Advisory "evidence stays Advisory under Strict @ Verify (never blocks on first touch)"
          }
        ]
