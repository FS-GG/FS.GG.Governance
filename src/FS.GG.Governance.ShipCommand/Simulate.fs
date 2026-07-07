// The PURE dry-run simulation core (112). Visibility lives in Simulate.fsi (Principle II) — this file
// carries NO top-level access modifiers. No I/O, no clock: `classify`/`assemble` are pure, total transitions
// over already-parsed values. It reuses the SddHandoff `Reader.parse`/`Consumer.consume` cores verbatim to
// read the handoff's declared evidence and its diagnostics; it never re-implements handoff parsing.

namespace FS.GG.Governance.ShipCommand

open FS.GG.Governance.Gates.Model              // Gate
open FS.GG.Governance.Ship.Model               // ShipDecision
open FS.GG.Governance.Adapters.SddHandoff       // Reader.parse, Consumer.consume, Model.*
open FS.GG.Governance.Adapters.SddHandoff.Model // DeclaredState, DeclaredNode, Diagnostic

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Simulate =

    type SignalClass =
        | RequiredSatisfied
        | RequiredAbsent
        | NotRequired

    type SignalSufficiency =
        { Signal: string
          Class: SignalClass }

    type Sufficiency =
        { Signals: SignalSufficiency list
          RequiredAbsentCount: int
          AllNotEvaluated: bool }

    type SimulatedResult =
        { Decision: ShipDecision
          Sufficiency: Sufficiency
          HandoffDiagnostics: Diagnostic list }

    // The sufficiency breakdown classifies the handoff's *declared* evidence — a preview heuristic over what the
    // producer wrote, NOT the kernel's taint-closed *effective* state. The effective state (e.g. a `Real` node
    // resting on a `Synthetic` one tainted to auto-synthetic) is what drives the actual verdict via
    // `Consumer.consume` → `Ship.rollup`, and that authoritative verdict is carried on `SimulatedResult.Decision`
    // (`verdict`/`blockers`). So the top-line verdict is never rosier than reality; only this supplementary
    // signal view reads the declared state, by design (see `.fsi` — "declared evidence signals").
    //
    // Stale evidence is never sufficient regardless of declared state (the Governance-owned freshness flag).
    // Otherwise: only `Real` is satisfied; the not-yet/failed states are absent gaps; deliberately deferred /
    // accepted / disclosed-synthetic states are NOT counted as gaps (they are declared choices, not omissions).
    let classify (state: DeclaredState) (stale: bool) : SignalClass =
        if stale then
            RequiredAbsent
        else
            match state with
            | Real -> RequiredSatisfied
            | Pending
            | Failed
            | Skipped -> RequiredAbsent
            | Synthetic
            | Deferred
            | AcceptedDeferral -> NotRequired

    let assemble
        (decision: ShipDecision)
        (selectedGates: Gate list)
        (reads: Reader.HandoffRead list)
        : SimulatedResult =
        // Parse every located read for its evidence signals; a read that refuses contributes no signals but its
        // diagnostic is surfaced (below, via Consumer.consume) so a malformed/version-mismatched handoff is not
        // silently dropped (FR-008).
        let parsed = reads |> List.map Reader.parse
        let handoffs = parsed |> List.choose (function | Ok h -> Some h | Error _ -> None)

        // Consumer.consume is the authoritative diagnostic source (parse + version + integrity + staleness).
        let consumed = Consumer.consume reads

        let signals =
            handoffs
            |> List.collect (fun h -> h.Evidence.Nodes)
            |> List.map (fun (n: DeclaredNode) ->
                { Signal = n.Id
                  Class = classify n.State n.Stale })

        let requiredAbsentCount =
            signals |> List.filter (fun s -> s.Class = RequiredAbsent) |> List.length

        let anySatisfied = signals |> List.exists (fun s -> s.Class = RequiredSatisfied)

        // The all-absent (Audio) failure mode: the change is gate-worthy (there ARE required gates) yet the
        // handoff carried no real, non-stale signal. Surfaced explicitly so it is never read as a clean Pass.
        let allNotEvaluated = not (List.isEmpty selectedGates) && not anySatisfied

        { Decision = decision
          Sufficiency =
            { Signals = signals
              RequiredAbsentCount = requiredAbsentCount
              AllNotEvaluated = allNotEvaluated }
          HandoffDiagnostics = consumed.Diagnostics }
