// pack-and-apicheck.fsx — the 088 breaking-change (API-Compat) DETECTOR entrypoint.
//
// Why this exists: the Governance repo publishes ~70 `FS.GG.Governance.*` packages that consumers pin by
// version range. The auto-update fabric only flows safe versions if a BREAKING change carries a MAJOR bump.
// This script is the I/O-edge SENSOR (Constitution Principle IV): it packs each `IsPackable=true` project,
// compares each freshly-packed assembly against its BASELINE package on the local feed via the .NET SDK's
// ApiCompat / Package Validation, and emits the per-package `ApiBreakSignal` + coverage as DATA. It then
// GRADES that data through the REAL pure library (`Pack.apiCompatibilityFact`/`coverageOutcome`/
// `apiCompatibilityRollup` + `Sensing.parseApiCompatOutput`) — never a re-implementation.
//
// It NEVER decides block/allow and NEVER reddens the build (research D7): the verdict lives in the
// `ApiCompatibility` release rule's `Maturity` (advisory now, `BlockOnRelease` once SC-005 holds). The
// script always exits 0; the gate (the rule, or the CI grading step) renders the decision.
//
// FAIL-SAFE (FR-008): a missing/unreadable baseline or a tool error maps to `Indeterminate` (NEVER a clean
// pass). A never-published package maps to `NoBaseline` (FR-009), reported — never hidden (FR-007).
//
// Usage:
//   dotnet fsi pack-and-apicheck.fsx                 # pack Release + ApiCompat vs feed baseline, human summary
//   dotnet fsi pack-and-apicheck.fsx --json          # machine-readable ApiBreakSignal set + coverage + rollup
//   dotnet fsi pack-and-apicheck.fsx --baseline-feed <dir>   # baseline feed (default ~/.local/share/nuget-local)
//   dotnet fsi pack-and-apicheck.fsx --output <dir>  # pack destination (default: a temp dir)
//   dotnet fsi pack-and-apicheck.fsx --selftest      # exercise the pure grading path on representative signals
//
// PREREQUISITE: build the solution first (`dotnet fsi build.fsx`) — the script `#r`s the built leaf assemblies
// to GRADE through the real pure functions. CI builds, then runs this.

open System
open System.IO
open System.Diagnostics

let scriptDir = __SOURCE_DIRECTORY__
let repoRoot = scriptDir

// ── #r the REAL built leaves so grading uses the actual pure functions (no re-implementation) ──
let private binOf (proj: string) =
    Path.Combine(repoRoot, "src", proj, "bin", "Debug", "net10.0", proj + ".dll")

[ "FS.GG.Governance.Config"
  "FS.GG.Governance.ReleaseRules"
  "FS.GG.Governance.PackEvidence"
  "FS.GG.Governance.ReleaseFactsSensing" ]
|> List.iter (fun p ->
    let dll = binOf p
    if File.Exists dll then
        printfn "#r %s" dll)

#r "src/FS.GG.Governance.Config/bin/Debug/net10.0/FS.GG.Governance.Config.dll"
#r "src/FS.GG.Governance.ReleaseRules/bin/Debug/net10.0/FS.GG.Governance.ReleaseRules.dll"
#r "src/FS.GG.Governance.PackEvidence/bin/Debug/net10.0/FS.GG.Governance.PackEvidence.dll"
#r "src/FS.GG.Governance.ReleaseFactsSensing/bin/Debug/net10.0/FS.GG.Governance.ReleaseFactsSensing.dll"

open FS.GG.Governance.Config.Model
open FS.GG.Governance.ReleaseRules.Model
open FS.GG.Governance.PackEvidence
open FS.GG.Governance.PackEvidence.Model
open FS.GG.Governance.ReleaseFactsSensing

// ── Args ──
let rawArgs = fsi.CommandLineArgs |> Array.toList |> List.tail
let jsonOut = rawArgs |> List.contains "--json"
let selftest = rawArgs |> List.contains "--selftest"

let nugetLocal =
    Path.Combine(Environment.GetFolderPath Environment.SpecialFolder.UserProfile, ".local", "share", "nuget-local")

let rec readFlag flag dflt args =
    match args with
    | f :: v :: _ when f = flag -> v
    | _ :: tail -> readFlag flag dflt tail
    | [] -> dflt

