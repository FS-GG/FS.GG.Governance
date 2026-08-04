module FS.GG.Governance.ReferenceGateSet.Tests.ReferenceGateSetResolutionTests

// #386 AC2/AC4 — THE CONSUMER RESOLUTION CONTRACT, proven against the INSTALLED PACKAGE.
//
// The issue's measured gap: the package's payload ships under `contentFiles/any/any/`, which a
// modern SDK-style `PackageReference` does NOT materialize into a consumer's working tree, and the
// package carried no `build`/`buildTransitive` targets. So there was no verb at all by which an
// ordinary repository could obtain the published profile — only a hand copy out of a clone of this
// repo, which is not a resolution of the package.
//
// These tests do the whole thing for real, and nothing here reads `samples/`:
//
//   R1  a fresh consumer with ONLY a `PackageReference` and `dotnet restore` gets NO `.fsgg/` —
//       the gap is measured, not assumed, so R2 has a subject and cannot pass vacuously;
//   R2  `dotnet msbuild -t:FsggResolveReferenceGateSet` materializes the profile, byte-identical to
//       the package's own payload;
//   R3  the resolved `.fsgg/` loads `Valid` through the real `Config.Loader.loadAndValidate`;
//   R4  ADR-0009 §Decision 3 survives the new route: a product that EDITS its resolved copy to
//       delete and downgrade the floor still inherits it at full strength. This is the constraint
//       that defeated the package route the first time, so it is executed, not argued.
//
// "Clean clone" is enforced by construction: the consumer lives in a fresh temp directory outside
// this repo (so no `Directory.Build.props` reaches it), its `nuget.config` `<clear/>`s every
// inherited source, and `NUGET_PACKAGES` points at a fresh store — so the package under test is
// genuinely installed from the produced artifact and cannot be served by an earlier local build.

open System
open System.IO
open System.IO.Compression
open System.Diagnostics
open Expecto
open FS.GG.Governance.Config
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.Inheritance
open FS.GG.Governance.ReferenceGateSet.Tests.PackFixture

let private shippedFiles =
    orderedFiles @ [ "controlled-imports.fsx"; "controlled-imports.json" ]

let private contentPrefix = "contentFiles/any/any/.fsgg/"

// ── The temp consumer ────────────────────────────────────────────────────────────────────────

/// A consumer project with no source, no language content, and exactly one `PackageReference` —
/// the minimum an ordinary repository needs in order to RESOLVE a profile. Deliberately a `.csproj`:
/// an F# project would drag `FSharp.Core` into the restore graph and need a second feed, which would
/// make the hermetic `<clear/>` below impossible to keep.
let private consumerProject (version: string) =
    sprintf
        """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
    <RestorePackagesWithLockFile>false</RestorePackagesWithLockFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="FS.GG.Governance.ReferenceGateSet" Version="%s" />
  </ItemGroup>
</Project>
"""
        version

/// `<clear/>` removes every inherited source (the machine-level nuget.org entry included), so the
/// only package this consumer can possibly install is the one we just produced.
///
/// `packageSourceMapping` needs its OWN `<clear/>`: mapping is a SEPARATE inherited section, and
/// clearing sources does not clear it. Without this, an inherited mapping that does not name
/// `fsgg-produced` excludes our feed from consideration entirely and restore fails NU1100 with
/// "the following source(s) were not considered" — measured, not theorised.
let private consumerNugetConfig (feedDir: string) =
    sprintf
        """<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="fsgg-produced" value="%s" />
  </packageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="fsgg-produced">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
"""
        feedDir

let private runDotnet (workingDir: string) (packagesDir: string) (args: string list) : int * string * string =
    let psi = ProcessStartInfo "dotnet"
    args |> List.iter psi.ArgumentList.Add
    psi.WorkingDirectory <- workingDir
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true
    psi.UseShellExecute <- false
    // A fresh package store: "installed from the produced artifact", not "already on this machine".
    psi.Environment.["NUGET_PACKAGES"] <- packagesDir
    psi.Environment.["DOTNET_CLI_TELEMETRY_OPTOUT"] <- "1"
    psi.Environment.["DOTNET_NOLOGO"] <- "1"
    psi.Environment.["MSBUILDDISABLENODEREUSE"] <- "1"
    // The consumer must not inherit this repo's credentialed org feed by environment either.
    psi.Environment.["FSGG_PACKAGES_ACTOR"] <- ""
    psi.Environment.["FSGG_PACKAGES_READ_TOKEN"] <- ""

    match Process.Start psi with
    | null -> failwith "dotnet did not start"
    | p ->
        let out = p.StandardOutput.ReadToEnd()
        let err = p.StandardError.ReadToEnd()
        p.WaitForExit()
        p.ExitCode, out, err

