// build.fsx — the checked-in, bounded build/test command for FS.GG.Governance.sln.
//
// Why this exists: the 162-project solution thrashes under `dotnet build`'s default
// parallelism (one MSBuild worker node per logical core, each launching its own
// `dotnet fsc` process — F# has no shared compiler server like C#'s VBCSCompiler), so a
// 24-wide build over-subscribes threads/heaps and fails with MSB6003/MSB6006 instead of
// finishing. Bounding the node count with an explicit `-m:N` on the MSBuild *command
// line* (the only place it is honored — not Directory.Build.props/.rsp) turns a >10-min
// failing build into a ~33 s green one. N scales with the machine and is never unbounded.
// See specs/080-fix-slow-solution-build/ (research D1–D4).
//
// Usage:
//   dotnet fsi build.fsx                          # build the whole solution (Debug)
//   dotnet fsi build.fsx test                     # run the whole test suite
//   dotnet fsi build.fsx build -c Release         # any extra args pass through to dotnet
//   dotnet fsi build.fsx --no-restore /t:Rebuild  # default verb is build

open System
open System.Diagnostics

let solution = "FS.GG.Governance.sln"

let cores = Environment.ProcessorCount

// Bounded, hardware-derived MSBuild node count. Never unbounded: clamp(2, ceil(cores/4), 12).
// 24 cores -> 6 (the proven anchor); 2 cores -> 2; 64 cores -> 12.
let maxNodes = max 2 (min 12 (int (ceil (float cores / 4.0))))

let rawArgs =
    fsi.CommandLineArgs
    |> Array.toList
    |> List.tail // drop the script path itself

// `--print-command` resolves and prints the dotnet command line, then exits without building.
// It exists so the checked-in guard can assert the *actual emitted* bound rather than scrape source.
let printOnly = rawArgs |> List.contains "--print-command"
let scriptArgs = rawArgs |> List.filter (fun a -> a <> "--print-command")

let verb, rest =
    match scriptArgs with
    | ("build" | "test" as v) :: tail -> v, tail
    | other -> "build", other

// Default to Debug, but never duplicate a configuration the caller already supplied.
let hasConfig = rest |> List.exists (fun a -> a = "-c" || a = "--configuration")
let configArgs = if hasConfig then [] else [ "-c"; "Debug" ]

let dotnetArgs = [ verb; solution; sprintf "-m:%d" maxNodes ] @ configArgs @ rest

printfn "build.fsx: %d logical cores detected -> -m:%d (bounded)" cores maxNodes
printfn "build.fsx: dotnet %s" (String.Join(" ", dotnetArgs))

if printOnly then
    printfn "build.fsx: --print-command set; not building."
    exit 0

let psi = ProcessStartInfo("dotnet")
dotnetArgs |> List.iter psi.ArgumentList.Add
psi.UseShellExecute <- false

let sw = Stopwatch.StartNew()
let proc = Process.Start psi
proc.WaitForExit()
sw.Stop()

printfn
    "build.fsx: %s %s completed in %d ms (exit %d)"
    verb
    solution
    sw.ElapsedMilliseconds
    proc.ExitCode

// Preserve the underlying dotnet exit code so a real failure still fails (FR-009).
exit proc.ExitCode
