// Curated public signature contract for the PURE MVU core of the template-provider seam (071).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II).
// The matching Loop.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings —
// visibility is presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Loop.fs body
// exists (Principle I). This module is the PURE side of the Constitution's MVU boundary (Principle IV):
// `init`/`update` perform NO I/O, NO clock, NO git — the whole
// version-check → invoke → boundary-check → probe → write → record composition is a pure transition
// over `Model` + `Msg`, emitting `Effect` data the edge `Interpreter` executes. Every match is
// exhaustive and wildcard-free (data-model §7). The core hardcodes NO provider name, package id,
// target name, toolchain, or layout (FR-003).

namespace FS.GG.Governance.Scaffold

open FS.GG.Governance.Scaffold.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Loop =

    /// The bundled run input the edge `Interpreter.run` takes: the bounded request plus the OPTIONAL
    /// already-resolved provider (resolution/discovery is a deferred host concern — research D0/D1).
    /// `run` destructures it into `init`'s two arguments.
    type RunRequest =
        { Request: ScaffoldRequest
          Provider: TemplateProvider option }

    /// The I/O the pure `update` REQUESTS but never performs (Principle IV). The edge `Interpreter`
    /// executes each and feeds the result back as a `Msg`. None of these is decided here-and-performed;
    /// the decision (whether to probe, whether to write) lives in `update`, the execution at the edge.
    type Effect =
        | InvokeProvider of provider: TemplateProvider * request: ScaffoldRequest
        | ProbeCollisions of paths: string list
        | WriteAll of files: (string * string) list

    /// External results the interpreter feeds back into `update`. `CollisionsProbed` carries the
    /// target-relative subset that already exists (or a probe fault); `FilesWritten` the all-or-nothing
    /// write result. Each `Error` reifies a recoverable refusal — `update` never throws.
    type Msg =
        | ProviderEmitted of Result<ProviderEmission, ProviderError>
        | CollisionsProbed of Result<string list, string>
        | FilesWritten of Result<unit, string>

    /// How far the pure transition has progressed. `init` for the no-provider / incompatible cases
    /// terminates directly at `Done`; a compatible provider advances Invoking → Probing → Writing → Done.
    type Phase =
        | Invoking
        | Probing
        | Writing
        | Done

    /// The durable state the workflow owns. `Provider` is the resolved provider (None ⇒ no-op);
    /// `Emission` is the captured provider description (set after a successful invoke, used to build the
    /// write batch + the manifest's generated list); `Manifest` is `None` until a terminal transition
    /// folds the deterministic provenance record (data-model §6/§7).
    type Model =
        { Request: ScaffoldRequest
          Provider: TemplateProvider option
          Phase: Phase
          Emission: ProviderEmission option
          Manifest: ScaffoldManifest option }

    /// Initial state plus the first requested effect(s) (Principle IV `init`). `None` ⇒ a terminal
    /// `Done(NoProvider)` with ZERO effects and a folded no-provider manifest (FR-002). A provider whose
    /// declared contract version is incompatible ⇒ a terminal `Done(Refused (ContractMismatch …))` with
    /// ZERO effects, BEFORE any invocation (FR-009, contract C2). A compatible provider ⇒ Phase
    /// `Invoking` and the single `InvokeProvider` effect.
    val init: request: ScaffoldRequest -> provider: TemplateProvider option -> Model * Effect list

    /// The pure transition that IS the whole seam (FR-004): on a provider emission it runs the
    /// path-boundary check over every `RelativePath` (relative, no escaping `..`, not rooted — D5) then
    /// emits `ProbeCollisions` over the resolved paths ∪ reserved; on an empty collision set it emits
    /// `WriteAll`; on a write `Ok` it folds the terminal `Scaffolded` manifest (Generated ascending,
    /// each `ProviderOwned`). Every failure mode — unresolvable/emit-failed provider, out-of-target,
    /// collision, probe/write fault — short-circuits to `Done` with the mapped, provider-attributed
    /// refusal and NO further effects (FR-007/FR-008/FR-009, SC-005). TOTAL — never throws, performs no
    /// I/O.
    val update: msg: Msg -> model: Model -> Model * Effect list
