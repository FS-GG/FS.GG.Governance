// The thin host entry for `fsgg refresh` (F057): parse argv → build the real ports → run the interpreter →
// print any diagnostics to stderr → return the mapped exit code. ALL decision logic lives in the pure
// `Loop`; ALL I/O in the `Interpreter`. A `parse` rejection exits 2 BEFORE any port is built, so a usage
// error writes nothing and starts no filesystem work. stderr diagnostics are tagged
// `fsgg refresh [<category>]: <message>` so a missing/malformed INPUT is distinguishable from a TOOL defect
// (Constitution VI).

module FS.GG.Governance.RefreshCommand.Program

open FS.GG.Governance.RefreshCommand
open FS.GG.Governance.RefreshJson.RefreshModel

let categoryToken (outcome: RefreshOutcome) : string =
    match outcome with
    | NothingToRefresh -> "ok"
    | ViewsRegenerated -> "regenerated"
    | StaleUnresolved' -> "stale-unresolved"
    | UsageError' -> "usage"
    | InputUnavailable -> "input-unavailable"
    | ToolError -> "tool-error"

[<EntryPoint>]
let main argv =
    match Loop.parse (List.ofArray argv) with
    | Error err ->
        eprintfn "fsgg refresh [usage]: %s" err.Message
        Loop.exitCode UsageError'
    | Ok request ->
        let ports = Interpreter.realPorts request.Repo
        let model = Interpreter.run ports request

        for d in model.Diagnostics do
            eprintfn "fsgg refresh [%s]: %s" (categoryToken d.Category) d.Message

        Loop.exitCode model.Exit
