module FS.GG.Governance.Sample.SddReferenceProvider.Tests.Support

open System
open System.Diagnostics
open System.IO
open FS.GG.Governance.Scaffold
open FS.GG.Governance.Scaffold.Model

// Fixtures for the layered worked example. Filesystem effects run against a REAL temp directory via
// `Interpreter.realPorts` (Principle V); the e2e build step shells out to the REAL .NET SDK. The ONLY
// synthetic element is the lifecycle-precondition stand-in — in production that layer is authored by
// sibling-owned `fsgg-sdd init` (research D4); here it is a disclosed literal fixture, marked at its use
// site below and named in the PR description.

// ── repo root (for the surface-drift baselines + the committed golden) ──

let rec findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        let here ext =
            File.Exists(Path.Combine(d.FullName, "FS.GG.Governance." + ext))

        if here "sln" || here "slnx" then d.FullName else findRepoRoot d.Parent

let repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))

/// The committed, byte-stable manifest golden the worked example asserts (data-model §4).
let goldenPath =
    Path.Combine(repoRoot, "fixtures", "sdd-reference", "scaffold-manifest.golden.json")

// ── the lifecycle-precondition stand-in (DISCLOSED SYNTHETIC) ──

/// A small literal set of representative lifecycle-skeleton paths the worked example seeds BEFORE the
/// runtime scaffold and passes as `ScaffoldRequest.ReservedPaths` (data-model §3). The seam treats them
/// as reserved (a provider that emitted any of them would be refused as a collision).
// SYNTHETIC: the lifecycle layer is sibling-owned `fsgg-sdd init` output — research D4. Real production
// wiring lives in the FS.GG.SDD repo (FR-013); here these literals stand in to demonstrate layering and
// reserved-path avoidance only.
let lifecycleReservedPaths : string list =
    [ ".fsgg/policy.fsgg"
      "work/0001/spec.md"
      "readiness/0001/state.json" ]

// ── real temp directories with a FIXED leaf (for a byte-stable golden) ──

/// The fixed `<App>` leaf name the worked example pins so the emitted paths — and therefore the manifest
/// golden — are byte-stable across runs while the target LOCATION stays unique. `Emit` derives `<App>`
/// from the target's leaf name (data-model §2), so a fixed leaf ⇒ a deterministic manifest.
let fixedAppName = "MyApp"

/// Create a fresh, UNIQUE parent temp directory containing an empty `MyApp` child, and return the child
/// (the scaffold target). Unique location, fixed leaf ⇒ fresh empty target + deterministic emission.
let freshTarget () : string =
    let parent = Path.Combine(Path.GetTempPath(), "fsgg-sdd-ref-" + Guid.NewGuid().ToString("N"))
    let target = Path.Combine(parent, fixedAppName)
    Directory.CreateDirectory target |> ignore
    target

/// Recursively delete a temp target (and its unique parent), ignoring errors.
let cleanup (target: string) : unit =
    try
        let toRemove =
            match Directory.GetParent target with
            | null -> target
            | parent -> parent.FullName

        if Directory.Exists toRemove then
            Directory.Delete(toRemove, true)
    with _ ->
        ()

/// Every file (relative to `root`, '/'-separated) currently under `root`, ascending.
let filesUnder (root: string) : string list =
    if Directory.Exists root then
        Directory.GetFiles(root, "*", SearchOption.AllDirectories)
        |> Array.map (fun f -> Path.GetRelativePath(root, f).Replace('\\', '/'))
        |> Array.sort
        |> Array.toList
    else
        []

// ── request / run builders ──

let runRequest (target: string) (reserved: string list) (provider: TemplateProvider option) : Loop.RunRequest =
    { Request = { Target = target; ReservedPaths = reserved }
      Provider = provider }

// ── the real `dotnet build` runner (real-evidence edge, with a named missing-SDK skip) ──

/// The result of attempting the e2e build: either the .NET SDK ran (carrying its exit code + captured
/// output) or it is absent on this machine (⇒ a NAMED skip, not a failure — research D3, Principle VI).
type BuildAttempt =
    | Built of exitCode: int * output: string
    | SdkMissing of detail: string

/// Run `dotnet build <slnPath>` and capture the outcome. A missing `dotnet` on PATH is reported as
/// `SdkMissing` (the test skips with a named prerequisite rationale) — distinguishable from a real build
/// failure (`Built` with a non-zero exit code).
let dotnetBuild (slnPath: string) : BuildAttempt =
    let psi = ProcessStartInfo("dotnet", sprintf "build \"%s\"" slnPath)
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true
    psi.UseShellExecute <- false

    match Path.GetDirectoryName slnPath with
    | null -> ()
    | dir -> psi.WorkingDirectory <- dir

    try
        match Process.Start psi with
        | null -> SdkMissing "dotnet process did not start"
        | started ->
            use proc = started
            let stdout = proc.StandardOutput.ReadToEnd()
            let stderr = proc.StandardError.ReadToEnd()
            proc.WaitForExit()
            Built(proc.ExitCode, stdout + stderr)
    with
    | :? System.ComponentModel.Win32Exception as e -> SdkMissing e.Message
    | :? InvalidOperationException as e -> SdkMissing e.Message
