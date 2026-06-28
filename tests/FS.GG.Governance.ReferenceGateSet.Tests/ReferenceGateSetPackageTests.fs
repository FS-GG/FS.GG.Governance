module FS.GG.Governance.ReferenceGateSet.Tests.ReferenceGateSetPackageTests

// 086: the guard that proves the PUBLISHED artifact is the VALIDATED artifact. It produces the real
// `.nupkg` by invoking the checked-in pack script (`pack-reference-gate-set.fsx`) — never a
// pre-staged file — then asserts over the actual archive and the actual script-emitted version
// (Principle V: real artifacts only, no synthetic fixtures, no re-encoded copy of any rule). It is
// the companion to the 079 `ReferenceGateSetGuard` (which freezes the bundle's invariants): this
// one freezes that what ships equals what those invariants validate. The pack script's own pre-pack
// gate runs ONLY the G1–G7 guard (filtered), so producing the artifact here does not recurse into
// these package tests. No new public F# surface (Tier 2 for the test; the FEATURE is Tier 1 by the
// package contract).

open System
open System.IO
open System.IO.Compression
open System.Diagnostics
open Expecto
open FS.GG.Governance.Tests.Common

let private repoRoot = RepositoryHelpers.repoRoot
let private packScript = Path.Combine(repoRoot, "pack-reference-gate-set.fsx")
let private samplesFsgg = Path.Combine(repoRoot, "samples", "sdd-reference-gate-set", ".fsgg")
let private packagingProject =
    Path.Combine(repoRoot, "packaging", "FS.GG.Governance.ReferenceGateSet", "FS.GG.Governance.ReferenceGateSet.fsproj")

// Fixed bundle order (positional version segments) — must match the pack script and the ADR.
let private orderedFiles = [ "governance.yml"; "capabilities.yml"; "policy.yml"; "tooling.yml" ]
let private contentPrefix = "contentFiles/any/any/.fsgg/"
let private expectedVersion = "1.2.1.1"

/// Run `dotnet fsi pack-reference-gate-set.fsx <args>` from the repo root; capture (exit, out, err).
let private runPack (args: string list) : int * string * string =
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
    match Process.Start psi with
    | null -> failwith "dotnet fsi did not start"
    | p ->
        let out = p.StandardOutput.ReadToEnd()
        let err = p.StandardError.ReadToEnd()
        p.WaitForExit()
        p.ExitCode, out, err

/// Copy the four canonical reference files into a fresh temp `<dir>/.fsgg/`; return <dir> (the
/// directory that CONTAINS `.fsgg/`, i.e. the `--source` value). Real I/O, no mock.
let private copyReferenceTo () : string =
    let tmp = Path.Combine(Path.GetTempPath(), "fsgg-pack-test-" + Guid.NewGuid().ToString("N"))
    let fsgg = Path.Combine(tmp, ".fsgg")
    Directory.CreateDirectory fsgg |> ignore
    for f in orderedFiles do
        File.Copy(Path.Combine(samplesFsgg, f), Path.Combine(fsgg, f))
    tmp

// ── Shared fixture: produce the REAL .nupkg once, gated on G1–G7, into a temp feed dir ──
// Packs through the actual production path (gate + pack). Output goes to a temp dir so the run
// neither depends on nor pollutes the shared ~/.local/share/nuget-local feed.
let private producedNupkg =
    lazy
        (let outDir = Path.Combine(Path.GetTempPath(), "fsgg-pack-out-" + Guid.NewGuid().ToString("N"))
         Directory.CreateDirectory outDir |> ignore
         let code, out, err = runPack [ "--output"; outDir ]
         if code <> 0 then
             failtestf "pack-reference-gate-set.fsx failed (exit %d)\nSTDOUT:\n%s\nSTDERR:\n%s" code out err
         match Directory.GetFiles(outDir, "FS.GG.Governance.ReferenceGateSet.*.nupkg") with
         | [| p |] -> p
         | other -> failtestf "expected exactly one produced .nupkg in %s; got %A" outDir other)

let private entryNames (archive: ZipArchive) : string list =
    archive.Entries |> Seq.map (fun e -> e.FullName) |> List.ofSeq

let private readEntryBytes (archive: ZipArchive) (name: string) : byte[] =
    match archive.GetEntry name with
    | null -> failtestf "archive entry not found: %s" name
    | e ->
        use s = e.Open()
        use ms = new MemoryStream()
        s.CopyTo ms
        ms.ToArray()

