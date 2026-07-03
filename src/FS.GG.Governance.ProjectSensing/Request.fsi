// Curated public signature for the CLI run-request vocabulary (100, M-ARCH-2).
//
// CommandKind/OutputFormat/ReviewBudget/RunRequest were extracted verbatim from the Cli
// executable (Cli.fs) into this internal ProjectSensing library — same `FS.GG.Governance.Cli`
// namespace — so the sensing edge (ArtifactReading, which consumes a RunRequest) can live in a
// library and be reused by both the Cli exe and the EvidenceCommand tool without any executable
// referencing another executable. Every `RunRequest`/`CommandKind`/… reference in Cli and
// EvidenceCommand is unchanged (same namespace, same shapes). Compiles after Project (RunRequest
// carries a `Set<Domain>`), before ArtifactReading.

namespace FS.GG.Governance.Cli

open FS.GG.Governance.Kernel
open FS.GG.Governance.Host

/// The fixed user-visible operations. `WatchCommand`/`TuiCommand` (F27 wiring 063, US3/US4) are the
/// read-only interactive surfaces: they carry NO JSON contract and are dispatched at the Program edge
/// (which composes a real F19 `RouteResult` view via the route pipeline and drives
/// `HumanRender.Watch.run`/`Tui.run`), never through the one-shot snapshot→host→output MVU.
type CommandKind =
    | RouteCommand
    | ExplainCommand
    | ContractCommand
    | EvidenceCommand
    | WatchCommand
    | TuiCommand

/// Output format selected by `--format` or `--json`.
type OutputFormat =
    | Text
    | Json

/// Fresh agent-review budget granted by the caller. `CacheOnly` is the default.
type ReviewBudget =
    | CacheOnly
    | FreshReviews of count: int

/// Normalized command invocation.
type RunRequest =
    { Root: string
      Command: CommandKind
      Mode: RunMode
      Format: OutputFormat
      Scope: string list
      Domains: Set<Domain>
      ReviewBudget: ReviewBudget
      ReviewStore: string option
      OutputPath: string option
      Judge: JudgeId
      /// F27 wiring (063): the host-parsed `--plain` flag, carried to the capability-sensing edge so a
      /// piped/explicit-plain run renders ANSI-free even on a TTY (FR-004/FR-012). It is NOT serialized
      /// into the JSON envelope (`requestJson` is unchanged), so every JSON contract stays byte-identical.
      ExplicitPlain: bool }
