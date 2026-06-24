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
        | Executed
        | Reused
        | NotExecuted

    type GateOutcome =
        { GateId: GateId
          Disposition: GateDisposition
          ExitCode: ExitCode option
          Passed: bool option }
