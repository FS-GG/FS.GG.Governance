module FS.GG.Governance.ReferenceGateSet.Tests.PackFixture

// The shared "produce the REAL artifact" fixture for every test in this project that needs one.
//
// It was inside ReferenceGateSetPackageTests until #386 added a SECOND consumer (the resolution
// proof, which installs the produced package into a temp consumer). Two copies would have packed
// twice — the pack runs the whole G1–G7 gate first, so that is a real cost — and, worse, they could
// have packed DIFFERENTLY. One `lazy` here means both suites assert over the same bytes.
//
// Everything here produces artifacts through the checked-in production path
// (`pack-reference-gate-set.fsx`); nothing is pre-staged or synthesized (Principle V).

open System
open System.IO
open System.Diagnostics
open System.Reflection
open Expecto

let repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot
let packScript = Path.Combine(repoRoot, "pack-reference-gate-set.fsx")
let samplesFsgg = Path.Combine(repoRoot, "samples", "sdd-reference-gate-set", ".fsgg")

let packagingProject =
    Path.Combine(repoRoot, "packaging", "FS.GG.Governance.ReferenceGateSet", "FS.GG.Governance.ReferenceGateSet.fsproj")

/// Fixed YAML order — the schema-manifest field order (must match the pack script and ADR-0055).
let orderedFiles = [ "governance.yml"; "capabilities.yml"; "policy.yml"; "tooling.yml" ]

/// The configuration THIS assembly was built in, read from the attribute the SDK generates from
/// $(Configuration) — the real build fact, not a guess (`#if DEBUG` would re-encode the assumption
/// that the symbol implies the configuration; a path scrape would re-encode the output layout).
/// The pack gate is shelled from inside our own `dotnet test` run and must target the tree the
/// caller actually built, so it needs this (#148). Fail loud rather than assume a default: a silent
/// "Debug" here is exactly the mismatch that made `dotnet test -c Release` red on a clean tree.
///
/// A function, not a module-level value, deliberately: a `let`-bound value would be computed in the
/// module initializer, so this failure would throw at test DISCOVERY — no failed test, no `Failed!`
/// line, just a crashed suite and a non-zero exit (the invisible failure mode of #149). Called from
/// runPack, it fails as a red test that names the cause.
let buildConfiguration () =
    match Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyConfigurationAttribute>() with
    | null ->
        failwith
            "no AssemblyConfigurationAttribute on the test assembly — cannot tell the pack gate which configuration to run in (is GenerateAssemblyInfo disabled?)"
    | attr -> attr.Configuration

/// Run `dotnet fsi pack-reference-gate-set.fsx <args>` from the repo root; capture (exit, out, err).
let runPack (args: string list) : int * string * string =
    let psi = ProcessStartInfo "dotnet"
    psi.ArgumentList.Add "fsi"
    psi.ArgumentList.Add packScript
    args |> List.iter psi.ArgumentList.Add
    psi.WorkingDirectory <- repoRoot
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true
    psi.UseShellExecute <- false
    // We are already running under `dotnet test` for THIS project; tell the pack gate to run the
    // guard with --no-build so a nested run does not contend on rebuilding the loaded assembly.
    psi.Environment.["FSGG_PACK_GATE_NO_BUILD"] <- "1"
    // …and --no-build only resolves against the tree we were actually built into, so the gate must
    // run in OUR configuration, not a hard-coded one. Set here rather than per call site so every
    // runPack is correct by construction (#148).
    psi.Environment.["FSGG_PACK_GATE_CONFIGURATION"] <- buildConfiguration ()

    match Process.Start psi with
    | null -> failwith "dotnet fsi did not start"
    | p ->
        let out = p.StandardOutput.ReadToEnd()
        let err = p.StandardError.ReadToEnd()
        p.WaitForExit()
        p.ExitCode, out, err

/// Copy the four canonical YAML reference files into a fresh temp `<dir>/.fsgg/`; return <dir> (the
/// directory that CONTAINS `.fsgg/`, i.e. the `--source` value). Real I/O, no mock.
let copyReferenceTo () : string =
    let tmp = Path.Combine(Path.GetTempPath(), "fsgg-pack-test-" + Guid.NewGuid().ToString("N"))
    let fsgg = Path.Combine(tmp, ".fsgg")
    Directory.CreateDirectory fsgg |> ignore

    for f in orderedFiles do
        File.Copy(Path.Combine(samplesFsgg, f), Path.Combine(fsgg, f))

    tmp

// ── The REAL .nupkg, produced once, gated on G1–G7, into a temp feed dir ──
// Packs through the actual production path (gate + pack). Output goes to a temp dir so the run
// neither depends on nor pollutes the shared ~/.local/share/nuget-local feed.

/// The directory holding the produced package — usable directly as a NuGet source.
let producedFeedDir =
    lazy
        (let outDir = Path.Combine(Path.GetTempPath(), "fsgg-pack-out-" + Guid.NewGuid().ToString("N"))
         Directory.CreateDirectory outDir |> ignore
         let code, out, err = runPack [ "--output"; outDir ]

         if code <> 0 then
             failtestf "pack-reference-gate-set.fsx failed (exit %d)\nSTDOUT:\n%s\nSTDERR:\n%s" code out err

         outDir)

/// The produced `.nupkg` path.
let producedNupkg =
    lazy
        (match Directory.GetFiles(producedFeedDir.Value, "FS.GG.Governance.ReferenceGateSet.*.nupkg") with
         | [| p |] -> p
         | other -> failtestf "expected exactly one produced .nupkg in %s; got %A" producedFeedDir.Value other)

/// The version the pack script pinned, read from the produced artifact's own file name — the real
/// shipped fact, never a constant re-typed here.
let producedVersion =
    lazy
        (let stem = "FS.GG.Governance.ReferenceGateSet."

         match Path.GetFileNameWithoutExtension producedNupkg.Value |> Option.ofObj with
         | Some name when name.StartsWith(stem, StringComparison.Ordinal) -> name.Substring stem.Length
         | other -> failtestf "cannot read the produced package's version from its file name: %A" other)
