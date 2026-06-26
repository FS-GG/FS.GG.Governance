// Curated public signature contract for the EDGE interpreter of the template-provider seam (071).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The
// matching Interpreter.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings.
//
// This module is the IMPURE side of the Constitution's MVU boundary (Principle IV): it executes the
// `Loop.Effect`s the pure `update` requests, against INJECTED, FAKEABLE ports, and feeds each result
// back as a `Loop.Msg`. It is TOTAL and SAFE (FR-008, SC-005): every port `Error` and every thrown
// exception is caught and reified to the matching `Msg` — the interpreter NEVER throws and NEVER leaves
// a partial tree. The host-owned lifecycle skeleton is never written by this seam (it only ADDS the
// provider-emitted runtime files); the manifest itself is NOT written here (host concern, research D0).

namespace FS.GG.Governance.Scaffold

open FS.GG.Governance.Scaffold.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    /// The bundle of injected edge ports — everything impure the seam touches (contract C4). `Invoke`
    /// runs the resolved provider's `Emit`; `Probe` returns the target-relative subset of the given
    /// paths that ALREADY exist (so `update` can refuse the whole batch — FR-007); `Write` lays down all
    /// (relative-path, contents) pairs ATOMICALLY (temp + rename, all-or-nothing — SC-005); `Out` is the
    /// host's stdout injection point (the seam emits no summary itself — research D0). Wholly fakeable in
    /// tests (a fake in-proc provider, an in-memory probe/write recorder) so no real filesystem is
    /// reached for the pure-edge assertions; the real-filesystem tests use `realPorts` against a temp dir.
    type Ports =
        { Invoke: TemplateProvider -> ScaffoldRequest -> Result<ProviderEmission, ProviderError>
          Probe: string list -> Result<string list, string>
          Write: (string * string) list -> Result<unit, string>
          Out: string -> unit }

    /// Build the REAL ports for an operator-chosen `target` directory: invoke the provider in-process,
    /// `Probe` the filesystem for the existing subset of target-relative paths, `Write` every file
    /// atomically (temp + rename) under `target` after RE-CONFIRMING each resolved absolute path stays
    /// inside `target` (defence-in-depth — D5), and an `Out` to `Console.Out`. Reaches NO network, NO
    /// clock; writes ONLY the provider-emitted runtime files, never the manifest.
    val realPorts: target: string -> Ports

    /// Execute ONE `Loop.Effect` against the ports and return its result `Loop.Msg`. TOTAL and SAFE:
    /// catches every port `Error` and thrown exception, reifying it to the matching `Msg` (an invoke
    /// failure ⇒ `ProviderEmitted (Error _)`, a probe/write fault ⇒ `CollisionsProbed/FilesWritten
    /// (Error _)`). NEVER throws.
    val step: ports: Ports -> effect: Loop.Effect -> Loop.Msg

    /// The interpreter loop: `Loop.init` the request, thread each emitted `Effect` through `step`, feed
    /// every result `Msg` back into `Loop.update`, and stop at `Done`. Returns the terminal `Loop.Model`
    /// (carrying the folded `ScaffoldManifest`). TOTAL — never throws; the whole composition runs
    /// deterministically when `ports` are faked, and leaves no partial tree on any failure (SC-005).
    val run: ports: Ports -> request: Loop.RunRequest -> Loop.Model