let baselineFeed = readFlag "--baseline-feed" nugetLocal rawArgs
let outputDir = readFlag "--output" (Path.Combine(Path.GetTempPath(), "fsgg-apicheck-" + string (Environment.ProcessId))) rawArgs

// ── Process edge ──
let runDotnet (args: string list) : int * string =
    let psi = ProcessStartInfo("dotnet")
    args |> List.iter psi.ArgumentList.Add
    psi.UseShellExecute <- false
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true
    psi.WorkingDirectory <- repoRoot
    let proc = Process.Start psi
    // Drain both pipes CONCURRENTLY (start the reads before waiting) so a large write to one can never
    // block dotnet on the other — the classic pipe-buffer deadlock a sequential ReadToEnd pair invites.
    let outTask = proc.StandardOutput.ReadToEndAsync()
    let errTask = proc.StandardError.ReadToEndAsync()
    proc.WaitForExit()
    let out = outTask.Result
    let err = errTask.Result
    proc.ExitCode, out + "\n" + err

// ── Baseline resolution on the folder feed ──
// The baseline is the HIGHEST version of the same package id on the feed that is STRICTLY BELOW the packed
// version (the "last published"). Absent ⇒ NoBaseline (FR-009). Parsing is filename-based and total.
let private nupkgStem (file: string) = Path.GetFileNameWithoutExtension file

/// Split a `Some.Package.Id.1.2.3` stem into (id, version) — version is the trailing semver-ish suffix.
let private splitIdVersion (stem: string) : (string * string) option =
    let m = System.Text.RegularExpressions.Regex.Match(stem, @"^(?<id>.+?)\.(?<ver>\d+\.\d+(\.\d+)?([-+].*)?)$")
    if m.Success then Some(m.Groups.["id"].Value, m.Groups.["ver"].Value) else None

let private feedVersions (feed: string) : Map<string, string list> =
    if not (Directory.Exists feed) then
        Map.empty
    else
        Directory.GetFiles(feed, "*.nupkg")
        |> Array.choose (nupkgStem >> splitIdVersion)
        |> Array.fold
            (fun acc (id, ver) ->
                let prev = Map.tryFind id acc |> Option.defaultValue []
                Map.add id (ver :: prev) acc)
            Map.empty

/// Highest feed version of `id` strictly below `packed`, by the library's own semantic comparator (via
/// versionDelta: a baseline candidate is valid iff packed-vs-candidate is a forward change).
let private baselineFor (feed: Map<string, string list>) (id: string) (packed: string) : string option =
    Map.tryFind id feed
    |> Option.defaultValue []
    |> List.filter (fun cand ->
        match Pack.versionDelta (Some cand) (Some packed) with
        | MajorBump
        | MinorOrPatchBump -> true
        | NoForwardChange
        | NoBaselineDelta -> false)
    // pick the highest such candidate (the most recent published baseline below packed)
    |> List.sortWith (fun a b ->
        match Pack.versionDelta (Some a) (Some b) with
        | MajorBump
        | MinorOrPatchBump -> 1
        | _ -> -1)
    |> List.tryLast

// ── ApiCompat invocation (fail-safe) ──
// Runs the SDK ApiCompat tool against the baseline package. If the tool is unavailable or errors, returns an
// `ERROR …` marker line so the parser yields `Indeterminate` (NEVER a clean pass). The normalized marker
// protocol the parser consumes: NOBASELINE / NOTPACKABLE / ERROR <reason> / OK / BREAK …
let private apiCompatOutput (baselineNupkg: string) (packedNupkg: string) : string =
    // The ApiCompat global tool: `apicompat package --baseline <old> <new>`. Job-scoped install in CI.
    let exit, out = runDotnet [ "tool"; "run"; "apicompat"; "--"; "package"; "--baseline-package"; baselineNupkg; packedNupkg ]
    if exit = 0 && (out.Trim() = "" || out.ToUpperInvariant().Contains "NO BREAKING") then
        "OK"
    elif out.ToUpperInvariant().Contains "CP0" then
        out // raw ApiCompat CPxxxx diagnostics — the parser recognizes them as breaks
    else
        // Tool missing / unexpected output ⇒ Indeterminate (fail-safe), naming the reason.
        sprintf "ERROR apicompat unavailable or inconclusive (exit %d)" exit

