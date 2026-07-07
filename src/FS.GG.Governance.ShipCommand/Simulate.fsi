// Curated public signature contract for the PURE dry-run simulation core (112, US1/US2).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II).
// The matching Simulate.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings —
// visibility is presence/absence here.
//
// Design-first artifact (Principle I): the types the `fsgg ship --dry-run` preview produces. This module
// is PURE (no I/O, no clock): given the rolled `ShipDecision`, the selected gates, and the located handoff
// reads, it classifies the handoff's declared evidence signals into a SUFFICIENCY breakdown and assembles
// the whole simulated result. It answers "is the handoff even sufficient?" WITHOUT executing any gate — the
// verdict is the pre-execution rollup (every command-gate is `notEvaluated` in a dry run), so an all-absent
// handoff is surfaced as such rather than as a clean Pass (spec FR-011).

namespace FS.GG.Governance.ShipCommand

open FS.GG.Governance.Gates.Model              // Gate (the selected gates — what the change requires)
open FS.GG.Governance.Ship.Model               // ShipDecision (the reused verdict core)
open FS.GG.Governance.Adapters.SddHandoff       // Reader.HandoffRead, Model.Diagnostic

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Simulate =

    /// How a declared handoff evidence signal stands against what a real gate would need. `RequiredSatisfied`
    /// = real, non-stale evidence is carried; `RequiredAbsent` = the signal is needed but missing / not real /
    /// stale (the would-be-`notEvaluated` gap — the FS.GG.Audio failure mode); `NotRequired` = deliberately
    /// deferred / accepted / disclosed-synthetic, i.e. not counted as a gap.
    type SignalClass =
        | RequiredSatisfied
        | RequiredAbsent
        | NotRequired

    /// One classified evidence signal (a declared node id + its class).
    type SignalSufficiency =
        { Signal: string
          Class: SignalClass }

    /// The handoff-sufficiency breakdown (US2). `RequiredAbsentCount` > 0 ⇒ the handoff is insufficient;
    /// `AllNotEvaluated` = true ⇒ the change is gate-worthy yet NOTHING real was carried (the exact
    /// all-`notEvaluated` state the dry run must surface rather than hide behind an empty blocker list).
    type Sufficiency =
        { Signals: SignalSufficiency list
          RequiredAbsentCount: int
          AllNotEvaluated: bool }

    /// The whole simulated dry-run result. `Decision` is the reused `Ship.rollup` verdict (no gate executed);
    /// `Sufficiency` names the required-but-absent signals; `HandoffDiagnostics` carries the parse / version /
    /// staleness diagnostics so a malformed or version-mismatched handoff can never read as a bare Pass (FR-008).
    type SimulatedResult =
        { Decision: ShipDecision
          Sufficiency: Sufficiency
          HandoffDiagnostics: Model.Diagnostic list }

    /// Classify one declared evidence node's state+staleness into a `SignalClass`. PURE and TOTAL.
    val classify: state: Model.DeclaredState -> stale: bool -> SignalClass

    /// Assemble the simulated result from the rolled decision, the selected (required) gates, and the located
    /// handoff reads. PURE and TOTAL — never throws. Parses each read for its evidence signals, folds in the
    /// consumer diagnostics, and computes the sufficiency breakdown. `selectedGates` drives `AllNotEvaluated`
    /// (a change with required gates but no satisfied signal is the all-absent case).
    val assemble:
        decision: ShipDecision ->
        selectedGates: Gate list ->
        reads: Reader.HandoffRead list ->
            SimulatedResult
