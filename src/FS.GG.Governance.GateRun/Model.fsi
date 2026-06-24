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

    /// How one selected gate was handled on this run (Key entity: "Gate disposition").
    ///   • `Executed`    — the gate declared a command and was run this run (it was `mustRecompute`, OR it was
    ///                     `reusable` but its prior outcome was not recoverable, so it was conservatively
    ///                     recomputed — FR-004).
    ///   • `Reused`      — the gate declared a command and a prior captured outcome was reused; it was NOT
    ///                     spawned this run (the cache payoff — FR-003).
    ///   • `NotExecuted` — the gate declared no command; it was not run and keeps its current rollup
    ///                     treatment (FR-005).
    type GateDisposition =
        | Executed
        | Reused
        | NotExecuted

    /// The per-gate execution result attached to `route.json` / `audit.json` (matched by `GateId`) and, in
    /// `fsgg ship`, fed to the verdict relocation (D3). `ExitCode`/`Passed` are `Some` for `Executed`
    /// (the real F051 outcome's exit, possibly a sentinel) and `Reused` (the `priorExitOf` recovery), and
    /// `None` only for `NotExecuted`. `Passed` is `Some true` iff the exit code is success (`0`) — a non-zero
    /// or sentinel exit is a fail (FR-006).
    type GateOutcome =
        { GateId: GateId
          Disposition: GateDisposition
          ExitCode: ExitCode option
          Passed: bool option }
