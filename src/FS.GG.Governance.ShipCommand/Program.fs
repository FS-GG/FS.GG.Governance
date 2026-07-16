// The thin host entry for `fsgg ship` (F026): parse argv → build the real ports → run the interpreter
// → print any diagnostics to stderr → return the mapped exit code. ALL decision logic lives in the
// pure `Loop`; ALL I/O in the `Interpreter`. A `parse` rejection exits 2 BEFORE any port is built, so a
// usage error (including an unrecognized lever) writes no artifact and starts no git/filesystem work
// (FR-010). A blocked verdict is NOT a diagnostic — it is reported in the summary and surfaces as exit 1.

module FS.GG.Governance.ShipCommand.Program

open FS.GG.Governance.ShipCommand

let usageMessage (err: Loop.UsageError) : string =
    match err with
    | Loop.UnknownFlag flag -> "unknown flag: " + flag
    | Loop.UnexpectedArgument value -> "unexpected argument: " + value
    | Loop.MissingValue flag -> "missing value for flag: " + flag
    | Loop.PathsAndSinceTogether -> "--paths and --since are mutually exclusive"
    | Loop.EmptyPaths -> "--paths requires at least one path"
    | Loop.UnrecognizedMode m -> "unrecognized --mode: " + m
    | Loop.UnrecognizedProfile p -> "unrecognized --profile: " + p

let categoryToken (decision: Loop.ExitDecision) : string =
    match decision with
    | Loop.Success -> "ok"
    | Loop.Blocked -> "blocked"
    | Loop.UsageError' -> "usage"
    | Loop.InputUnavailable -> "input-unavailable"
    | Loop.ToolError -> "tool-error"

[<EntryPoint>]
let main argv =
    match Loop.parse (List.ofArray argv) with
    | Error err ->
        eprintfn "fsgg ship: %s" (usageMessage err)
        Loop.exitCode Loop.UsageError'
    | Ok request ->
        let ports = Interpreter.realPorts request.Repo
        let model = Interpreter.run ports request

        for d in model.Diagnostics do
            eprintfn "fsgg ship [%s]: %s" (categoryToken d.Category) d.Message

        Loop.exitCode model.Exit