[<Tests>]
let packageGuard =
    testList
        "ReferenceGateSetPackage"
        [
          // ── US1 (T005) — byte-identity & content-only over the REAL produced artifact ──

          // SC-002: exactly the four reference files, each byte-identical to the on-disk sample.
          test "T005 Package carries exactly the four .fsgg files byte-identical to source" {
              use archive = ZipFile.OpenRead producedNupkg.Value
              let content = entryNames archive |> List.filter (fun n -> n.StartsWith contentPrefix) |> List.sort
              let expected = orderedFiles |> List.map (fun f -> contentPrefix + f) |> List.sort
              Expect.equal content expected "exactly the four content files at contentFiles/any/any/.fsgg/"
              for f in orderedFiles do
                  let packed = readEntryBytes archive (contentPrefix + f)
                  let onDisk = File.ReadAllBytes(Path.Combine(samplesFsgg, f))
                  Expect.equal packed onDisk (sprintf "%s: packed bytes identical to on-disk sample (0 drift)" f)
          }

          // SC-005: content-only — no lib/ assembly anywhere in the archive.
          test "T005 Package is content-only with no lib assembly" {
              use archive = ZipFile.OpenRead producedNupkg.Value
              let lib = entryNames archive |> List.filter (fun n -> n.StartsWith "lib/")
              Expect.isEmpty lib "a content-only package must carry no lib/<tfm>/ assembly"
          }

          // SC-005: empty dependency group — installing imposes no runtime/assembly dependency.
          test "T005 Package declares no dependency group" {
              use archive = ZipFile.OpenRead producedNupkg.Value
              let nuspecName =
                  entryNames archive |> List.find (fun n -> n.EndsWith ".nuspec")
              let nuspec = System.Text.Encoding.UTF8.GetString(readEntryBytes archive nuspecName)
              Expect.isFalse (nuspec.Contains "<dependencies") "nuspec must declare no <dependencies> group (FR-007)"
          }

          // ── US2 (T008) — gated production & single source ──

          // FR-004/SC-004: a broken G1–G7 invariant on a temp-dir copy (via --source) makes the pack
          // gate fire — non-zero exit, and NO .nupkg is written to the (empty) output dir.
          test "T008 Pack aborts and writes no nupkg when a G1-G7 invariant is broken" {
              let src = copyReferenceTo ()
              let outDir = Path.Combine(Path.GetTempPath(), "fsgg-pack-broken-" + Guid.NewGuid().ToString("N"))
              Directory.CreateDirectory outDir |> ignore
              try
                  // Break G5: defaultProfile must be `light`. Flip it to `strict` (still a declared
                  // profile, so the set loads Valid) — the guard's G5 assertion then fails.
                  let policy = Path.Combine(src, ".fsgg", "policy.yml")
                  let broken = (File.ReadAllText policy).Replace("defaultProfile: light", "defaultProfile: strict")
                  File.WriteAllText(policy, broken)

                  let code, out, err = runPack [ "--source"; src; "--output"; outDir ]
                  Expect.notEqual code 0 (sprintf "pack must fail when G1-G7 are red\nSTDOUT:\n%s\nSTDERR:\n%s" out err)
                  let written = Directory.GetFiles(outDir, "*.nupkg")
                  Expect.isEmpty written "no .nupkg may be written when the gate fails (shipped == validated)"
              finally
                  try Directory.Delete(src, true) with _ -> ()
                  try Directory.Delete(outDir, true) with _ -> ()
          }

          // FR-002: the package draws from samples/sdd-reference-gate-set/.fsgg/ — a single source,
          // no duplicated second copy. Assert the packaging project references that exact path.
          test "T008 Packaging project sources the four files in place from the sample directory" {
              let proj = File.ReadAllText packagingProject
              Expect.stringContains
                  proj
                  "../../samples/sdd-reference-gate-set/.fsgg/*.yml"
                  "the .fsproj must pack the canonical samples in place (no duplicated copy, FR-002)"
          }

          // ── US3 (T011) — deterministic, distinguishable version via the script's actual output ──

          // SC-003: the rule derives exactly 1.2.1.1 from the canonical schema versions (no clock/env).
          test "T011 print-version emits the deterministic 1.2.1.1" {
              let code, out, err = runPack [ "--print-version" ]
              Expect.equal code 0 (sprintf "--print-version must succeed; stderr:\n%s" err)
              Expect.equal (out.Trim()) expectedVersion "derived version is governance.capabilities.policy.tooling = 1.2.1.1"
          }

          // SC-003: a single-segment schemaVersion bump yields a distinguishable version — asserted
          // against the script's ACTUAL emitted value (not a re-scraped rule), over a temp-dir copy.
          test "T011 a policy schemaVersion bump yields a distinguishable 1.2.2.1" {
              let src = copyReferenceTo ()
              try
                  let policy = Path.Combine(src, ".fsgg", "policy.yml")
                  let bumped = (File.ReadAllText policy).Replace("schemaVersion: 1", "schemaVersion: 2")
                  File.WriteAllText(policy, bumped)
                  let code, out, err = runPack [ "--print-version"; "--source"; src ]
                  Expect.equal code 0 (sprintf "--print-version must succeed; stderr:\n%s" err)
                  Expect.equal (out.Trim()) "1.2.2.1" "bumping policy.yml's schemaVersion changes exactly the policy segment"
              finally
                  try Directory.Delete(src, true) with _ -> ()
          }
        ]
