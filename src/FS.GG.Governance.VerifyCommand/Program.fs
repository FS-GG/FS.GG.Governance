// The thin host entry for `fsgg verify` (F056): parse argv → build the real ports → run the interpreter →
// print any diagnostics to stderr → return the mapped exit code. ALL decision logic lives in the pure
// `Loop`; ALL I/O in the `Interpreter`. A parse rejection exits 2 BEFORE any port is built, so a usage
// error (including an unrecognized profile or a forbidden `--mode`) writes no artifact and starts no
// git/filesystem work. A blocked verdict is NOT a diagnostic — it is reported in the summary and surfaces as
// exit 1. Verify is NOT the merge authority (FR-017): a passing `fsgg verify` does not authorize a merge.

module FS.GG.Governance.VerifyCommand.Program

open FS.GG.Governance.VerifyCommand

let usageMessage (err: Loop.UsageError) : string =
    match err with
    | Loop.UnknownFlag flag -> "unknown flag: " + flag
    | Loop.UnexpectedArgument value -> "unexpected argument: " + value
    | Loop.MissingValue flag -> "missing value for flag: " + flag
    | Loop.PathsAndSinceTogether -> "--paths and --since are mutually exclusive"
    | Loop.EmptyPaths -> "--paths requires at least one path"
    | Loop.UnrecognizedProfile p -> "unrecognized --profile: " + p

let categoryToken (decision: Loop.ExitDecision) : string =
    match decision with
    | Loop.Success -> "ok"
    | Loop.Blocked -> "blocked"
    | Loop.UsageError' -> "usage"
    | Loop.InputUnavailable -> "input-unavailable"
    | Loop.ToolError -> "tool-error"

// The caller of this seam owns any original capture session for the whole invocation.
// A loader rejection precedes real-port construction, catalog reads and legacy execution.
let internal runWithProviderLoader
    (loadProvider: Loop.RunRequest -> ProviderContext.Selection -> Result<Loop.ProviderPlanningInput, string>)
    (makePorts: string -> Interpreter.Ports)
    (report: string -> unit)
    argv =
    match Loop.parseInvocation (List.ofArray argv) with
    | Error(Loop.LegacyUsage err) ->
        report ("fsgg verify: " + usageMessage err)
        Loop.exitCode Loop.UsageError'
    | Error(Loop.ProviderUsage err) ->
        report (sprintf "fsgg verify: provider selection rejected: %A" err)
        Loop.exitCode Loop.UsageError'
    | Ok invocation ->
        let outcome =
            match invocation.Provider with
            | None -> Ok(Interpreter.run (makePorts invocation.Request.Repo) invocation.Request)
            | Some selection ->
                match loadProvider invocation.Request selection with
                | Error message -> Error message
                | Ok provider ->
                    let model = Interpreter.runProviderPlanning (makePorts invocation.Request.Repo) invocation.Request provider
                    Ok model.Legacy
        match outcome with
        | Error message ->
            report ("fsgg verify [input-unavailable]: " + message)
            Loop.exitCode Loop.InputUnavailable
        | Ok model ->
            for diagnostic in model.Diagnostics do
                report (sprintf "fsgg verify [%s]: %s" (categoryToken diagnostic.Category) diagnostic.Message)
            Loop.exitCode model.Exit

// The supplying caller retains every returned session, including failures, through its own teardown.
// This adapter performs no acquisition, renewal, release, disposal or provider execution.
let internal runWithCapturedProvider
    (supply: Loop.RunRequest -> ProviderContext.Selection -> Result<ProviderContextCapture.CaptureSession * FS.GG.Governance.GateRun.Plan.ProviderCommandContext, string>)
    makePorts report argv =
    let load request selection =
        match supply request selection with
        | Error message -> Error message
        | Ok(session, context) ->
            let observation = ProviderContextCapture.inspect session
            let root = System.IO.Path.GetFullPath request.Repo
            if context.RepoRoot <> root
               || not (observation.Roots |> List.exists (fun r -> r.RequestedPath = root && r.CurrentBindingMatches = Some true)) then
                Error "provider command context does not bind the original captured repository root"
            else
                match ProviderContextLoader.load selection session with
                | Error errors -> Error(sprintf "provider declaration admission rejected: %A" errors)
                | Ok resolution -> Ok ({ ResolutionRequest = resolution; CommandContext = context }: Loop.ProviderPlanningInput)
    runWithProviderLoader load makePorts report argv

[<EntryPoint>]
let main argv =
    // Standalone capture cannot yet establish its externally owned lifetime/teardown contract.
    // Preserve that exact missing prerequisite instead of starting an unowned native activity.
    let unavailable _ _ =
        Error "provider capture requires an independently admitted caller-owned lifetime and teardown boundary; standalone provider execution is unavailable"
    runWithProviderLoader unavailable Interpreter.realPorts (eprintfn "%s") argv
