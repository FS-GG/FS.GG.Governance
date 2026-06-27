module FS.GG.Governance.Sample.SddReferenceProvider.Tests.BoundedBuildTests

open System
open System.Diagnostics
open System.Runtime.InteropServices
open Expecto
open FS.GG.Governance.Sample.SddReferenceProvider.Tests.Support

// US1 forced-stall bound (research D9, quickstart Scenario 2, contract §"bounded" test): prove the bound is
// real and cheap — a genuine OS process tree is genuinely killed within budget+margin in ~1-2 s, with NO
// real hanging `dotnet build` and no network/SDK reliance (FR-010, Principle V). It exercises the same
// `Support.runBounded` primitive `dotnetBuild` is built on.

/// The OS-selected sleeper that waits far longer than the sub-second test budget, so `runBounded` MUST cut
/// it off. Returns (exe, args) or None on a platform with no known sleeper (⇒ a NAMED platform skip, never
/// a silent green). On Windows `ping -n <n> 127.0.0.1` is a redirect-safe wait (unlike `timeout`, which
/// throws when stdin is redirected, as it is here).
let private sleeper () : (string * string) option =
    if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
        Some("ping", "-n 30 127.0.0.1")
    elif RuntimeInformation.IsOSPlatform OSPlatform.Linux || RuntimeInformation.IsOSPlatform OSPlatform.OSX then
        Some("sleep", "30")
    else
        None

/// True iff a process with `pid` is still running. A reaped child ⇒ `GetProcessById` throws
/// `ArgumentException` (its only "not running" signal) ⇒ false. Checking the EXACT spawned PID — not a scan
/// by process name — is the only race-free death check when builds run concurrently (FR-010).
let private isAlive (pid: int) : bool =
    try
        not (Process.GetProcessById(pid).HasExited)
    with :? ArgumentException ->
        false

[<Tests>]
let tests =
    testList
        "BoundedBuild"
        [ test "bounded: a stalled build is cut off within budget+margin" {
              match sleeper () with
              | None -> skiptest "PLATFORM: no sleeper available to force a stall"
              | Some(exe, args) ->
                  let budget = TimeSpan.FromMilliseconds 500.
                  // `runBounded` reports the spawned PID synchronously on this thread, so the capture is
                  // race-free even when other builds run concurrently in the full suite.
                  let mutable childPid = ValueNone

                  let sw = Stopwatch.StartNew()
                  let outcome = runBounded exe args None budget (fun pid -> childPid <- ValueSome pid)
                  sw.Stop()

                  // (a) the outcome is a TimedOut carrying the configured budget — never silently a pass.
                  match outcome with
                  | TimedOut(b, _) -> Expect.equal b budget "the timeout carries the configured budget"
                  | other -> failtestf "expected TimedOut, got %A" other

                  // (b) the bounded call returned within budget + the named assertion margin.
                  Expect.isLessThan
                      sw.Elapsed
                      (budget + boundAssertionMargin)
                      "the bounded call returned within budget + margin"

                  // (c) the EXACT spawned sleeper is gone (Kill(entireProcessTree=true) + drain reaped it).
                  match childPid with
                  | ValueNone -> failtest "runBounded never reported a started sleeper PID"
                  | ValueSome pid -> Expect.isFalse (isAlive pid) "the spawned sleeper process tree was terminated"
          } ]
