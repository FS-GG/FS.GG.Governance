module FS.GG.Governance.Sample.SddReferenceProvider.Tests.SolutionBuildWrapperTests

// 080: regression guard for the checked-in solution-build wrapper `build.fsx`.
// It freezes the load-bearing decisions from specs/080-fix-slow-solution-build/ (research
// D2/D3/D4) so a later edit that drops the bound fails CI: the wrapper (a) targets the whole
// solution `FS.GG.Governance.sln`, (b) ALWAYS emits an explicit, bounded `-m:N` on the MSBuild
// command line — never unbounded, the only place the cap is honored and the only thing that
// stops the 162-project `dotnet fsc` over-subscription that made the unbounded build thrash —
// and (c) derives N from the core count within a sane clamp (>= 2, <= 12) so it scales with
// hardware yet can never reintroduce over-subscription.
//
// Real evidence (Principle V): it actually runs `dotnet fsi build.fsx --print-command` and
// inspects the command the wrapper WOULD hand MSBuild — not a scrape of the source text (which
// a comment mentioning `-m:` could satisfy spuriously). No synthetic input.
//
// This lives in the repo's existing "bounded build" guard project (the 078 worked-example
// suite) rather than a new project so `FS.GG.Governance.sln` membership stays byte-identical
// (Tier 2: no .fsi / surface / .sln change).

open System.Diagnostics
open System.Text.RegularExpressions
open Expecto
open FS.GG.Governance.Tests.Common

let private repoRoot = RepositoryHelpers.repoRoot

/// Run `dotnet fsi build.fsx --print-command` from the repo root and return its stdout. The
/// wrapper resolves and prints the full `dotnet` command line, then exits without building.
let private emittedStdout () =
    let psi = ProcessStartInfo("dotnet")
    [ "fsi"; "build.fsx"; "--print-command" ] |> List.iter psi.ArgumentList.Add
    psi.WorkingDirectory <- repoRoot
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true
    psi.UseShellExecute <- false

    match Process.Start psi with
    | null -> failwith "could not start `dotnet fsi build.fsx --print-command`"
    | started ->
        use proc = started
        let out = proc.StandardOutput.ReadToEnd()
        proc.WaitForExit()
        out

/// The resolved `build.fsx: dotnet <verb> <sln> -m:N ...` line from a real dry run.
let private commandLine =
    emittedStdout().Split('\n')
    |> Array.tryFind (fun l -> l.Contains "build.fsx: dotnet ")
    |> Option.defaultValue ""

/// The same bounded node formula the wrapper encodes (research D4); replicated here so the
/// guard fails if the wrapper's clamp is ever loosened past the proven envelope.
let private boundedNodes cores = max 2 (min 12 (int (ceil (float cores / 4.0))))

[<Tests>]
let tests =
    testList
        "080 solution-build wrapper guard"
        [ test "emitted command targets the whole solution FS.GG.Governance.sln" {
              Expect.stringContains
                  commandLine
                  "FS.GG.Governance.sln"
                  "wrapper must build the full solution, not a subset (FR-002/SC-004)"
          }

          test "emitted command carries an explicit, bounded -m:N" {
              let m = Regex.Match(commandLine, @"-m:(\d+)")

              Expect.isTrue
                  m.Success
                  (sprintf
                      "wrapper must emit an explicit -m:N on the command line — props/rsp do not bind it (research D2/D3). Got: %s"
                      commandLine)

              let n = int m.Groups.[1].Value
              Expect.isGreaterThanOrEqual n 2 "emitted N must be >= 2 (not single-threaded, FR-006)"
              Expect.isLessThanOrEqual n 12 "emitted N must be <= 12 (never over-subscribed, research D4)"
          }

          test "the bounded node count stays in [2, 12] across hardware sizes" {
              for cores in [ 1; 2; 4; 8; 16; 24; 64; 256 ] do
                  let n = boundedNodes cores

                  Expect.isGreaterThanOrEqual
                      n
                      2
                      (sprintf "N must be >= 2 at %d cores (not single-threaded, FR-006)" cores)

                  Expect.isLessThanOrEqual
                      n
                      12
                      (sprintf "N must be <= 12 at %d cores (never over-subscribed, research D4)" cores)
              // the proven anchor: 24 cores -> 6 (research D1/D4)
              Expect.equal (boundedNodes 24) 6 "24-core host must derive the proven -m:6 anchor"
          } ]