// ── Per-package sensing ──
type PackageResult =
    { Surface: SurfaceId
      Packed: string
      Baseline: string option
      Signal: ApiBreakSignal
      Delta: VersionDelta }

let private senseOne (feed: Map<string, string list>) (nupkg: string) : PackageResult option =
    match splitIdVersion (nupkgStem nupkg) with
    | None -> None
    | Some(id, packed) ->
        let baseline = baselineFor feed id packed
        let delta = Pack.versionDelta baseline (Some packed)

        let signal =
            match baseline with
            | None -> ApiBreakSignal.NoBaseline
            | Some b ->
                let baselineNupkg =
                    Path.Combine(baselineFeed, sprintf "%s.%s.nupkg" id b)

                Sensing.parseApiCompatOutput (apiCompatOutput baselineNupkg nupkg)

        Some
            { Surface = SurfaceId id
              Packed = packed
              Baseline = baseline
              Signal = signal
              Delta = delta }

// ── Rendering ──
let private signalToken (s: ApiBreakSignal) =
    match s with
    | ApiBreakSignal.NoBreakingChanges -> "no-breaking-changes"
    | ApiBreakSignal.BreakingChanges bs -> sprintf "breaking-changes(%d)" bs.Length
    | ApiBreakSignal.NoBaseline -> "no-baseline"
    | ApiBreakSignal.Indeterminate r -> sprintf "indeterminate(%s)" r
    | ApiBreakSignal.NotPackable -> "not-packable"

let private outcomeToken (o: ApiCompatCoverageOutcome) =
    match o with
    | Checked Met -> "checked:met"
    | Checked Unmet -> "checked:UNMET"
    | Checked Unrecoverable -> "checked:UNRECOVERABLE"
    | NoBaselineYet -> "no-baseline-yet"
    | NotCovered r -> sprintf "not-covered(%s)" r

let private jsonEscape (s: string) = System.Text.Json.JsonSerializer.Serialize s

let private breakOriginToken (o: ApiBreakOrigin) =
    match o with
    | ApiBreakOrigin.Local -> "local"
    | ApiBreakOrigin.Inherited(SurfaceId s) -> "inherited:" + s

let private renderJson (results: PackageResult list) (coverage: ApiCompatCoverage list) (rollup: FactState) : string =
    let pkgJson (r: PackageResult) =
        let breaksJson =
            match r.Signal with
            | ApiBreakSignal.BreakingChanges bs ->
                bs
                |> List.map (fun b -> sprintf "{\"member\":%s,\"origin\":%s}" (jsonEscape b.Member) (jsonEscape (breakOriginToken b.Origin)))
                |> String.concat ","
            | _ -> ""

        let (SurfaceId sid) = r.Surface
        sprintf
            "{\"surface\":%s,\"packed\":%s,\"baseline\":%s,\"signal\":%s,\"breaks\":[%s]}"
            (jsonEscape sid)
            (jsonEscape r.Packed)
            (jsonEscape (r.Baseline |> Option.defaultValue ""))
            (jsonEscape (signalToken r.Signal))
            breaksJson

    let covJson (c: ApiCompatCoverage) =
        let (SurfaceId sid) = c.Surface
        sprintf "{\"surface\":%s,\"outcome\":%s}" (jsonEscape sid) (jsonEscape (outcomeToken c.Outcome))

    let rollupToken =
        match rollup with
        | Met -> "met"
        | Unmet -> "unmet"
        | Unrecoverable -> "unrecoverable"

    sprintf
        "{\"rollup\":%s,\"packages\":[%s],\"coverage\":[%s]}"
        (jsonEscape rollupToken)
        (results |> List.map pkgJson |> String.concat ",")
        (coverage |> List.map covJson |> String.concat ",")

