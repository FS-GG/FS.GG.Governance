// Curated public signature contract for the EDGE interpreter of the `fsgg release` host command (F055).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching Interpreter.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — the
// atomic writer, the declaration-read glue, and the exception guard live ONLY in the .fs.
//
// This module is the IMPURE side of the Constitution's MVU boundary (Principle IV): it executes the
// `Loop.Effect`s the pure `update` requests against INJECTED, FAKEABLE ports, and feeds each result back
// as a `Loop.Msg`. It REUSES the existing edges verbatim — `Config.Loader.FileReader` for the
// `.fsgg/release.yml` read (F014) and F054 `senseRelease`/`realPort` for the repository sensing (the
// `Sense` port) — adding only the persistence (`ArtifactWriter`) and stdout (`OutputSink`) ports. It is
// TOTAL and SAFE: every port `Error` and every thrown exception is caught and reified to the matching
// `Msg` — the interpreter NEVER throws and (via temp+rename) NEVER leaves a partial artifact, and a write
// failure is reified to a `Wrote(Error)` (mapped by `update` to `ToolError`, never a blocked verdict).
// NETWORK-FREE by construction: the sensing port reaches only local files via `System.IO` (F054, SC-008).

namespace FS.GG.Governance.ReleaseCommand

open FS.GG.Governance.Config                       // Loader.FileReader
open FS.GG.Governance.ReleaseFactsSensing.Model     // SourceLayout, ReleaseExpectations, SensedRelease

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    /// The injected PERSISTENCE port: write `content` to `path`, returning `Ok ()` or `Error reason` (an
    /// unwritable location is a value, never an exception). The real port writes via temp-file + atomic
    /// rename so a failed write never leaves a truncated `release.json` (FR-012); tests back it with an
    /// in-memory capturing or faulting writer.
    type ArtifactWriter = string -> string -> Result<unit, string>

    /// The injected STDOUT port: emit the rendered summary. The real port writes to `Console.Out`; tests
    /// capture the emitted string.
    type OutputSink = string -> unit

    /// The bundle of injected edge ports — everything impure the command touches. `Files` is the REUSED
    /// F014 read port (bound to the repo's `.fsgg`); `Sense` wraps F054 `realPort`+`senseRelease` (the
    /// real F053/F054 cores, NEVER mocked in end-to-end tests); `Write`/`Out` are the persistence/stdout
    /// ports faked in unit tests.
    type Ports =
        { Files: Loader.FileReader
          Sense: SourceLayout -> ReleaseExpectations -> SensedRelease
          Write: ArtifactWriter
          Out: OutputSink }

    /// Build the REAL ports for a repository working directory: `Config.Loader.fileSystemReader repo`
    /// (reads `<repo>/.fsgg/release.yml`), a `Sense` that builds `ReleaseFactsSensing.Interpreter.realPort
    /// repo layout` and runs `senseRelease`, a temp+rename `ArtifactWriter`, and a `Console.Out` sink.
    /// This is the ONLY place the command touches the real filesystem for writing; reading and sensing are
    /// delegated to the reused edges. Reaches NO network.
    val realPorts: repo: string -> Ports

    /// Execute ONE `Loop.Effect` against the ports and return its result `Loop.Msg`. TOTAL and SAFE:
    /// catches every port `Error` and thrown exception, reifying it to the matching `Msg` (an absent /
    /// unreadable / malformed declaration ⇒ `DeclarationLoaded(Error _)`, a write failure ⇒
    /// `Wrote(Error _)`). NEVER throws.
    val step: ports: Ports -> effect: Loop.Effect -> Loop.Msg

    /// The interpreter loop (mirrors `ShipCommand.Interpreter.run`): `Loop.init` the request, thread each
    /// emitted `Effect` through `step`, feed every result `Msg` back into `Loop.update`, and stop at
    /// `Done`. Returns the terminal `Loop.Model` (carrying the decided `ExitDecision` — including a
    /// `Blocked` verdict). TOTAL — never throws; the whole composition, the verdict, AND the exit-code
    /// mapping are exercised deterministically against a real temp-repo fixture (Principle V, SC-007).
    val run: ports: Ports -> request: Loop.RunRequest -> Loop.Model