/// A restored temp consumer of the produced package: (consumerDir, packagesDir). Built once and
/// shared, because a restore is the expensive part; each test that mutates the tree works on its
/// own copy of the resolved output rather than on this one.
let private restoredConsumer =
    lazy
        (let root = Path.Combine(Path.GetTempPath(), "fsgg-consumer-" + Guid.NewGuid().ToString("N"))
         let consumerDir = Path.Combine(root, "consumer")
         let packagesDir = Path.Combine(root, "packages")
         Directory.CreateDirectory consumerDir |> ignore
         Directory.CreateDirectory packagesDir |> ignore

         File.WriteAllText(Path.Combine(consumerDir, "Consumer.csproj"), consumerProject producedVersion.Value)
         File.WriteAllText(Path.Combine(consumerDir, "nuget.config"), consumerNugetConfig producedFeedDir.Value)

         let code, out, err = runDotnet consumerDir packagesDir [ "restore" ]

         if code <> 0 then
             failtestf
                 "a clean consumer could not restore FS.GG.Governance.ReferenceGateSet %s from the produced feed (exit %d)\nSTDOUT:\n%s\nSTDERR:\n%s"
                 producedVersion.Value
                 code
                 out
                 err

         consumerDir, packagesDir)

/// Resolve into a FRESH copy of the restored consumer, so a test that edits the resolved profile
/// cannot leak into another test. Returns the new consumer directory.
let private resolveIntoFreshConsumer () : string =
    let sourceDir, packagesDir = restoredConsumer.Value
    let target = Path.Combine(Path.GetTempPath(), "fsgg-resolve-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory target |> ignore

    // Copy the restored consumer (project, nuget.config, obj/ with the generated nuget targets) so
    // the resolve runs against a real restored project without re-restoring.
    // `Path.GetFileName`/`GetFileName` on a directory can be null under the SDK's nullness rules;
    // a name we cannot read is a broken copy, so fail loudly rather than silently flattening it.
    let nameOf (path: string) =
        match Path.GetFileName path |> Option.ofObj with
        | Some n when n <> "" -> n
        | _ -> failtestf "cannot read a file/directory name from '%s' while copying the restored consumer" path

    let rec copyDir (src: string) (dst: string) =
        Directory.CreateDirectory dst |> ignore

        for f in Directory.GetFiles src do
            File.Copy(f, Path.Combine(dst, nameOf f), true)

        for d in Directory.GetDirectories src do
            copyDir d (Path.Combine(dst, nameOf d))

    copyDir sourceDir target

    let code, out, err =
        runDotnet target packagesDir [ "msbuild"; "Consumer.csproj"; "-t:FsggResolveReferenceGateSet" ]

    if code <> 0 then
        failtestf
            "the documented resolution verb failed (exit %d)\nSTDOUT:\n%s\nSTDERR:\n%s"
            code
            out
            err

    target

/// The package's own payload, read out of the produced archive — the comparison basis for "what the
/// consumer got is what the package shipped". Never the on-disk samples: this item exists because
/// proving resolution against `samples/` proves nothing about the package.
let private packedPayload () : Map<string, byte[]> =
    use archive = ZipFile.OpenRead producedNupkg.Value

    archive.Entries
    |> Seq.filter (fun e -> e.FullName.StartsWith contentPrefix && e.FullName <> contentPrefix)
    |> Seq.map (fun e ->
        use s = e.Open()
        use ms = new MemoryStream()
        s.CopyTo ms
        e.FullName.Substring contentPrefix.Length, ms.ToArray())
    |> Map.ofSeq

[<Tests>]
let resolutionGuard =
    testSequenced
    <| testList
        "ReferenceGateSetResolution"
        [
          // ── R1 — the gap the contract closes is REAL, measured here ──
          // Without this, R2 could pass for the wrong reason: if `PackageReference` did materialize
          // contentFiles after all, R2 would be asserting a copy that restore already made.
          test "R1 restore alone does not materialize the profile (the contentFiles gap)" {
              let consumerDir, _ = restoredConsumer.Value
              let fsgg = Path.Combine(consumerDir, ".fsgg")

              Expect.isFalse
                  (Directory.Exists fsgg)
                  "a modern SDK-style PackageReference must not be assumed to materialize contentFiles/any/any/ — if this ever starts passing, the resolution contract's premise changed and #386's mechanism should be re-examined"
          }

          // ── R2 — the verb resolves the INSTALLED package's payload ──
          test "R2 the resolution verb materializes the package payload byte-identically" {
              let consumerDir = resolveIntoFreshConsumer ()
              let fsgg = Path.Combine(consumerDir, ".fsgg")

              Expect.isTrue
                  (Directory.Exists fsgg)
                  "dotnet msbuild -t:FsggResolveReferenceGateSet must create the consumer's .fsgg/"

              let resolved =
                  Directory.GetFiles(fsgg, "*", SearchOption.AllDirectories)
                  |> Array.map (fun p -> Path.GetRelativePath(fsgg, p).Replace('\\', '/'))
                  |> Array.sort
                  |> List.ofArray

              Expect.equal resolved (List.sort shippedFiles) "the resolved .fsgg/ must hold exactly the package's published set"

              let packed = packedPayload ()

              for name in shippedFiles do
                  let onDisk = File.ReadAllBytes(Path.Combine(fsgg, name))

                  match Map.tryFind name packed with
                  | None -> failtestf "the produced package carries no %s to compare against" name
                  | Some fromPackage ->
                      Expect.equal
                          onDisk
                          fromPackage
                          (sprintf "%s: the resolved bytes must be the PACKAGE's bytes (0 drift, no hand copy)" name)
          }

          // ── R3 — the resolved profile is one the loader accepts ──
          // The acceptance criterion is not "files appeared"; it is that `Config.Loader.loadAndValidate`
          // accepts what the verb produced. Run in-process against the real loader.
          test "R3 the resolved .fsgg loads Valid through Config.Loader.loadAndValidate" {
              let consumerDir = resolveIntoFreshConsumer ()

              match Loader.loadAndValidate consumerDir with
              | Valid _ -> ()
              | Invalid diags ->
                  failtestf
                      "the resolved profile must load Valid from a clean consumer; got %d diagnostic(s): %A"
                      (List.length diags)
                      diags
          }

          // ── R4 — ADR-0009 §Decision 3 survives the resolution route ──
          // The original rejection of a package-resolved floor was that "a floor a product can escape
          // by deleting a file is not a floor". docs/decisions/0011 keeps enforcement embedded, so
          // resolution is DISTRIBUTION only — and this proves it by trying the escape: take the
          // resolved copy, bind it to the `game` profile, DELETE both gameplay checks from it, and
          // show the product still inherits them at block-on-ship.
          test "R4 deleting the floor from the resolved copy does not escape it" {
              let consumerDir = resolveIntoFreshConsumer ()
              let capabilities = Path.Combine(consumerDir, ".fsgg", "capabilities.yml")
              let original = File.ReadAllText capabilities

              // Bind the product to the `game` template-profile (the lookup key ADR-0049 made
              // load-bearing) and remove the entire generated gameplay region — the most complete
              // escape a local edit can attempt short of deleting the file.
              let bound =
                  original.Replace("    maturity: block-on-ship\nchecks:", "    maturity: block-on-ship\n    templateProfile: game\nchecks:")

              Expect.notEqual bound original "the escape test must actually bind a templateProfile, or it proves nothing"

              let regionStripped =
                  match ReferenceProfile.extractRegion (TemplateProfile "game") bound with
                  | Ok region -> bound.Replace(region, "")
                  | Error e -> failtestf "could not locate the generated region in the resolved copy: %s" e

              File.WriteAllText(capabilities, regionStripped)

              let facts =
                  match Loader.loadAndValidate consumerDir with
                  | Valid f -> f
                  | Invalid diags -> failtestf "the escaped copy must still be a loadable product: %A" diags

              // The local copy now declares NO gameplay check…
              let localGameplay =
                  facts.Capabilities.Checks |> List.filter (fun c -> c.Domain = DomainId "gameplay")

              Expect.isEmpty localGameplay "the escape must actually have removed the local gameplay checks"

              // …yet the product still INHERITS both, at full strength, with no file read involved.
              let inherited = Inheritance.inheritedGatesFor facts

              let inheritedIds =
                  inherited |> List.map (fun g -> gateIdValue g.Id) |> Set.ofList

              Expect.equal
                  inheritedIds
                  (set [ "gameplay:fr-covered"; "gameplay:production-journey" ])
                  "a product that deleted the floor from its resolved .fsgg/ must still inherit it — ADR-0009 §Decision 3, preserved by docs/decisions/0011"

              for g in inherited do
                  Expect.equal
                      g.Maturity
                      BlockOnShip
                      (sprintf "%s must be inherited at its declared maturity, not a lowered one" (gateIdValue g.Id))

              // And composition puts them into the effective set even though the product's own gate
              // set is empty of them — the union `Ship.rollup` actually enforces.
              let effective = Inheritance.composeEffectiveGates inherited []

              Expect.equal
                  (effective |> List.map (fun g -> gateIdValue g.Id) |> Set.ofList)
                  inheritedIds
                  "the composed effective gate set must contain the inherited floor for a product declaring none"
          }
        ]