let private renderHuman (results: PackageResult list) (coverage: ApiCompatCoverage list) (rollup: FactState) : string =
    let header = "API compatibility gate (breaking-change → SemVer major) — ADVISORY"

    let covLines =
        coverage
        |> List.map (fun c ->
            let (SurfaceId sid) = c.Surface
            sprintf "  %-48s %s" sid (outcomeToken c.Outcome))

    let breaches =
        results
        |> List.choose (fun r ->
            match Pack.apiCompatibilityFact r.Signal r.Delta with
            | Some Unmet ->
                let (SurfaceId sid) = r.Surface
                Some(sprintf "  %s: breaking change(s) detected vs published %s; requires a MAJOR version bump or revert" sid (Option.defaultValue "?" r.Baseline))
            | Some Unrecoverable ->
                let (SurfaceId sid) = r.Surface
                Some(sprintf "  %s: API comparison indeterminate (%s)" sid (signalToken r.Signal))
            | _ -> None)

    let counts =
        let checkedN = coverage |> List.filter (fun c -> match c.Outcome with Checked _ -> true | _ -> false) |> List.length
        let noBase = coverage |> List.filter (fun c -> c.Outcome = NoBaselineYet) |> List.length
        let notCov = coverage |> List.filter (fun c -> match c.Outcome with NotCovered _ -> true | _ -> false) |> List.length
        sprintf "coverage: %d checked, %d no-baseline-yet, %d not-covered (of %d packages)" checkedN noBase notCov coverage.Length

    [ header
      ""
      counts
      ""
      "per-package coverage:" ]
    @ covLines
    @ (if List.isEmpty breaches then [ ""; "findings: none (advisory)" ] else [ ""; "findings (advisory — visible, non-blocking):" ] @ breaches)
    |> String.concat "\n"

// ── Selftest: exercise the pure grading path on representative signals (real-evidence of the grading) ──
let private runSelftest () : int =
    let localBreak = { Member = "X.foo"; Kind = MemberRemoved; Origin = ApiBreakOrigin.Local }

    let cases =
        [ ApiBreakSignal.NoBreakingChanges, MinorOrPatchBump, Some Met
          ApiBreakSignal.BreakingChanges [ localBreak ], MajorBump, Some Met
          ApiBreakSignal.BreakingChanges [ localBreak ], MinorOrPatchBump, Some Unmet
          ApiBreakSignal.NoBaseline, NoBaselineDelta, Some Met
          ApiBreakSignal.Indeterminate "x", MajorBump, Some Unrecoverable
          ApiBreakSignal.NotPackable, NoBaselineDelta, None ]

    let mutable ok = true

    for (s, d, expected) in cases do
        let actual = Pack.apiCompatibilityFact s d
        let pass = actual = expected
        ok <- ok && pass
        printfn "  %s  %A %A ⇒ %A (expected %A)" (if pass then "PASS" else "FAIL") s d actual expected

    // parser fail-safe
    let pf = Sensing.parseApiCompatOutput "" = ApiBreakSignal.Indeterminate "empty detector output"
    ok <- ok && pf
    printfn "  %s  parser empty ⇒ Indeterminate (fail-safe)" (if pf then "PASS" else "FAIL")
    if ok then 0 else 1

// ── Main ──
if selftest then
    printfn "pack-and-apicheck: --selftest (pure grading path, real library)"
    exit (runSelftest ())

printfn "pack-and-apicheck: packing the solution (Release) to %s" outputDir
Directory.CreateDirectory outputDir |> ignore

// Bound MSBuild node count like build.fsx — the 162-project solution thrashes under default parallelism.
let private maxNodes = max 2 (min 12 (int (ceil (float Environment.ProcessorCount / 4.0))))

let packExit, packLog =
    runDotnet [ "pack"; "FS.GG.Governance.sln"; "-c"; "Release"; sprintf "-m:%d" maxNodes; "-o"; outputDir; "--nologo" ]

if packExit <> 0 then
    // A pack failure is reported but does NOT redden THIS step (the gate decides). Emit an honest signal.
    eprintfn "pack-and-apicheck: dotnet pack failed (exit %d) — reporting all packages NotPackable (fail-safe)" packExit
    eprintfn "%s" (packLog.Substring(0, min 2000 packLog.Length))

let feed = feedVersions baselineFeed

let producedNupkgs =
    if Directory.Exists outputDir then Directory.GetFiles(outputDir, "*.nupkg") |> Array.toList else []

let results = producedNupkgs |> List.choose (senseOne feed)

let coverage =
    Pack.apiCompatCoverage (results |> List.map (fun r -> r.Surface, r.Signal, r.Delta))

let rollup =
    Pack.apiCompatibilityRollup (results |> List.map (fun r -> r.Signal, r.Delta))

if jsonOut then
    printfn "%s" (renderJson results coverage rollup)
else
    printfn "%s" (renderHuman results coverage rollup)

// ADVISORY: always exit 0 — the detector captures the signal; the rule renders the decision (D7).
exit 0
