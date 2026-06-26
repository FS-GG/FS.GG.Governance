// Curated public signature contract for the EDGE interpreter of the `fsgg evidence` host command (069).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Interpreter.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings.
//
// This module is the IMPURE side of the Constitution's MVU boundary (Principle IV): it executes the
// `Loop.Effect`s the pure `update` requests, against INJECTED, FAKEABLE ports, and feeds each result back as a
// `Loop.Msg`. The `SenseReport` port REUSES the F12 project-sensing path VERBATIM — `Project.compose` /
// `Project.toLoopConfig` → the `Host` loop → `Host.Model<ProjectFact>` → `Project.evidenceReport` — to produce
// the already-folded `ProjectEvidenceReport`; the pure `Loop` then composes `Kernel.Evidence` itself to surface
// any graph failure by name (D3). It is TOTAL and SAFE: every port `Error` and thrown exception is caught and
// reified to the matching `Msg` — the interpreter NEVER throws and (via temp+rename) NEVER leaves a partial
// artifact. It maps an absent/unreadable repo input to `InputMissing` (⇒ exit 3) and an interpreter/host defect
// to `ToolFault` (⇒ exit 4), never a fabricated "all effective" document (Principle VI).

namespace FS.GG.Governance.EvidenceCommand

open FS.GG.Governance.Cli // ProjectEvidenceReport

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    /// The bundle of injected edge ports — everything impure the command touches. `SenseReport` runs the whole
    /// F12 sense → Host loop → `Project.evidenceReport` edge for a repository working directory, classifying any
    /// failure into `InputMissing`/`ToolFault`. `Write` persists atomically (temp+rename); `Out` is the stdout
    /// sink. Wholly faked in tests so no real git/filesystem is reached.
    type Ports =
        { SenseReport: string -> Result<ProjectEvidenceReport, Loop.ReportFault>
          Write: string -> string -> Result<unit, string>
          Out: string -> unit }

    /// Build the REAL ports for a repository working directory: the F12 project sensing (`Project.compose` +
    /// `Host.Loop` drive over real on-disk SpecKit/design artifacts) folded by `Project.evidenceReport`, a
    /// temp+rename atomic `Write`, and a `Console.Out` sink. Reaches NO network. It NEVER fabricates a fact.
    val realPorts: repo: string -> Ports

    /// Execute ONE `Loop.Effect` against the ports and return its result `Loop.Msg`. TOTAL and SAFE: catches
    /// every port `Error` and thrown exception, reifying it to the matching `Msg`. NEVER throws.
    val step: ports: Ports -> effect: Loop.Effect -> Loop.Msg

    /// The interpreter loop: `Loop.init` the request, thread each emitted `Effect` through `step`, feed every
    /// result `Msg` back into `Loop.update`, and stop at `Done`. Returns the terminal `Loop.Model` (carrying
    /// the decided `ExitDecision`). TOTAL — never throws.
    val run: ports: Ports -> request: Loop.RunRequest -> Loop.Model
