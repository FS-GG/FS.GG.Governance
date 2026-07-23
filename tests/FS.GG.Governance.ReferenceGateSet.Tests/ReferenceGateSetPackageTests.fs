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
open System.Reflection
open Expecto
open FS.GG.Governance.Tests.Common

let private repoRoot = RepositoryHelpers.repoRoot
let private packScript = Path.Combine(repoRoot, "pack-reference-gate-set.fsx")
let private samplesFsgg = Path.Combine(repoRoot, "samples", "sdd-reference-gate-set", ".fsgg")
let private packagingProject =
    Path.Combine(repoRoot, "packaging", "FS.GG.Governance.ReferenceGateSet", "FS.GG.Governance.ReferenceGateSet.fsproj")

// Fixed YAML order — the schema-manifest field order (must match the pack script and ADR-0055).
let private orderedFiles = [ "governance.yml"; "capabilities.yml"; "policy.yml"; "tooling.yml" ]
let private shippedFiles = orderedFiles @ [ "controlled-imports.fsx"; "controlled-imports.json" ]
let private contentPrefix = "contentFiles/any/any/.fsgg/"
// ADR-0055: the in-package schema manifest sits ALONGSIDE the .fsgg set (a sibling of .fsgg/).
let private manifestEntry = "contentFiles/any/any/schema-manifest.json"
// The pinned plain SemVer (ADR-0055), no longer derived from the contained schemaVersions.
let private expectedVersion = "1.4.0"

