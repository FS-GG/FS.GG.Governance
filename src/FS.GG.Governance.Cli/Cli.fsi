// Curated public signature contract for the optional F12 command-line tool.
//
// This .fsi is the SOLE declaration of the CLI public surface (Constitution Principle II).
// The matching Cli.fs carries no top-level access modifiers. Program.fs is a thin edge:
// argv -> Cli.run -> stdout/stderr/report file -> process exit.
//
// The CLI is the user/CI boundary around the existing governance system. It does not
// evaluate rules itself: Project builds the F09/F10/F11 composition root and Host.Loop runs
// the governance MVU. This module owns parsing, command normalization, review-budget
// gating, output envelopes, and exit decisions.

namespace FS.GG.Governance.Cli

open FS.GG.Governance.Kernel
open FS.GG.Governance.Host

// 100 (M-ARCH-2): CommandKind/OutputFormat/ReviewBudget/RunRequest are declared in the ProjectSensing
// library (Request.fsi, same FS.GG.Governance.Cli namespace) so the ArtifactReading sensing edge can be
// reused by EvidenceCommand without referencing this exe. They remain part of this namespace's surface
// and every signature below still refers to them unqualified.

/// A parse/usage error. These map to exit code 64.
type ParseError =
    | MissingCommand
    | UnknownCommand of string
    | UnknownOption of string
    /// A stray positional token (does not start with `--`). Distinct from `UnknownOption`
    /// so the error names the actual problem — an unexpected argument, not a bad flag.
    | UnexpectedArgument of string
    | MissingOptionValue of string
    | InvalidMode of string
    | InvalidFormat of string
    | InvalidReviewBudget of string
    /// Syntactically invalid root value. Missing or unreadable filesystem roots are
    /// snapshot/input failures and map to `InputUnavailable`, not `UsageError`.
    | InvalidRoot of string

/// The process-level outcome category.
type ExitDecision =
    | Success
    | GovernedBlocking
    | UsageError of ParseError list
    | InputUnavailable of reason: string
    | ToolError of reason: string

/// Review-budget and cache accounting for one command run.
type BudgetState =
    { Requested: string list
      CacheHits: string list
      CacheMisses: string list
      FreshDispatches: string list
      Pending: string list
      BudgetExhausted: string list }

/// Command-specific payload produced after the host run. The `route` payload carries the
/// computed F07 `Route` AND the SDD→Governance handoff gates consumed from the snapshot
/// (`Adapters.SddHandoff.Consumer`) — the latter drive the `GovernedBlocking` exit at
/// `--mode gate` and are rendered for attribution (empty when no handoff is present).
type CommandPayload =
    | RoutePayload of route: Route * handoffGates: FS.GG.Governance.Gates.Model.Gate list
    | ExplainPayload of Explanation list
    | ContractPayload of ContractEntry list
    | EvidencePayload of ProjectEvidenceReport

/// Final value rendered to text/JSON and returned to Program.fs.
type CommandResult =
    { Request: RunRequest option
      Payload: CommandPayload option
      Budget: BudgetState
      Failures: Failure list
      Exit: ExitDecision }

/// CLI command-state phase.
type Phase =
    | Starting
    | LoadingSnapshot
    | RunningHost
    | RenderingOutput
    | Done

/// Durable CLI MVU model.
type Model =
    { Phase: Phase
      RawArgv: string list
      Request: RunRequest option
      Snapshot: ProjectSnapshot option
      HostModel: FS.GG.Governance.Host.Model<ProjectFact> option
      Budget: BudgetState
      Result: CommandResult option }

/// Events/results accepted by the CLI MVU boundary.
type Msg =
    | Parsed of Result<RunRequest, ParseError list>
    | SnapshotLoaded of Result<ProjectSnapshot, string>
    | HostCompleted of host: FS.GG.Governance.Host.Model<ProjectFact> * budget: BudgetState
    | OutputWritten of Result<unit, string>

/// I/O requested by the pure CLI update.
type Effect =
    | LoadSnapshot of RunRequest
    | RunHost of RunRequest * ProjectSnapshot
    | WriteOutput of RunRequest * CommandResult
    | Finish of ExitDecision

/// Impure ports injected into the command edge and faked in semantic tests.
type CliPorts =
    { LoadSnapshot: RunRequest -> Result<ProjectSnapshot, string>
      RunHost: RunRequest -> ProjectSnapshot -> FS.GG.Governance.Host.Model<ProjectFact> * BudgetState
      WriteOutput: RunRequest -> CommandResult -> Result<unit, string> }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Cli =

    /// Parse argv into a normalized run request. Performs no filesystem I/O.
    val parse: argv: string list -> Result<RunRequest, ParseError list>

    /// Numeric process exit code for an exit decision.
    val exitCode: decision: ExitDecision -> int

    /// Deterministic ordinal-sorted distinct projection of a string list. Shared pure
    /// vocabulary reused by the parser (scope normalization) and CliRender (`jsonArray`).
    val stableStrings: values: string list -> string list

    /// Initialize the CLI MVU boundary from raw argv.
    val init: argv: string list -> Model * Effect list

    /// Pure CLI update: parse -> load snapshot -> run Host -> write output -> finish.
    val update: msg: Msg -> model: Model -> Model * Effect list

    /// Drive the CLI MVU boundary to completion using injected ports.
    val run: ports: CliPorts -> argv: string list -> CommandResult
