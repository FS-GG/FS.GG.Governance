// Curated public signature contract for the EDGE interpreter of the `fsgg refresh` host command (F057).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching Interpreter.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — the
// per-source digester, the generator runner, the provenance-lock reader/writer, the atomic writer, and the
// exception guards live ONLY in the .fs.
//
// This module is the IMPURE side of the Constitution's MVU boundary (Principle IV): it executes the
// `Loop.Effect`s the pure `update` requests against INJECTED, FAKEABLE ports, and feeds each result back as
// a `Loop.Msg`. It REUSES the existing edges verbatim — `Config.Loader.FileReader` for the
// `.fsgg/refresh.yml` read (F014) and the F051 `GateExecution.Interpreter.realPort` process port for the
// generator runs — adding only the per-source SHA-256 digester (`Sense`), the recorded-provenance
// reader/writer (`ReadProv`/`WriteProv`), the atomic artifact writer (`Write`), and a stdout sink (`Out`).
// It is TOTAL and SAFE: every port `Error` and thrown exception is caught and reified to the matching
// `Msg` — the interpreter NEVER throws and (via temp+rename) NEVER leaves a partial artifact. NETWORK-FREE
// by construction: every port reaches only local files via `System.IO` and the F051 process port (SC-007).

namespace FS.GG.Governance.RefreshCommand

open FS.GG.Governance.Config                       // Loader.FileReader
open FS.GG.Governance.FreshnessKey.Model             // ArtifactHash, GeneratorVersion
open FS.GG.Governance.RefreshJson.RefreshModel       // GenerationEntry

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    /// The bundle of injected edge ports — everything impure the command touches. `Files` is the REUSED
    /// F014 read port (bound to the repo's `.fsgg`); `Sense` digests a view's declared source(s) + senses
    /// its generator version; `ReadProv` reads a view's recorded provenance from the generated lock;
    /// `Generate` runs the view's declared generator (the F051 process port) and returns the output digest;
    /// `WriteProv`/`Write` are the atomic temp-then-rename writers (lock / refresh.json); `Out` is the
    /// stdout sink. Every port is fakeable; unit tests inject capturing/faulting fakes, the end-to-end test
    /// uses `realPorts` against a real temp repo with a real deterministic generator command.
    type Ports =
        { Files: Loader.FileReader
          Sense: GenerationEntry -> Result<ArtifactHash list * GeneratorVersion, string>
          ReadProv: string -> (ArtifactHash list * GeneratorVersion) option
          Generate: GenerationEntry -> Result<ArtifactHash, string>
          WriteProv: string -> (ArtifactHash list * GeneratorVersion * ArtifactHash) -> Result<unit, string>
          Write: string -> string -> Result<unit, string>
          Out: string -> unit }

    /// Build the REAL ports for a repository working directory: `Config.Loader.fileSystemReader repo` (reads
    /// `<repo>/.fsgg/refresh.yml`), a `Sense` that SHA-256-digests each declared source (file or directory)
    /// and takes the generator version from the entry's `GeneratorBasis`, a `ReadProv`/`WriteProv` over the
    /// generated `<repo>/.fsgg/refresh.lock.json` (atomic, deterministic), a `Generate` that runs the entry's
    /// declared generator through the F051 process port and re-digests the produced output, a temp+rename
    /// `Write`, and a `Console.Out` sink. This is the ONLY place the command starts a process or touches the
    /// real filesystem. Reaches NO network.
    val realPorts: repo: string -> Ports

    /// Execute ONE `Loop.Effect` against the ports and return its result `Loop.Msg`. TOTAL and SAFE: catches
    /// every port `Error` and thrown exception, reifying it to the matching `Msg` (an absent/unreadable/
    /// malformed manifest ⇒ `ManifestLoaded(Error _)`, a sense failure ⇒ `Sensed(_, Error _)`, a generator
    /// failure ⇒ `Regenerated'(_, Error _)`, a write failure ⇒ `ProvenanceWritten/Wrote(Error _)`). NEVER
    /// throws.
    val step: ports: Ports -> effect: Loop.Effect -> Loop.Msg

    /// The interpreter loop (mirrors `ReleaseCommand.Interpreter.run`): `Loop.init` the request, thread each
    /// emitted `Effect` through `step`, feed every result `Msg` back into `Loop.update`, and stop at `Done`.
    /// Returns the terminal `Loop.Model` (carrying the decided `RefreshOutcome` and per-view decisions).
    /// TOTAL — never throws; the whole composition is exercised deterministically against a real temp-repo
    /// fixture (Principle V).
    val run: ports: Ports -> request: Loop.RunRequest -> Loop.Model
