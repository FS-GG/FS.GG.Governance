// pack-reference-gate-set.fsx — produce the content-only `FS.GG.Governance.ReferenceGateSet`
// NuGet package (086), gated on the existing G1–G7 reference-set guard.
//
// Why this exists: the validated reference `.fsgg` gate set under
// samples/sdd-reference-gate-set/.fsgg/ is published as ONE versioned source of truth for the
// Templates overlay drift gate (Templates#14). This script is the single place that (a) derives
// the package version deterministically from the four contained `schemaVersion` declarations,
// (b) refuses to pack when the G1–G7 invariants are red (the shipped artifact is provably the
// tested artifact, FR-004), and (c) packs to ~/.local/share/nuget-local/ (constitution). It
// mirrors the build.fsx idiom: linear build step, Process.Start confined to the script edge, a
// `--print-version` dry-run hook (like build.fsx's `--print-command`) so the guard test asserts
// the ACTUAL emitted version rather than a scraped duplicate of the rule, and loud non-zero exit
// discipline (Principle VI).
//
// Usage:
//   dotnet fsi pack-reference-gate-set.fsx                      # gate on G1–G7, then pack -> nuget-local
//   dotnet fsi pack-reference-gate-set.fsx --print-version      # derive + print the version, no gate, no pack
//   dotnet fsi pack-reference-gate-set.fsx --source <dir>       # read <dir>/.fsgg/*.yml instead of the default
//   dotnet fsi pack-reference-gate-set.fsx --output <dir>       # pack into <dir> instead of nuget-local
//   dotnet fsi pack-reference-gate-set.fsx --no-gate            # pack without the G1–G7 gate (CI runs the gate separately)
//   dotnet fsi pack-reference-gate-set.fsx --configuration Release   # run the G1–G7 gate in Release (default: Debug)
//   dotnet fsi pack-reference-gate-set.fsx --print-gate-command  # print the gate's dotnet args, no gate, no pack
//
// `--source <dir>`: <dir> is the directory that CONTAINS the `.fsgg/` folder; the script reads
// <dir>/.fsgg/*.yml. Default <dir> = samples/sdd-reference-gate-set. It feeds BOTH the
// version-derivation AND the G1–G7 gate (via the FSGG_REFERENCE_GATE_SET_DIR env var the 079 guard
// honors), so the guard test can point a temp-dir copy — bumped `schemaVersion` (SC-003) or a
// broken invariant (FR-004) — at the script without touching the canonical source. (The packed
// CONTENT is always the canonical samples — the .fsproj packs them in place; `--source` is a test
// hook for version + gate, not a way to produce an alternate package.)
//
// `--output <dir>`: pack destination; default ~/.local/share/nuget-local/. The guard test packs
// into a temp dir so an automated run neither depends on nor pollutes the shared local feed.
//
// `--configuration <cfg>` (or FSGG_PACK_GATE_CONFIGURATION): the configuration the G1–G7 GATE runs
// in; default Debug. It does NOT affect the pack, which is always Release — the shipped artifact is
// content-only, so no gate configuration can change its bytes. This is an input, not a constant,
// because the gate is sometimes shelled from INSIDE a `dotnet test` run of the guard project (the
// package tests do exactly that, with FSGG_PACK_GATE_NO_BUILD) — and a `--no-build` gate pinned to
// Debug points at a Debug tree a Release caller never built, so `dotnet test -c Release` was red on
// a clean tree while Debug-only CI stayed green (#148). The caller passes the configuration it was
// built in, and the two halves agree about which tree is in play.

open System
open System.IO
open System.Text.RegularExpressions
open System.Diagnostics

// ── Locations ──
// Resolve paths relative to THIS script's directory so the script works from any CWD.
let scriptDir = __SOURCE_DIRECTORY__
let repoRoot = scriptDir // the script lives at the repo root, next to build.fsx

let defaultSource = Path.Combine(repoRoot, "samples", "sdd-reference-gate-set")

let packagingProject =
    Path.Combine(
        repoRoot,
        "packaging",
        "FS.GG.Governance.ReferenceGateSet",
        "FS.GG.Governance.ReferenceGateSet.fsproj"
    )

let guardTestProject =
    Path.Combine(
        repoRoot,
        "tests",
        "FS.GG.Governance.ReferenceGateSet.Tests",
        "FS.GG.Governance.ReferenceGateSet.Tests.fsproj"
    )

// Pack output: the constitution-mandated local feed.
let nugetLocal =
    Path.Combine(Environment.GetFolderPath Environment.SpecialFolder.UserProfile, ".local", "share", "nuget-local")

