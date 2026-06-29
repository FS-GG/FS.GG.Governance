module FS.GG.Governance.Cli.Tests.ProfileAwareHandoffGateTests

open Expecto
open FS.GG.Governance.Kernel
open FS.GG.Governance.Host
open FS.GG.Governance.Cli
open FS.GG.Governance.Adapters.SddHandoff

module ConfigModel = FS.GG.Governance.Config.Model

// 090 (US1/US2): the handoff-gate blocking decision at `route --mode gate` derives from the canonical
// Phase-5 enforcement core parameterized by the active policy profile. These tests drive the public
// Cli.init/update transition (the SAME surface MvuTests uses) over in-memory fixtures — no filesystem,
// process, clock, or fake judge. The profile is carried on the snapshot exactly as the Config-load edge
// (`ArtifactReading.locateDefaultProfile`) supplies it. Tests are named `ProfileAware…` so the quickstart
// `--filter "FullyQualifiedName~ProfileAware"` selects them.

let private snapshot: ProjectSnapshot =
    { Root = "."
      Supplied = []
      Change = { SpecKit = None; DesignSystem = None; Scope = [] }
      Artifacts = []
      Handoffs = []
      DefaultProfile = None }

// A benign host route (no blocking failure), so the ONLY blocking source is the consumed handoff —
// proving the profile shifts the handoff gate's boundary, not some unrelated rule.
let private hostModel: FS.GG.Governance.Host.Model<ProjectFact> =
    { Phase = FS.GG.Governance.Host.Phase.Quiescent
      Facts = []
      Route = { Stakes = Routine; Advisory = []; Blocking = []; Reason = "light" }
      Pending = Set.empty
      Disclosures = []
      Failures = []
      Rounds = 0 }

let private emptyBudget =
    { Requested = []
      CacheHits = []
      CacheMisses = []
      FreshDispatches = []
      Pending = []
      BudgetExhausted = [] }

let private handoffRead (source: string) (json: string) : Reader.HandoffRead = { Source = source; Json = json }

// A failing handoff consumes to a `BlockOnShip` gate (base Blocking); a satisfied one to `Warn`.
let private failingHandoff =
    handoffRead
        "readiness/wi-1/governance-handoff.json"
        """{ "contractVersion": "1.0.0", "schemaVersion": 1,
             "evidence": { "nodes": [ { "id": "test:unit", "state": "failed" } ], "dependencies": [] } }"""

let private satisfiedHandoff =
    handoffRead
        "readiness/wi-2/governance-handoff.json"
        """{ "contractVersion": "1.0.0", "schemaVersion": 1,
             "evidence": { "nodes": [ { "id": "test:unit", "state": "real" } ], "dependencies": [] } }"""

let private profile (raw: string) : ConfigModel.ProfileId option = Some(ConfigModel.ProfileId raw)

// Drive parse → snapshot (with handoffs + declared profile) → host-completion and recover the route
// exit from the emitted WriteOutput effect.
let private routeExit (mode: string) (declared: ConfigModel.ProfileId option) (handoffs: Reader.HandoffRead list) : ExitDecision =
    let argv = [ "route"; "--root"; "."; "--mode"; mode ]
    let model, _ = Cli.init argv
    let model, _ = Cli.update (SnapshotLoaded(Ok { snapshot with Handoffs = handoffs; DefaultProfile = declared })) model
    let _, effects = Cli.update (HostCompleted(hostModel, emptyBudget)) model

    effects
    |> List.tryPick (function
        | WriteOutput(_, result) -> Some result.Exit
        | _ -> None)
    |> Option.defaultWith (fun () -> failwith "no WriteOutput effect")

[<Tests>]
let tests =
    testList
        "ProfileAware handoff gate"
        [
          // ── US1: strict tightens the boundary (Acceptance 1 & 3; contract matrix strict rows) ──
          test "ProfileAware strict + failing @ gate ⇒ GovernedBlocking (exit 2)" {
              Expect.equal
                  (routeExit "gate" (profile "strict") [ failingHandoff ])
                  GovernedBlocking
                  "strict tightens the ship-maturity gate to the verify boundary ⇒ a failing handoff blocks"
          }

          test "ProfileAware strict + satisfied @ gate ⇒ Success (exit 0)" {
              Expect.equal
                  (routeExit "gate" (profile "strict") [ satisfiedHandoff ])
                  Success
                  "a satisfied handoff is Warn ⇒ withheld under every profile"
          }

          // ── US1: light relaxes the boundary (Acceptance 2; Invariant 1, 6) ──
          // The light + failing row is the FAILS-BEFORE evidence — it blocks against the profile-blind
          // shortcut and must flip to Success once the derivation honors the profile.
          test "ProfileAware light + failing @ gate ⇒ Success (exit 0, advisory)" {
              Expect.equal
                  (routeExit "gate" (profile "light") [ failingHandoff ])
                  Success
                  "light leaves the ship floor above the verify boundary ⇒ a failing handoff is advisory"
          }

          test "ProfileAware light + satisfied @ gate ⇒ Success (exit 0)" {
              Expect.equal
                  (routeExit "gate" (profile "light") [ satisfiedHandoff ])
                  Success
                  "light never inverts a satisfied handoff into a block"
          }

          // ── US1: many gates — block iff ANY derives Blocking (Invariant 4; Edge "Multiple handoffs") ──
          test "ProfileAware strict + [satisfied; failing] @ gate ⇒ GovernedBlocking (one still blocks)" {
              Expect.equal
                  (routeExit "gate" (profile "strict") [ satisfiedHandoff; failingHandoff ])
                  GovernedBlocking
                  "a withheld satisfied gate must not mask a failing gate that still blocks under strict"
          }

          test "ProfileAware light + [satisfied; failing] @ gate ⇒ Success (both advisory)" {
              Expect.equal
                  (routeExit "gate" (profile "light") [ satisfiedHandoff; failingHandoff ])
                  Success
                  "under light neither gate reaches the boundary ⇒ no gate derives Blocking"
          }

          // ── US2: absent profile fails safe to strict (Acceptance 1 & 2; Invariant 2; FR-004) ──
          test "ProfileAware absent profile + failing @ gate ⇒ GovernedBlocking (fail-safe to strict)" {
              Expect.equal
                  (routeExit "gate" None [ failingHandoff ])
                  GovernedBlocking
                  "no declared profile resolves to strict ⇒ a failing handoff still blocks (no green-by-omission)"
          }

          test "ProfileAware absent profile + satisfied @ gate ⇒ Success (exit 0)" {
              Expect.equal
                  (routeExit "gate" None [ satisfiedHandoff ])
                  Success
                  "a satisfied handoff passes even under the strict fail-safe default"
          }

          // ── US2: declared-but-unrecognized profile fails safe to strict (Edge; research D2; Invariant 2) ──
          // The ONLY row exercising recognizeProfile's not-recognized branch: a custom profile that
          // validates upstream (declared in `profiles:`) but is not an enforcement-recognized lever.
          test "ProfileAware unrecognized declared profile + failing @ gate ⇒ GovernedBlocking" {
              Expect.equal
                  (routeExit "gate" (profile "balanced") [ failingHandoff ])
                  GovernedBlocking
                  "an unrecognized-but-declared profile resolves to strict, never relaxing"
          }
        ]
