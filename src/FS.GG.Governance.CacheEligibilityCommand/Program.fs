// The thin host entry for `fsgg cache-eligibility` (F044): parse argv → build the real ports → run the
// interpreter → print the summary to stdout and any diagnostics to stderr → return the mapped exit code.
// ALL decision logic lives in the pure `Loop`; ALL I/O in the `Interpreter`. A `parse` rejection exits 2
// BEFORE any port is built, so a usage error writes no artifact and starts no git/filesystem/hash work.
// Cache eligibility is INFORMATION: a gate that must recompute or is unresolved is exit 0, never a failure.

module FS.GG.Governance.CacheEligibilityCommand.Program

open FS.GG.Governance.CacheEligibilityCommand

let usageMessage (err: Loop.UsageError) : string =
    match err with
    | Loop.UnknownFlag flag -> "unknown flag: " + flag
    | Loop.MissingValue flag -> "missing value for flag: " + flag
    | Loop.PathsAndSinceTogether -> "--paths and --since are mutually exclusive"
    | Loop.EmptyPaths -> "--paths requires at least one path"
    | Loop.BadFormat value -> "unknown --format value: " + value + " (expected human|json)"

let categoryToken (decision: Loop.ExitDecision) : string =
    match decision with
    | Loop.Success -> "ok"
    | Loop.UsageError' -> "usage"
    | Loop.InputUnavailable -> "input-unavailable"
    | Loop.ToolError -> "tool-error"

[<EntryPoint>]
let main argv =
    match Loop.parse (List.ofArray argv) with
    | Error err ->
        eprintfn "fsgg cache-eligibility: %s" (usageMessage err)
        Loop.exitCode Loop.UsageError'
    | Ok request ->
        let ports = Interpreter.realPorts request.Repo
        let model = Interpreter.run ports request

        for d in model.Diagnostics do
            eprintfn "fsgg cache-eligibility [%s]: %s" (categoryToken d.Category) d.Message

        Loop.exitCode model.Exit