/// Fail loud and closed (Principle VI): write an actionable message to stderr and exit non-zero.
let fail (msg: string) : 'a =
    eprintfn "pack-reference-gate-set: %s" msg
    exit 1

// ── Arg parsing ──
let rawArgs = fsi.CommandLineArgs |> Array.toList |> List.tail // drop the script path

let printVersionOnly = rawArgs |> List.contains "--print-version"
let printGateCommandOnly = rawArgs |> List.contains "--print-gate-command"
let noGate = rawArgs |> List.contains "--no-gate"

/// Read a `--flag <value>` option. ONE scanner for all three of them: they were three copies of the
/// same recursion, so a parsing bug had to be fixed three times (and was, in exactly one).
///
/// Accepts BOTH `--flag value` and `--flag=value` — the latter because `dotnet` itself accepts it,
/// so it is the form a user's habit produces. A flag present with NO value is a hard failure, never
/// a silent fallback to the default: these options decide which tree gets gated and where the
/// artifact lands, and a silently-dropped `--configuration` is precisely the quiet Debug/Release
/// mismatch #148 was.
let rec readFlag (name: string) (args: string list) : string option =
    let eq = name + "="

    match args with
    | k :: v :: _ when k = name -> Some v
    | [ k ] when k = name -> fail (sprintf "%s requires a value (e.g. `%s Release`) — refusing to guess." name name)
    | k :: _ when k.StartsWith(eq, StringComparison.Ordinal) ->
        match k.Substring eq.Length with
        | "" -> fail (sprintf "%s= was given an empty value — refusing to guess." name)
        | v -> Some v
    | _ :: tail -> readFlag name tail
    | [] -> None

// `--source <dir>` (the directory that contains `.fsgg/`); default = the canonical sample dir.
let sourceDir = readFlag "--source" rawArgs |> Option.defaultValue defaultSource
let fsggDir = Path.Combine(sourceDir, ".fsgg")

// `--output <dir>`: pack destination; default = the constitution-mandated local feed.
let outputDir = readFlag "--output" rawArgs |> Option.defaultValue nugetLocal

// `--configuration <cfg>`: the configuration the G1–G7 gate runs in (NOT the pack, which is always
// Release). Resolution order: explicit flag, then the FSGG_PACK_GATE_CONFIGURATION env var the
// package guard sets alongside FSGG_PACK_GATE_NO_BUILD (a nested gate must run against the tree its
// caller actually built), then Debug — the standalone/CI default, unchanged.
//
// Validated against the configurations this repo actually builds. MSBuild treats `Configuration` as
// a free-form property, so an unvalidated typo (`Relase`) would quietly BUILD and gate a tree nobody
// intended — a green gate over the wrong artifact. Fail loud instead (Principle VI).
let knownConfigurations = [ "Debug"; "Release" ]

let gateConfiguration =
    let resolved =
        match readFlag "--configuration" rawArgs with
        | Some cfg -> cfg.Trim()
        | None ->
            match Environment.GetEnvironmentVariable "FSGG_PACK_GATE_CONFIGURATION" with
            | v when String.IsNullOrWhiteSpace v -> "Debug"
            | v -> v.Trim()

    if not (knownConfigurations |> List.contains resolved) then
        fail (
            sprintf
                "unknown configuration '%s' — expected one of: %s"
                resolved
                (String.concat ", " knownConfigurations)
        )

    resolved

// ── Version derivation (FR-006/SC-003) ──
// Version = "{governance}.{capabilities}.{policy}.{tooling}" — each segment is the file's own
// `schemaVersion`, in fixed bundle order (manifest root first). A bump to any one file changes
// EXACTLY one segment, so the version is deterministic, reversible, and distinguishable on every
// bump. Recorded as an ADR in FS-GG/.github (the numbering rule is itself a contract).
// Fixed file order — do not reorder (the version segments are positional).
let private orderedFiles = [ "governance.yml"; "capabilities.yml"; "policy.yml"; "tooling.yml" ]

let private schemaVersionRegex = Regex(@"^\s*schemaVersion:\s*(\d+)\s*$", RegexOptions.Multiline)

/// Read the single `schemaVersion:` integer from one file under <source>/.fsgg/.
/// Distinguishes a missing file / missing-or-unparseable line (malformed INPUT) from a tool defect
/// by naming the exact file and what was expected (Principle VI).
let private readSchemaVersion (fileName: string) : int =
    let path = Path.Combine(fsggDir, fileName)
    if not (File.Exists path) then
        fail (sprintf "reference file not found: %s (expected under %s)" fileName fsggDir)
    let text = File.ReadAllText path
    let m = schemaVersionRegex.Match text
    if not m.Success then
        fail (sprintf "no parseable `schemaVersion: <int>` line in %s — cannot derive a package version" path)
    match Int32.TryParse m.Groups.[1].Value with
    | true, v -> v
    | false, _ -> fail (sprintf "schemaVersion in %s is not an integer: %s" path m.Groups.[1].Value)

