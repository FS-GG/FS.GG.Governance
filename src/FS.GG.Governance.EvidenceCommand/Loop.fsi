// Curated public signature contract for the PURE MVU core of the `fsgg evidence` host command (069).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Loop.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility is presence/
// absence here; every codec/render helper stays unexposed by absence from this signature.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Loop.fs body exists
// (Principle I). This module is the PURE side of the Constitution's MVU boundary (Principle IV):
// `parse`/`init`/`toDocument`/`update`/`render`/`exitCode` perform NO I/O, NO git, NO clock — the whole
// sense → build-closure → project → persist → summarize → exit composition is a pure transition over
// `Model` + `Msg`, emitting `Effect` data the edge `Interpreter` executes. It REUSES `Kernel.Evidence`
// (`build`/`effective`) at its OWN edge to recover the `GraphError` that `Project.evidenceReport` swallows to
// `Map.empty` (D3/FR-004), WITHOUT modifying `Project.evidenceReport`. Effective evidence is INFORMATION, not
// a verdict: the host assigns no severity, profile, mode, enforcement, or ship verdict — its exit code is
// OPERATIONAL ONLY (FR-007).

namespace FS.GG.Governance.EvidenceCommand

open FS.GG.Governance.Cli // ProjectEvidenceReport
open FS.GG.Governance.EvidenceJson // EvidenceDocument

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Loop =

    /// Summary output format (the host affordance). `--format json` selects `Json` (the contracted
    /// `evidence.json` bytes on stdout); default `Human`. `--plain` is an additive ANSI-free signal that
    /// composes with `--format` WITHOUT changing it (the `Json` branch still wins; M-CLI-7). The JSON document
    /// is the contracted artifact and is ALWAYS written regardless of format. The human view exposes no field
    /// the JSON document lacks and carries no verdict / exit-code / timestamp / path.
    type OutputFormat =
        | Human
        | Json

    /// The normalized invocation. Defaults applied in `parse`: `Repo = "."`,
    /// `Out = <repo>/readiness/evidence.json`, `Format = Human`, `ExplicitPlain = false`.
    type RunRequest =
        { Repo: string
          Out: string
          Format: OutputFormat
          ExplicitPlain: bool }

    /// Pure-parser rejections — each maps to `UsageError'`/exit 2.
    type UsageError =
        | UnknownFlag of string
        | MissingValue of flag: string
        | BadFormat of value: string

    /// The process-level outcome category. Deliberately carries NO ship/merge verdict (FR-007): emitting
    /// `evidence.json` is INFORMATION. `Success` 0, `UsageError'` 2, `InputUnavailable` 3, `ToolError` 4.
    type ExitDecision =
        | Success
        | UsageError'
        | InputUnavailable
        | ToolError

    /// Why the report sensing failed — distinguishes an absent/unreadable input (⇒ `InputUnavailable`, 3)
    /// from an interpreter/tool defect (⇒ `ToolError`, 4), per Principle VI; NEVER a fabricated document.
    type ReportFault =
        | InputMissing of reason: string
        | ToolFault of reason: string

    /// The I/O the pure `update` REQUESTS but never performs (Principle IV). The edge `Interpreter` executes
    /// each and feeds the result back as a `Msg`. `SenseReport` runs the F12 project-sensing path + the Host
    /// loop + `Project.evidenceReport` at the edge; the pure `update` then composes `Kernel.Evidence` itself.
    type Effect =
        | SenseReport of repo: string
        | WriteArtifact of path: string * content: string
        | EmitSummary of text: string

    /// External results the interpreter feeds back into `update`.
    type Msg =
        | Begin
        | Reported of Result<ProjectEvidenceReport, ReportFault>
        | Wrote of Result<unit, string>
        | Emitted

    /// A host-edge diagnostic — actionable text carrying NO clock, machine-absolute path, or environment
    /// value. Distinct from the F014 catalog `Diagnostic`.
    type Diagnostic =
        { Category: ExitDecision
          Message: string }

    /// How far the pipeline has progressed.
    type Phase =
        | Parsed
        | Sensed
        | Projected
        | Persisted
        | Done

    /// The durable state the workflow owns. `Document` is the typed projection value and `Doc` its rendered
    /// `evidence.json` string, BOTH computed (in pure `update`) before the write effect is emitted.
    type Model =
        { Request: RunRequest
          Phase: Phase
          Report: ProjectEvidenceReport option
          Document: EvidenceDocument option
          Doc: string option
          Diagnostics: Diagnostic list
          Exit: ExitDecision }

    /// Parse argv into a normalized request. PURE and TOTAL — usage problems are `UsageError` values, never
    /// exceptions. Tolerates a leading `evidence` verb. `--format` other than `human`/`json` ⇒ `BadFormat`.
    val parse: argv: string list -> Result<RunRequest, UsageError>

    /// Initial state plus the first requested effect — `SenseReport request.Repo`.
    val init: request: RunRequest -> Model * Effect list

    /// The pure host mapping `ProjectEvidenceReport -> EvidenceDocument` (data-model §Host mapping). Re-runs
    /// `Kernel.Evidence.build` over the report's declared nodes + dependencies: `Error e ⇒ Content = Malformed
    /// e` (NO per-node map, FR-004); `Ok graph ⇒ Content = WellFormed` with each node's `Declared` from the
    /// report, `Effective` from `Evidence.effective`, MVP `Freshness` (`Some Fresh → Fresh`, else `Unknown` —
    /// never a guessed cause, D4/INV-6), and `Source` carried through. Disclosures are carried as rendered
    /// `(rule, justification)` pairs. PURE and TOTAL — no I/O, never throws.
    val toDocument: report: ProjectEvidenceReport -> EvidenceDocument

    /// The pure transition. `Reported (Ok report)` runs `toDocument` → `EvidenceJson.ofReport` and emits the
    /// write effect; `Wrote (Ok ())` emits the summary; `Emitted` ⇒ `Done`/`Success`. A sensing/write failure
    /// short-circuits to `Done` with the mapped operational `ExitDecision` and NO further effects (no partial
    /// artifact). PURE and TOTAL — no I/O, never throws.
    val update: msg: Msg -> model: Model -> Model * Effect list

    /// The deterministic summary — `Json` ⇒ the contracted `evidence.json` bytes; `Human` ⇒ a plain digest of
    /// the projected document. PURE: no clock/abs-path/env, byte-stable for a fixed `Model`.
    val render: model: Model -> format: OutputFormat -> string

    /// Map the decided outcome to a numeric process exit code: `Success` 0, `UsageError'` 2,
    /// `InputUnavailable` 3, `ToolError` 4. No ship/merge code (FR-007).
    val exitCode: decision: ExitDecision -> int
