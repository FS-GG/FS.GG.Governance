// The thin host entry for `fsgg release` (F055): parse argv → build the real ports → run the interpreter
// → print any diagnostics to stderr → return the mapped exit code. ALL decision logic lives in the pure
// `Loop`; ALL I/O in the `Interpreter`. A `parse` rejection exits 2 BEFORE any port is built, so a usage
// error writes no artifact and starts no filesystem work. A blocked verdict is NOT a diagnostic — it is
// reported in the summary and surfaces as exit 1. stderr diagnostics are tagged `fsgg release
// [<category>]: <message>` so a missing/malformed INPUT is distinguishable from a TOOL defect (Constitution
// VI).

module FS.GG.Governance.ReleaseCommand.Program

open FS.GG.Governance.ReleaseCommand

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
        eprintfn "fsgg release [usage]: %s" err.Message
        Loop.exitCode Loop.UsageError'
    | Ok request ->
        let ports = Interpreter.realPorts request.Repo
        let model = Interpreter.run ports request

        for d in model.Diagnostics do
            eprintfn "fsgg release [%s]: %s" (categoryToken d.Category) d.Message

        Loop.exitCode model.Exit