let derivedVersion () : string =
    orderedFiles
    |> List.map readSchemaVersion
    |> List.map string
    |> String.concat "."

// ── Process edge (mirrors build.fsx) ──
/// Run `dotnet` with the given args (and optional extra env vars); return the exit code. I/O lives
/// only here, at the script edge.
let runDotnetWithEnv (env: (string * string) list) (args: string list) : int =
    let psi = ProcessStartInfo("dotnet")
    args |> List.iter psi.ArgumentList.Add
    env |> List.iter (fun (k, v) -> psi.Environment.[k] <- v)
    psi.UseShellExecute <- false
    psi.WorkingDirectory <- repoRoot
    printfn "pack-reference-gate-set: dotnet %s" (String.Join(" ", args))
    let proc = Process.Start psi
    proc.WaitForExit()
    proc.ExitCode

let runDotnet = runDotnetWithEnv []

// ── Main ──

// FSGG_PACK_GATE_NO_BUILD: when the package guard shells this script from INSIDE its own
// `dotnet test` run, a nested `dotnet test` that rebuilds the already-loaded guard assembly
// would contend on the running DLL. The caller (which has just built the project) sets this so
// the gate runs `--no-build` against the existing assembly — no rebuild, no contention. Unset
// for standalone/CI use, where the gate builds the guard itself.
let noBuildGate =
    match Environment.GetEnvironmentVariable "FSGG_PACK_GATE_NO_BUILD" with
    | null | "" | "0" -> false
    | _ -> true

let gateArgs =
    [ "test"
      guardTestProject
      "-c"
      gateConfiguration
      "--filter"
      "FullyQualifiedName~ReferenceGateSetGuard" ]
    @ (if noBuildGate then [ "--no-build" ] else [])

if printGateCommandOnly then
    // Dry run: emit ONLY the gate's `dotnet` args, so the guard test can assert the ACTUAL emitted
    // command rather than a scraped duplicate of the rule (the `--print-version` idiom, and
    // build.fsx's `--print-command` before it). This is what lets the DEBUG-ONLY CI lane observe
    // that the gate honours a Release caller: without it, a re-hard-coded `-c Debug` is
    // indistinguishable from correct behaviour in Debug, which is how #148 survived. No gate, no pack.
    printfn "%s" (String.Join(" ", gateArgs))
    exit 0

let version = derivedVersion ()

if printVersionOnly then
    // Dry run: emit ONLY the derived version on its own line so the guard test can capture it
    // verbatim. No gate, no pack.
    printfn "%s" version
    exit 0

// G1–G7 gate (FR-004): run ONLY the reference-set guard, never the whole suite (which now also
// contains the package tests that produce the not-yet-packed .nupkg — running them here would
// deadlock). On red, abort BEFORE any `.nupkg` is written. CI runs the same script.
if not noGate then
    printfn "pack-reference-gate-set: running the G1–G7 reference-set guard before packing…"
    // Point the 079 guard at <source> via the env var it honors (default = canonical), so a
    // `--source` temp copy with a broken invariant makes the gate fire (FR-004) without mutating
    // the canonical samples.
    let gateExit =
        runDotnetWithEnv [ ("FSGG_REFERENCE_GATE_SET_DIR", Path.GetFullPath sourceDir) ] gateArgs
    if gateExit <> 0 then
        fail (
            sprintf
                "G1–G7 reference-set guard failed (exit %d) — refusing to pack. The shipped artifact must be the validated artifact (FR-004). Fix the guard, then re-run."
                gateExit
        )

// Pack to the output dir at the derived version. IncludeBuildOutput=false in the .fsproj keeps the
// .nupkg content-only (no lib/, no dependency group).
Directory.CreateDirectory outputDir |> ignore

let packExit =
    runDotnet
        [ "pack"
          packagingProject
          "-c"
          "Release"
          (sprintf "-p:Version=%s" version)
          "-o"
          outputDir ]

if packExit <> 0 then
    fail (sprintf "dotnet pack failed (exit %d)" packExit)

printfn
    "pack-reference-gate-set: wrote %s"
    (Path.Combine(outputDir, sprintf "FS.GG.Governance.ReferenceGateSet.%s.nupkg" version))

exit 0
