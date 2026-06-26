// Curated public signature contract for the EDGE interpreter of the `fsgg release` host command (F055,
// grown by 065 F26 host wiring). This .fsi is the SOLE declaration of the module's public surface
// (Constitution Principle II). The matching Interpreter.fs carries NO access modifiers on top-level
// bindings — the atomic writer, the pack-output reader, the head sense, and the exception guard live ONLY
// in the .fs.
//
// This module is the IMPURE side of the Constitution's MVU boundary (Principle IV): it executes the
// `Loop.Effect`s the pure `update` requests against INJECTED, FAKEABLE ports, and feeds each result back as
// a `Loop.Msg`. 065 adds the F51 execution port (`Execute`), a pack-output reader (`PackRead`), and the
// normalized head/environment/builder senses the release attestation needs — every new edge here, with NO
// I/O entering any pure core (FR-010). It is TOTAL and SAFE: every port `Error` and thrown write exception
// is caught and reified to the matching `Msg`; the interpreter NEVER throws and (via temp+rename) NEVER
// leaves a partial artifact.

namespace FS.GG.Governance.ReleaseCommand

open FS.GG.Governance.Config                       // Loader.FileReader
open FS.GG.Governance.Config.Model                  // SurfaceId, EnvironmentClass
open FS.GG.Governance.FreshnessKey.Model            // Revision
open FS.GG.Governance.Provenance.Model              // BuilderIdentity
open FS.GG.Governance.GateExecution.Model           // ExecutionPort
open FS.GG.Governance.CommandKind.Model             // KindedCommandRun
open FS.GG.Governance.PackEvidence.Model            // PackOutcome
open FS.GG.Governance.ReleaseFactsSensing.Model      // SourceLayout, ReleaseExpectations, SensedRelease

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    /// The injected PERSISTENCE port (temp-file + atomic rename so a failed write never leaves a truncated
    /// file). Tests back it with an in-memory capturing or faulting writer.
    type ArtifactWriter = string -> string -> Result<unit, string>

    /// The injected STDOUT port.
    type OutputSink = string -> unit

    /// The bundle of injected edge ports. `Files`/`Sense`/`Write`/`Out` are the existing F055 ports; 065
    /// adds the F51 `Execute` port, the `PackRead` pack-output reader (it reads the produced `.nupkg`'s
    /// normalized path/version/digest from the recorded `Pack` run and classifies `Packed` /
    /// `PackedNoArtifact` / `PackFailed` — a non-zero exit ⇒ `PackFailed`, an unreadable artifact ⇒
    /// `PackedNoArtifact (ArtifactUnreadable …)`, never a throw), and the three normalized provenance senses.
    type Ports =
        { Files: Loader.FileReader
          Sense: SourceLayout -> ReleaseExpectations -> SensedRelease
          Execute: ExecutionPort
          PackRead: SurfaceId -> KindedCommandRun -> PackOutcome
          SenseHead: unit -> Revision
          SenseEnvironment: unit -> EnvironmentClass
          SenseBuilder: unit -> BuilderIdentity
          Write: ArtifactWriter
          Out: OutputSink }

    /// Build the REAL ports for a repository working directory: the reused F014 reader + F054 sense + atomic
    /// writer + console sink, PLUS the real F51 execution port, a real pack-output reader (locating the
    /// produced `.nupkg` under the constitution's pack-output dir and computing its `ArtifactHash`), the
    /// F016 head-revision sense, and the normalized environment/builder senses (no username/host/clock).
    val realPorts: repo: string -> Ports

    /// Execute ONE `Loop.Effect` against the ports and return its result `Loop.Msg`. TOTAL and SAFE.
    val step: ports: Ports -> effect: Loop.Effect -> Loop.Msg

    /// The interpreter loop: `Loop.init` the request, thread each emitted `Effect` through `step`, feed every
    /// result `Msg` back into `Loop.update`, and stop at `Done`. Returns the terminal `Loop.Model`.
    val run: ports: Ports -> request: Loop.RunRequest -> Loop.Model
