// The pure gate-run helpers (F052). Visibility lives in Plan.fsi (Principle II) — this file carries NO
// `private`/`internal`/`public` modifiers on top-level bindings; the argv scanner stays unexported by ABSENCE
// from the .fsi. Everything is PURE — no process, no clock, no I/O. The run itself is the injected F051 port,
// at the command's interpreter edge.

namespace FS.GG.Governance.GateRun

open FS.GG.Governance.CommandRecord.Model      // Executable, Argument, ExitCode, EnvironmentDelta
open FS.GG.Governance.EvidenceReuse.Model       // EvidenceRef
open FS.GG.Governance.Config.Model              // ToolingFacts, CommandSpec, CommandId, TimeoutLimit
open FS.GG.Governance.Gates.Model               // Gate, GatePrerequisite, RequiresCommand
open FS.GG.Governance.GateExecution.Model        // GateCommand

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Plan =

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

                if i < n then i <- i + 1 // skip closing quote
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

                if i < n then i <- i + 1 // skip closing quote
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

        match List.ofSeq tokens with
        | [] -> None
        | exe :: args -> Some(Executable exe, args |> List.map Argument)

    let commandFor (repoRoot: string) (tooling: ToolingFacts) (gate: Gate) : GateCommand option =
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
        | None -> None
        | Some id ->
            match tooling.Commands |> List.tryFind (fun spec -> spec.Id = id) with
            | None -> None
            | Some spec ->
                match lexCommandLine spec.Command with
                | None -> None
                | Some(exe, args) ->
                    Some
                        { Executable = exe
                          Arguments = args
                          WorkingDirectory = WorkingDirectory repoRoot
                          Environment = { Added = []; Changed = []; Removed = [] }
                          Timeout = spec.Timeout
                          CapturedOutput = NoCapturedOutput }

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