/// The test's OWN independent parse of a sample's `schemaVersion:` — so an assertion over the packed
/// manifest is evidence about the real on-disk generations, not a re-scrape of the script's rule
/// (Principle V).
let private schemaVersionOf (fileName: string) : int =
    let text = File.ReadAllText(Path.Combine(samplesFsgg, fileName))
    let m =
        System.Text.RegularExpressions.Regex.Match(
            text,
            @"^\s*schemaVersion:\s*(\d+)\s*$",
            System.Text.RegularExpressions.RegexOptions.Multiline)
    if not m.Success then failtestf "no schemaVersion in %s" fileName
    int m.Groups.[1].Value

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
let private buildConfiguration () =
    match Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyConfigurationAttribute>() with
    | null ->
        failwith
            "no AssemblyConfigurationAttribute on the test assembly — cannot tell the pack gate which configuration to run in (is GenerateAssemblyInfo disabled?)"
    | attr -> attr.Configuration

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
    testSequenced
    <| testList
        "ReferenceGateSetPackage"
        [
          // ── US1 (T005) — byte-identity & content-only over the REAL produced artifact ──

          // SC-002: exactly the reference YAML + controlled-import contract files, each
          // byte-identical to the on-disk sample.
          test "T005 Package carries exactly the six .fsgg files byte-identical to source" {
              use archive = ZipFile.OpenRead producedNupkg.Value
              let content = entryNames archive |> List.filter (fun n -> n.StartsWith contentPrefix) |> List.sort
              let expected = shippedFiles |> List.map (fun f -> contentPrefix + f) |> List.sort
              Expect.equal content expected "exactly the six content files at contentFiles/any/any/.fsgg/"
              for f in shippedFiles do
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

          // FR-002: the package draws all six files from samples/sdd-reference-gate-set/.fsgg/ — a
          // single source, no duplicated second copy. Assert each item class references that path.
          test "T008 Packaging project sources all six files in place from the sample directory" {
              let proj = File.ReadAllText packagingProject
              Expect.stringContains
                  proj
                  "../../samples/sdd-reference-gate-set/.fsgg/*.yml"
                  "the .fsproj must pack the four canonical YAML files in place (no duplicated copy, FR-002)"
              Expect.stringContains
                  proj
                  "../../samples/sdd-reference-gate-set/.fsgg/controlled-imports.fsx"
                  "the .fsproj must pack the canonical controlled-import verifier in place"
              Expect.stringContains
                  proj
                  "../../samples/sdd-reference-gate-set/.fsgg/controlled-imports.json"
                  "the .fsproj must pack the canonical controlled-import starter manifest in place"
          }

          // ── US3 (T011) — plain SemVer (ADR-0055): pinned, deterministic, decoupled from schemaVersions ──

          // ADR-0055: --print-version emits the pinned plain SemVer verbatim (no clock/env, no
          // derivation from the contained schemaVersions).
          test "T011 print-version emits the pinned plain SemVer 1.4.0" {
              let code, out, err = runPack [ "--print-version" ]
              Expect.equal code 0 (sprintf "--print-version must succeed; stderr:\n%s" err)
              Expect.equal (out.Trim()) expectedVersion "the version is the pinned plain SemVer (ADR-0055), not a schema-derived tuple"
          }

          // ADR-0055's core decision — the version no longer encodes the schemaVersions. A
          // schemaVersion bump on a --source copy leaves the version UNCHANGED (still the pinned
          // SemVer) and instead moves the in-package MANIFEST. This is the exact inversion of the
          // retired ADR-0007 rule, under which the same bump changed the version (1.2.1.1 → 1.2.2.1).
          test "T011 a schemaVersion bump moves the manifest, NOT the version" {
              let src = copyReferenceTo ()
              try
                  let policy = Path.Combine(src, ".fsgg", "policy.yml")
                  let bumped = (File.ReadAllText policy).Replace("schemaVersion: 1", "schemaVersion: 2")
                  File.WriteAllText(policy, bumped)

                  let vcode, vout, verr = runPack [ "--print-version"; "--source"; src ]
                  Expect.equal vcode 0 (sprintf "--print-version must succeed; stderr:\n%s" verr)
                  Expect.equal (vout.Trim()) expectedVersion "a schemaVersion bump must NOT change the pinned version (ADR-0055)"

                  let mcode, mout, merr = runPack [ "--print-manifest"; "--source"; src ]
                  Expect.equal mcode 0 (sprintf "--print-manifest must succeed; stderr:\n%s" merr)
                  Expect.stringContains mout "\"policy\": 2" "the bumped policy generation must show in the manifest"
              finally
                  try Directory.Delete(src, true) with _ -> ()
          }

          // ── ADR-0055 — the in-package schema manifest, over the REAL produced artifact ──

          // The manifest ships at contentFiles/any/any/schema-manifest.json — ALONGSIDE the .fsgg set,
          // never inside it, so the packed `.fsgg/` stays byte-identical to source (T005 counts
          // exactly the six reference-set files there).
          test "ADR-0055 Package carries schema-manifest.json alongside .fsgg, not inside it" {
              use archive = ZipFile.OpenRead producedNupkg.Value
              let names = entryNames archive
              Expect.contains names manifestEntry "the manifest must be packed at contentFiles/any/any/schema-manifest.json"
              Expect.isFalse
                  (names |> List.exists (fun n -> n.StartsWith contentPrefix && n.EndsWith "schema-manifest.json"))
                  "the manifest must be a SIBLING of .fsgg/, not a fifth file inside it (byte-identity, T005)"
          }

          // The manifest records the four contained schemaVersion GENERATIONS, keyed by file stem,
          // matching the real on-disk samples (independently parsed here — Principle V).
          test "ADR-0055 schema-manifest.json records the on-disk schema generations" {
              use archive = ZipFile.OpenRead producedNupkg.Value
              let manifest = System.Text.Encoding.UTF8.GetString(readEntryBytes archive manifestEntry)
              for f in orderedFiles do
                  let key = Path.GetFileNameWithoutExtension f
                  let gen = schemaVersionOf f
                  Expect.stringContains
                      manifest
                      (sprintf "\"%s\": %d" key gen)
                      (sprintf "manifest must record %s at its on-disk schemaVersion %d" key gen)
          }

          // ── #148 — the nested gate runs in the CALLER's configuration, not a hard-coded one ──
          //
          // These assert the ACTUAL emitted gate command (via the script's --print-gate-command dry
          // run), not a scraped duplicate of the rule. They are the reason this fix is guarded AT
          // ALL: CI is Debug-only (#150), and in Debug a re-hard-coded `-c Debug` is
          // indistinguishable from correct threading — which is exactly how #148 survived on a green
          // main. Passing an EXPLICIT configuration that differs from the ambient one makes the
          // Debug lane able to see the difference. Dry runs: no build, no gate, no pack.

          test "#148 the gate targets the configuration the caller asked for, not a hard-coded Debug" {
              let code, out, err = runPack [ "--print-gate-command"; "--configuration"; "Release" ]
              Expect.equal code 0 (sprintf "--print-gate-command must succeed; stderr:\n%s" err)
              Expect.stringContains out "-c Release" "an explicit --configuration must reach the nested gate"
              // The flag must also WIN over the FSGG_PACK_GATE_CONFIGURATION runPack sets to OUR
              // configuration — otherwise this assertion would pass vacuously in a Release run.
              Expect.isFalse (out.Contains "-c Debug") "the explicit flag wins over the caller's env var"
          }

          // `--flag=value` is the form dotnet itself accepts, so it is the form habit produces. It
          // used to be silently ignored — falling back to Debug, i.e. the very mismatch #148 fixes.
          test "#148 the gate honours the --configuration=<cfg> form" {
              let code, out, err = runPack [ "--print-gate-command"; "--configuration=Release" ]
              Expect.equal code 0 (sprintf "--print-gate-command must succeed; stderr:\n%s" err)
              Expect.stringContains out "-c Release" "--configuration=Release must reach the nested gate"
          }

          // With no explicit flag, the gate must run in the configuration the CALLER was built in —
          // which runPack passes via FSGG_PACK_GATE_CONFIGURATION. This is the assertion that goes
          // red in a Release run against the original hard-coded script.
          test "#148 with no flag, the gate runs in the configuration this test assembly was built in" {
              let code, out, err = runPack [ "--print-gate-command" ]
              Expect.equal code 0 (sprintf "--print-gate-command must succeed; stderr:\n%s" err)
              Expect.stringContains
                  out
                  (sprintf "-c %s" (buildConfiguration ()))
                  "a --no-build gate only resolves against the tree its caller actually built"
          }

          // Fail loud, never guess (Principle VI): a dropped value silently gating the wrong tree is
          // the failure this whole item is about.
          test "#148 a --configuration with no value fails loudly instead of defaulting" {
              let code, out, _ = runPack [ "--print-gate-command"; "--configuration" ]
              Expect.notEqual code 0 (sprintf "a valueless --configuration must not silently default\nSTDOUT:\n%s" out)
          }

          test "#148 an unknown configuration fails loudly instead of gating an unintended tree" {
              let code, out, _ = runPack [ "--print-gate-command"; "--configuration"; "Relase" ]
              Expect.notEqual code 0 (sprintf "a typo'd configuration must be refused, not forwarded to MSBuild\nSTDOUT:\n%s" out)
          }
        ]
