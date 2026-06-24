// Curated public signature contract for the EDGE interpreter of the `fsgg verify` host command (F056).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Interpreter.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings.
//
// This module is the IMPURE side of the Constitution's MVU boundary (Principle IV): it executes the
// `Loop.Effect`s the pure `update` requests, against INJECTED, FAKEABLE ports, and feeds each result back as
// a `Loop.Msg`. The `Ports` bundle is IDENTICAL to F026 `ShipCommand`'s: it REUSES the existing edges
// verbatim — `Config.Loader.FileReader` for catalog reads (F014), `Snapshot.Ports` for git sensing (F016),
// the F046 `FreshnessSensor`/`StoreReader`, and the F051 gate-execution `ExecutionPort` — plus the persistence
// edge (`ArtifactWriter`) and stdout edge (`OutputSink`). It is TOTAL and SAFE: every port `Error` and every
// thrown exception is caught and reified to the matching `Msg` — the interpreter NEVER throws and (via
// temp+rename) NEVER leaves a partial artifact, and a write failure is reified to a `ToolError` (never a
// blocked verdict). The only difference from `ShipCommand` is the document written (verify.json).

namespace FS.GG.Governance.VerifyCommand

open FS.GG.Governance.Config              // Loader.FileReader
open FS.GG.Governance.Snapshot            // Ports
open FS.GG.Governance.FreshnessSensing     // FreshnessSensor, StoreReader (F046)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    /// The injected PERSISTENCE port: write `content` to `path`, returning `Ok ()` or `Error reason` (an
    /// unwritable location is a value, never an exception). The real port writes via temp-file + atomic rename
    /// so a failed write never leaves a truncated `verify.json`; tests back it with an in-memory capturing
    /// writer.
    type ArtifactWriter = string -> string -> Result<unit, string>

    /// The injected STDOUT port: emit the rendered summary. The real port writes to `Console.Out`; tests
    /// capture the emitted string.
    type OutputSink = string -> unit

    /// The bundle of injected edge ports — everything impure the command touches (IDENTICAL to ShipCommand's).
    /// `Files`/`Git` are the REUSED F014/F016 ports; `Freshness`/`Store` the REUSED F046 sensing ports;
    /// `Execute` the REUSED F051 gate-execution port; `Write`/`Out` the persistence/stdout ports. Wholly faked
    /// in tests so no real `git` process or real filesystem is reached.
    type Ports =
        { Files: Loader.FileReader
          Git: FS.GG.Governance.Snapshot.Ports
          Freshness: FreshnessSensing.FreshnessSensor
          Store: FreshnessSensing.StoreReader
          Write: ArtifactWriter
          Out: OutputSink
          /// F052: the injected GATE-EXECUTION port — the only seam through which the command touches a gate
          /// process. `realPorts` wires the merged F051 `GateExecution.Interpreter.realPort`; tests inject a
          /// deterministic fake.
          Execute: FS.GG.Governance.GateExecution.Model.ExecutionPort }

    /// Build the REAL ports for a repository working directory: `Config.Loader.fileSystemReader repo`,
    /// `Snapshot.Interpreter.realPorts repo`, the F046 real sensor/store reader, the F051 real execution port,
    /// a temp+rename `ArtifactWriter`, and a `Console.Out` sink. This is the ONLY place the command touches the
    /// real filesystem for writing. Reaches NO network.
    val realPorts: repo: string -> Ports

    /// Execute ONE `Loop.Effect` against the ports and return its result `Loop.Msg`. TOTAL and SAFE: catches
    /// every port `Error` and thrown exception, reifying it to the matching `Msg`. NEVER throws.
    val step: ports: Ports -> effect: Loop.Effect -> Loop.Msg

    /// The interpreter loop: `Loop.init` the request, thread each emitted `Effect` through `step`, feed every
    /// result `Msg` back into `Loop.update`, and stop at `Done`. Returns the terminal `Loop.Model` (carrying
    /// the decided `ExitDecision` — including a `Blocked` verdict). TOTAL — never throws.
    val run: ports: Ports -> request: Loop.RunRequest -> Loop.Model
