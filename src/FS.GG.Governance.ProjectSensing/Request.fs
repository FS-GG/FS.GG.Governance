namespace FS.GG.Governance.Cli

open FS.GG.Governance.Kernel
open FS.GG.Governance.Host

type CommandKind =
    | RouteCommand
    | ExplainCommand
    | ContractCommand
    | EvidenceCommand
    | WatchCommand
    | TuiCommand

type OutputFormat =
    | Text
    | Json

type ReviewBudget =
    | CacheOnly
    | FreshReviews of count: int

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
      ExplicitPlain: bool }
