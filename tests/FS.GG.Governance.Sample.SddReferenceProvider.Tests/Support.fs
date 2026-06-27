module FS.GG.Governance.Sample.SddReferenceProvider.Tests.Support

open System
open System.Diagnostics
open System.IO
open FS.GG.Governance.Scaffold
open FS.GG.Governance.Scaffold.Model
// 074: findRepoRoot consolidated into the shared RepositoryHelpers (sln||slnx superset).
let repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot

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

/// The result of attempting the e2e build: the SDK ran (carrying its exit code + captured output), it is
/// absent on this machine (⇒ a NAMED skip, not a failure), or it did not return within the finite budget
/// and its process tree was terminated (⇒ a NAMED timeout skip — research D3, Principle VI). The three
/// cases stay total and mutually exclusive so a timeout can never silently become a pass.
type BuildAttempt =
    | Built of exitCode: int * output: string
    | SdkMissing of detail: string
    | TimedOut of budget: TimeSpan * partialOutput: string

// ── the finite build budget and the two named timing margins (data-model §2, research D5) ──

/// The finite ceiling a real-evidence build may consume before it is cut off as `TimedOut`. Default 120 s;
/// overridable via `FSGG_BUILD_BUDGET_SECONDS` (absent / non-numeric / ≤ 0 ⇒ the 120 s default — a
/// malformed override NEVER yields an unbounded wait, FR-001/FR-010).
let buildBudget : TimeSpan =
    let fallback = TimeSpan.FromSeconds 120.

    match Environment.GetEnvironmentVariable "FSGG_BUILD_BUDGET_SECONDS" with
    | null -> fallback
    | raw ->
        match Double.TryParse(raw, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture) with
        | true, secs when secs > 0. -> TimeSpan.FromSeconds secs
        | _ -> fallback

/// The bounded post-`Kill` drain wait in `runBounded`, so reading the partial output after a tree-kill
/// cannot itself block. Distinct from `boundAssertionMargin` below.
let killDrainMargin : TimeSpan = TimeSpan.FromSeconds 5.

/// The assertion tolerance the forced-stall test (`BoundedBuildTests`) allows over `budget` when checking
/// the bound. Distinct from the drain margin so the two "small margin" notions are unambiguous (FR-010).
let boundAssertionMargin : TimeSpan = TimeSpan.FromSeconds 2.

/// Run `exe args` (optionally in `workingDir`), capturing stdout+stderr ASYNCHRONOUSLY and waiting at most
/// `budget`. Async capture (`OutputDataReceived`/`ErrorDataReceived` + `BeginOutputReadLine` *after* `Start`)
/// defeats the `ReadToEnd()` deadlock (research D1): a synchronous `ReadToEnd()` blocks until the child
/// closes its pipe — which a hung build never does — so a bare `WaitForExit(budget)` after it would be dead
/// code; the hang is in the read. On overrun the WHOLE process tree is killed (research D2 — `dotnet build`
/// fans out MSBuild worker nodes; killing only the parent leaves orphans) and the attempt is `TimedOut`; a
/// start failure (`Process.Start` null / `Win32Exception` / `InvalidOperationException`) ⇒ `SdkMissing`
/// (preserves FR-004); otherwise `Built` with the real exit code.
///
/// `onStarted` is invoked SYNCHRONOUSLY on the caller's thread with the spawned PID immediately after a
/// successful `Start` (before the bounded wait). It lets a caller identify the EXACT child by PID — the only
/// race-free way to assert the spawned process was terminated when many builds run concurrently (a scan by
/// process *name* would catch unrelated siblings, FR-010). Production callers pass `ignore`.
let runBounded
    (exe: string)
    (args: string)
    (workingDir: string option)
    (budget: TimeSpan)
    (onStarted: int -> unit)
    : BuildAttempt =
    let psi = ProcessStartInfo(exe, args)
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true
    psi.UseShellExecute <- false

    match workingDir with
    | Some dir -> psi.WorkingDirectory <- dir
    | None -> ()

    try
        match Process.Start psi with
        | null -> SdkMissing "process did not start"
        | started ->
            use proc = started
            onStarted proc.Id
            // A `mutable` StringBuilder behind the two async pipe handlers is the plainest BCL pattern
            // (Principle III) — disclosed here at the use site. The handlers fire on threadpool threads, so
            // appends are serialized under `lock`.
            let captured = Text.StringBuilder()

            let append (e: DataReceivedEventArgs) =
                match e.Data with
                | null -> ()
                | line -> lock captured (fun () -> captured.AppendLine line |> ignore)

            proc.OutputDataReceived.Add append
            proc.ErrorDataReceived.Add append
            proc.BeginOutputReadLine()
            proc.BeginErrorReadLine()

            if proc.WaitForExit(int budget.TotalMilliseconds) then
                // Flush any in-flight async output before reading the accumulator (documented .NET step).
                proc.WaitForExit()
                Built(proc.ExitCode, captured.ToString())
            else
                proc.Kill(entireProcessTree = true)
                // Bounded drain: reading the partial output after the tree-kill must not itself block.
                proc.WaitForExit(int killDrainMargin.TotalMilliseconds) |> ignore
                TimedOut(budget, captured.ToString())
    with
    | :? System.ComponentModel.Win32Exception as e -> SdkMissing e.Message
    | :? InvalidOperationException as e -> SdkMissing e.Message

/// Run `dotnet build <slnPath>` bounded by `buildBudget`. `-maxcpucount:1 --disable-build-servers` collapses
/// the MSBuild worker fan-out and stops persistent build-server processes (research D6, FR-007) — the
/// resource contention that wedged the surrounding suite during 077. A thin wrapper over `runBounded`: a
/// missing SDK ⇒ `SdkMissing`, an overrun ⇒ `TimedOut`, otherwise `Built` with the real exit code.
let dotnetBuild (slnPath: string) : BuildAttempt =
    let workingDir =
        match Path.GetDirectoryName slnPath with
        | null -> None
        | dir -> Some dir

    runBounded
        "dotnet"
        (sprintf "build \"%s\" -maxcpucount:1 --disable-build-servers" slnPath)
        workingDir
        buildBudget
        ignore

// ── run-configuration gate: fast default, real evidence behind an explicit opt-in / CI (research D4) ──

/// True iff the heavyweight real-evidence build should run: `FSGG_REAL_EVIDENCE` is exactly `1`, OR `CI` is
/// *truthy* (set and, trimmed + case-insensitive, NOT one of "" / "0" / "false") — so GitHub Actions' CI=true
/// and a bare CI=1 both enable, while CI=0 / CI=false / unset do not (the canonical CI full-evidence path,
/// FR-005). The env read is kept in this one place so the gate is testable.
let realEvidenceEnabled () : bool =
    let truthy =
        match Environment.GetEnvironmentVariable "CI" with
        | null -> false
        | raw ->
            match raw.Trim().ToLowerInvariant() with
            | ""
            | "0"
            | "false" -> false
            | _ -> true

    Environment.GetEnvironmentVariable "FSGG_REAL_EVIDENCE" = "1" || truthy

/// The NAMED opt-out skip message used when `realEvidenceEnabled ()` is false (FR-009).
let realEvidenceSkipReason : string =
    "REAL-EVIDENCE OPT-OUT: set FSGG_REAL_EVIDENCE=1 (or run under CI) to exercise the real dotnet build"
