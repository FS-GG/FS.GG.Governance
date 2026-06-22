// Curated public signature contract for the EDGE interpreter of the `fsgg ship` host command (F026).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching Interpreter.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings.
//
// This module is the IMPURE side of the Constitution's MVU boundary (Principle IV): it executes the
// `Loop.Effect`s the pure `update` requests, against INJECTED, FAKEABLE ports, and feeds each result
// back as a `Loop.Msg`. The `Ports` bundle is IDENTICAL to F022's (research D3): it REUSES the existing
// sensing edges verbatim — `Config.Loader.FileReader` for catalog reads (F014) and `Snapshot.Ports`
// for git sensing (F016) — and the persistence edge (`ArtifactWriter`) and stdout edge (`OutputSink`)
// F022 introduced. The ONLY difference from F022 at runtime is that one artifact is written (audit.json),
// not two. It is TOTAL and SAFE (FR-010, FR-013, SC-004): every port `Error` and every thrown exception
// is caught and reified to the matching `Msg` — the interpreter NEVER throws and NEVER leaves a partial
// artifact, and a write failure is reified to a `ToolError` (never a blocked verdict).

namespace FS.GG.Governance.ShipCommand

open FS.GG.Governance.Config              // Loader.FileReader
open FS.GG.Governance.Snapshot            // Ports
open FS.GG.Governance.FreshnessSensing     // FreshnessSensor, StoreReader (F046)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    /// The injected PERSISTENCE port: write `content` to `path`, returning `Ok ()` or `Error reason`
    /// (an unwritable location is a value, never an exception — FR-010). The real port writes via
    /// temp-file + atomic rename so a failed write never leaves a truncated `audit.json` (research D10);
    /// tests back it with an in-memory capturing writer.
    type ArtifactWriter = string -> string -> Result<unit, string>

    /// The injected STDOUT port: emit the rendered summary. The real port writes to `Console.Out`;
    /// tests capture the emitted string.
    type OutputSink = string -> unit

    /// The bundle of injected edge ports — everything impure the command touches (IDENTICAL to F022's).
    /// `Files`/`Git` are the REUSED F014/F016 ports (catalog reads and git sensing are already fakeable);
    /// `Write`/`Out` are the F022 persistence/stdout ports. Wholly faked in tests (in-memory reader, an
    /// in-memory git `Ports`, a capturing writer/sink) so no real `git` process or real filesystem is
    /// reached (FR-013, SC-007).
    type Ports =
        { Files: Loader.FileReader
          Git: FS.GG.Governance.Snapshot.Ports
          Freshness: FreshnessSensing.FreshnessSensor
          Store: FreshnessSensing.StoreReader
          Write: ArtifactWriter
          Out: OutputSink }

    /// Build the REAL ports for a repository working directory: `Config.Loader.fileSystemReader repo`,
    /// `Snapshot.Interpreter.realPorts repo`, a temp+rename `ArtifactWriter`, and a `Console.Out` sink.
    /// This is the ONLY place the command touches the real filesystem for writing; reading and git are
    /// delegated to the reused edges. Reaches NO network.
    val realPorts: repo: string -> Ports

    /// Execute ONE `Loop.Effect` against the ports and return its result `Loop.Msg`. TOTAL and SAFE:
    /// catches every port `Error` and thrown exception, reifying it to the matching `Msg` (a sensing
    /// failure ⇒ `Sensed (Error _)`, an invalid catalog ⇒ `Loaded (Invalid _)`, a write failure ⇒
    /// `Wrote (_, Error _)`). NEVER throws.
    val step: ports: Ports -> effect: Loop.Effect -> Loop.Msg

    /// The interpreter loop (mirrors `RouteCommand.Interpreter.run`): `Loop.init` the request, thread
    /// each emitted `Effect` through `step`, feed every result `Msg` back into `Loop.update`, and stop at
    /// `Done`. Returns the terminal `Loop.Model` (carrying the decided `ExitDecision` — including a
    /// `Blocked` verdict). TOTAL — never throws; the whole composition, the verdict, AND the exit-code
    /// mapping are exercised deterministically when `ports` are faked (SC-007).
    val run: ports: Ports -> request: Loop.RunRequest -> Loop.Model
