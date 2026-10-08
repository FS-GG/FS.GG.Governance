// The pure gate-run helpers (F052). Visibility lives in Plan.fsi (Principle II) — this file carries NO
// `private`/`internal`/`public` modifiers on top-level bindings; the argv scanner stays unexported by ABSENCE
// from the .fsi. Everything is PURE — no process, no clock, no I/O. The run itself is the injected F051 port,
// at the command's interpreter edge.

namespace FS.GG.Governance.GateRun

open FS.GG.Governance.CommandRecord.Model // Executable, Argument, ExitCode, EnvironmentDelta
open FS.GG.Governance.EvidenceReuse.Model // EvidenceRef
open FS.GG.Governance.Config.Model // ToolingFacts, CommandSpec, CommandId, TimeoutLimit
open FS.GG.Governance.Gates.Model // Gate, GatePrerequisite, RequiresCommand
open FS.GG.Governance.GateExecution.Model // GateCommand

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Plan =

    type NoCommand =
        | NoPrerequisite
        | UnresolvedCommand of CommandId
        | EmptyCommandLine

    // A small explicit single-pass character scanner: whitespace separates tokens; single quotes, double
    // quotes, and backslash escapes group/quote a token; NO shell features (no globbing, variable expansion,
    // pipes, or redirection — those characters are literal). Unexported (absent from Plan.fsi). The `mutable`
    // index/accumulator/in-token flag are DISCLOSED and CONFINED here — the constitution's sanctioned hot-loop
    // use (Principle III). A token is started by any non-whitespace char OR an opening quote (so `''` yields an
    // empty token), letting an empty/all-whitespace line yield no tokens at all.
    let lexCommandLine (commandLine: string) : (Executable * Argument list) option =
        // mutable: single-pass argv scan
        let tokens = System.Collections.Generic.List<string>()
        let sb = System.Text.StringBuilder()
        let mutable i = 0
        let mutable inToken = false
        // A quote left open at end-of-input is malformed (FR-008, #56/B4): accepting it silently would
        // let a truncated command line lex into a bogus argv. Track it and reject the whole line.
        let mutable malformed = false
        let n = commandLine.Length

        let flush () =
            if inToken then
                tokens.Add(sb.ToString())
                sb.Clear() |> ignore
                inToken <- false

        while i < n do
            let c = commandLine.[i]

            if c = ' ' || c = '\t' || c = '\n' || c = '\r' then
                flush ()
                i <- i + 1
            elif c = '\'' then
                // Single quotes: every character literal until the closing quote (POSIX — no escapes inside).
                inToken <- true
                i <- i + 1

                while i < n && commandLine.[i] <> '\'' do
                    sb.Append(commandLine.[i]) |> ignore
                    i <- i + 1

                if i < n then
                    i <- i + 1 // skip closing quote
                else
                    malformed <- true // unterminated single quote
            elif c = '"' then
                // Double quotes: a backslash escapes the next character; everything else literal until close.
                inToken <- true
                i <- i + 1

                while i < n && commandLine.[i] <> '"' do
                    if commandLine.[i] = '\\' && i + 1 < n then
                        sb.Append(commandLine.[i + 1]) |> ignore
                        i <- i + 2
                    else
                        sb.Append(commandLine.[i]) |> ignore
                        i <- i + 1

                if i < n then
                    i <- i + 1 // skip closing quote
                else
                    malformed <- true // unterminated double quote
            elif c = '\\' then
                // A bare backslash escapes the next character (a trailing backslash is dropped).
                inToken <- true

                if i + 1 < n then
                    sb.Append(commandLine.[i + 1]) |> ignore
                    i <- i + 2
                else
                    i <- i + 1
            else
                inToken <- true
                sb.Append(c) |> ignore
                i <- i + 1

        flush ()

        if malformed then
            None
        else
            match List.ofSeq tokens with
            | [] -> None
            | exe :: args -> Some(Executable exe, args |> List.map Argument)

    let commandFor (repoRoot: string) (tooling: ToolingFacts) (gate: Gate) : Result<GateCommand, NoCommand> =
        // The declared command id is the gate's `RequiresCommand` prerequisite (F018); resolve it against the
        // loaded `tooling.Commands`, lex the declared command line, and assemble the GateCommand from DECLARED
        // inputs only (FR-002): repoRoot cwd, EMPTY env delta (the `EnvironmentClass` is a where-it-runs
        // declaration, not an env mutation), the declared timeout verbatim, `NoCapturedOutput`.
        let commandId =
            gate.Prerequisites
            |> List.tryPick (fun p ->
                match p with
                | RequiresCommand c -> Some c)

        match commandId with
        | None -> Error NoPrerequisite
        | Some id ->
            match tooling.Commands |> List.tryFind (fun spec -> spec.Id = id) with
            | None -> Error(UnresolvedCommand id)
            | Some spec ->
                match lexCommandLine spec.Command with
                | None -> Error EmptyCommandLine
                | Some(exe, args) ->
                    Ok
                        {
                            Executable = exe
                            Arguments = args
                            WorkingDirectory = WorkingDirectory repoRoot
                            Environment =
                                {
                                    Added = []
                                    Changed = []
                                    Removed = []
                                }
                            Timeout = spec.Timeout
                            CapturedOutput = NoCapturedOutput
                        }

    type ProviderCommandContext =
        { RepoRoot: string
          Environment: EnvironmentClass
          EnvironmentDelta: EnvironmentDelta
          RemainingTimeout: TimeoutLimit }

    let providerDiagnostic id field code message : FS.GG.Governance.Config.CapabilityBindings.Diagnostic =
        { CapabilityId = id; Field = field; Code = code; Message = message }

    let permitsEnvironment allowed observed =
        allowed = observed || (allowed = LocalOrCi && (observed = Local || observed = Ci))

    let commandForBinding
        (context: ProviderCommandContext)
        (gate: Gate)
        (binding: FS.GG.Governance.Config.CapabilityBindings.CapabilityBinding)
        : Result<GateCommand, FS.GG.Governance.Config.CapabilityBindings.Diagnostic list> =
        let id = binding.CapabilityId
        let reject field code message = Error [ providerDiagnostic id field code message ]
        let (TimeoutLimit remaining) = context.RemainingTimeout
        let (TimeoutLimit gateSeconds) = gate.Timeout
        let names =
            (context.EnvironmentDelta.Added |> List.map (fun v -> v.Name))
            @ (context.EnvironmentDelta.Changed |> List.map (fun v -> v.Name))
            @ (context.EnvironmentDelta.Removed |> List.map (fun v -> v.Name))
        let invalidName (EnvVarName name) =
            System.String.IsNullOrWhiteSpace name || name.Contains('=') || name.Contains(char 0)
        let invalidValue (EnvVarValue value) =
            System.Object.ReferenceEquals(value, null) || value.Contains(char 0)
        let badValue =
            (context.EnvironmentDelta.Added |> List.exists (fun v -> invalidValue v.Value))
            || (context.EnvironmentDelta.Changed |> List.exists (fun v -> invalidValue v.Old || invalidValue v.New))
            || (context.EnvironmentDelta.Removed |> List.exists (fun v -> invalidValue v.Old))
        if gateIdValue gate.Id <> id then
            reject "binding.capabilityId" FS.GG.Governance.Config.CapabilityBindings.MalformedInput "The binding must match the exact effective gate identity."
        elif remaining <= 0 || gateSeconds <= 0 then
            reject "execution.timeout" FS.GG.Governance.Config.CapabilityBindings.InvalidExecutionLimit "Remaining host and effective gate timeouts must be positive."
        elif context.Environment = LocalOrCi then
            reject "execution.environment" FS.GG.Governance.Config.CapabilityBindings.InvalidExecutionLimit "An observed concrete execution environment is required."
        elif not (permitsEnvironment gate.FreshnessKey.Environment context.Environment) then
            reject "execution.environment" FS.GG.Governance.Config.CapabilityBindings.InvalidExecutionLimit "The effective gate does not admit this execution environment."
        elif (names |> List.exists invalidName) || badValue || List.length names <> (names |> List.distinct |> List.length) then
            reject "execution.environmentDelta" FS.GG.Governance.Config.CapabilityBindings.MalformedInput "Environment delta names must be unique and names/values must be valid process inputs."
        elif System.String.IsNullOrWhiteSpace context.RepoRoot || not (System.IO.Path.IsPathFullyQualified context.RepoRoot) then
            reject "execution.repoRoot" FS.GG.Governance.Config.CapabilityBindings.InvalidPath "An absolute governed root is required; physical containment is revalidated by the host."
        else
            match binding.Binding, binding.Limits with
            | FS.GG.Governance.Config.CapabilityBindings.Command command, Some limits ->
                match limits.WorkingDirectory, limits.Timeout, limits.Cost with
                | Some (GovernedPath relative), Some (TimeoutLimit seconds), Some _ when seconds > 0 ->
                    let spelling = if System.Object.ReferenceEquals(relative, null) then "" else relative
                    let segments = spelling.Replace('\\', '/').Split('/')
                    let (GovernedPath normalized) = normalizePath spelling
                    // A resolved path is normalized; independently calling this projection on raw
                    // traversals/absolute paths must not acquire a command by normalizing them away.
                    if System.String.IsNullOrWhiteSpace relative || normalized <> relative || relative.Contains(':')
                       || relative.StartsWith('/') || (segments |> Array.contains "..")
                       || (relative |> Seq.exists System.Char.IsControl) then
                        reject "limits.workingDirectory" FS.GG.Governance.Config.CapabilityBindings.InvalidPath "A normalized governed relative working directory is required."
                    elif not (limits.Environments |> List.exists (fun allowed -> permitsEnvironment allowed context.Environment)) then
                        reject "limits.environments" FS.GG.Governance.Config.CapabilityBindings.InvalidExecutionLimit "Binding does not admit this execution environment."
                    elif System.String.IsNullOrWhiteSpace command.Executable || command.Executable.Contains(char 0)
                         || (command.Arguments |> List.exists (fun argument -> System.Object.ReferenceEquals(argument, null) || argument.Contains(char 0))) then
                        reject "binding.command" FS.GG.Governance.Config.CapabilityBindings.MalformedInput "Executable and literal arguments must be valid process inputs."
                    else
                        try
                            Ok
                                { Executable = Executable command.Executable
                                  Arguments = command.Arguments |> List.map Argument
                                  WorkingDirectory = WorkingDirectory (System.IO.Path.GetFullPath(System.IO.Path.Combine(context.RepoRoot, relative)))
                                  Environment = context.EnvironmentDelta
                                  Timeout = TimeoutLimit (min remaining (min seconds gateSeconds))
                                  CapturedOutput = NoCapturedOutput }
                        with :? System.ArgumentException ->
                            reject "limits.workingDirectory" FS.GG.Governance.Config.CapabilityBindings.InvalidPath "Working directory has an invalid path spelling."
                | _ -> reject "limits" FS.GG.Governance.Config.CapabilityBindings.InvalidExecutionLimit "Explicit positive executable limits are required."
            | FS.GG.Governance.Config.CapabilityBindings.SemanticOnly, _ ->
                reject "binding" FS.GG.Governance.Config.CapabilityBindings.IllegalSemanticOnly "A semantic-only obligation cannot project an executable command."
            | _ -> reject "limits" FS.GG.Governance.Config.CapabilityBindings.InvalidExecutionLimit "Explicit executable limits are required."

    let priorExitOf (reference: EvidenceRef) : ExitCode option =
        // The reference is the F032 canonical-identity string (F049 `referenceOf`), segments joined by '\n'.
        // The exit code is the `exit=1<len>:<value>` segment (presence digit `1`, decimal byte length, ':',
        // the decimal exit code). Read that ONE documented segment; any non-canonical reference (no such
        // segment, wrong shape, length mismatch) yields `None` ⇒ conservatively recompute (FR-004, D2). This
        // is the only place the otherwise-opaque reference is read (FR-015).
        let (EvidenceRef s) = reference

        s.Split('\n')
        |> Array.tryPick (fun segment ->
            if segment.StartsWith "exit=" then
                let body = segment.Substring 5

                if body.Length >= 1 && body.[0] = '1' then
                    let rest = body.Substring 1 // "<len>:<value>"

                    match rest.IndexOf ':' with
                    | -1 -> None
                    | colon ->
                        let lenText = rest.Substring(0, colon)
                        let value = rest.Substring(colon + 1)

                        match System.Int32.TryParse lenText with
                        | true, len when System.Text.Encoding.UTF8.GetByteCount value = len ->
                            match System.Int32.TryParse value with
                            | true, code -> Some(ExitCode code)
                            | _ -> None
                        | _ -> None
                else
                    None
            else
                None)

    let passed (exitCode: ExitCode) : bool = exitCode = ExitCode 0
