// The thin host entry for `fsgg evidence` (069): parse argv → build the real ports → run the interpreter →
// print any diagnostics to stderr → return the mapped exit code. ALL decision logic lives in the pure `Loop`;
// ALL I/O in the `Interpreter`. A `parse` rejection exits 2 BEFORE any port is built, so a usage error writes
// no artifact and starts no git/filesystem work. Effective evidence is INFORMATION: the exit code is
// operational only, never a ship/merge verdict (FR-007).

module FS.GG.Governance.EvidenceCommand.Program

open FS.GG.Governance.EvidenceCommand

let usageMessage (err: Loop.UsageError) : string =
    match err with
    | Loop.UnknownFlag flag -> "unknown flag: " + flag
    | Loop.MissingValue flag -> "missing value for flag: " + flag
    | Loop.BadFormat value -> "unknown --format value: " + value + " (expected human|text|json)"

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
        eprintfn "fsgg evidence: %s" (usageMessage err)
        Loop.exitCode Loop.UsageError'
    | Ok request ->
        let ports = Interpreter.realPorts request.Repo
        let model = Interpreter.run ports request

        for d in model.Diagnostics do
            eprintfn "fsgg evidence [%s]: %s" (categoryToken d.Category) d.Message

        Loop.exitCode model.Exit
