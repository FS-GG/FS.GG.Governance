// The gate-run domain vocabulary (F052). Visibility lives in Model.fsi (Principle II) — this file carries
// NO `private`/`internal`/`public` modifiers on top-level bindings. These two types are DATA, not behavior:
// how one selected gate was handled this run, and the pass/fail-bearing result attached to it. Every
// referenced type REUSES the F018/F032 vocabulary VERBATIM — never redefined (FR-015).

namespace FS.GG.Governance.GateRun

open FS.GG.Governance.Gates.Model            // GateId
open FS.GG.Governance.CommandRecord.Model     // ExitCode

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type GateDisposition =
        | Executed of exitCode: ExitCode * passed: bool
        | Reused of exitCode: ExitCode * passed: bool
        | NotExecuted

    type GateOutcome =
        { GateId: GateId
          Disposition: GateDisposition }

    let isPassing (disposition: GateDisposition) : bool =
        match disposition with
        | Executed(_, passed)
        | Reused(_, passed) -> passed
        | NotExecuted -> false
