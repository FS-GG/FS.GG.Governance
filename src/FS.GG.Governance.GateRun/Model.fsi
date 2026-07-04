// Curated public signature contract for the gate-run vocabulary (F052).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Model.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility is
// presence/absence here.
//
// Design-first artifact: drafted and committed BEFORE any Model.fs body exists (Principle I) and exercised in
// FSI (scripts/prelude.fsx). These two types are the host-side vocabulary the wiring needs and no merged core
// supplies: how one selected gate was handled this run, and the pass/fail-bearing result attached to it. All
// referenced types REUSE the F018/F032 vocabulary VERBATIM — never redefined (FR-015).

namespace FS.GG.Governance.GateRun

open FS.GG.Governance.Gates.Model            // GateId
open FS.GG.Governance.CommandRecord.Model     // ExitCode

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// How one selected gate was handled on this run (Key entity: "Gate disposition"), WITH the exit code and
    /// pass/fail carried INSIDE the two cases that have one. Because the exit + pass/fail live on the case (not
    /// as separate `option` fields on `GateOutcome`), an `Executed`/`Reused` gate WITHOUT an exit code is
    /// UNREPRESENTABLE, and a `NotExecuted` gate can never carry a spurious exit (111/B4). `passed` is `true`
    /// iff the exit code is success (`0`) — a non-zero or sentinel exit is a fail (FR-006).
    ///   • `Executed(exitCode, passed)` — the gate declared a command and was run this run (it was
    ///                     `mustRecompute`, OR it was `reusable` but its prior outcome was not recoverable, so
    ///                     it was conservatively recomputed — FR-004). `exitCode` is the real F051 outcome's
    ///                     exit, possibly a sentinel.
    ///   • `Reused(exitCode, passed)`   — the gate declared a command and a prior captured outcome was reused;
    ///                     it was NOT spawned this run (the cache payoff — FR-003). `exitCode` is the
    ///                     `priorExitOf` recovery.
    ///   • `NotExecuted`                — the gate declared no command; it was not run and keeps its current
    ///                     rollup treatment (FR-005). No exit code, never in the passed set.
    type GateDisposition =
        | Executed of exitCode: ExitCode * passed: bool
        | Reused of exitCode: ExitCode * passed: bool
        | NotExecuted

    /// The per-gate execution result attached to `route.json` / `audit.json` (matched by `GateId`) and, in
    /// `fsgg ship`, fed to the verdict relocation (D3). The exit code + pass/fail live on `Disposition` (they
    /// are only meaningful when the gate ran or reused a run), so no illegal exit-less-Executed state exists.
    type GateOutcome =
        { GateId: GateId
          Disposition: GateDisposition }

    /// `true` iff the gate ran (or reused a prior run) and its exit code was a pass; a `NotExecuted` gate is
    /// never passing — it is structurally excluded from the passed set (SC-002), never coerced to pass.
    val isPassing: disposition: GateDisposition -> bool
