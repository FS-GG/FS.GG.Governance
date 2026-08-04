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
/// cannot leak into another test. Returns the new consumer directory. `extraArgs` passes MSBuild
/// properties through so the target's documented options are exercised, not just its defaults.
/// `Path.GetFileName` on a directory can be null under the SDK's nullness rules; a name we cannot
/// read is a broken copy, so fail loudly rather than silently flattening it.
let private nameOf (path: string) =
    match Path.GetFileName path |> Option.ofObj with
    | Some n when n <> "" -> n
    | _ -> failtestf "cannot read a file/directory name from '%s' while copying a tree" path

/// Recursive directory copy. Shared by the ordinary-repository resolve (which copies the restored
/// consumer) and the generated-product resolve (which additionally copies a scaffolded `.fsgg/`).
let rec private copyTree (src: string) (dst: string) =
    Directory.CreateDirectory dst |> ignore

    for f in Directory.GetFiles src do
        File.Copy(f, Path.Combine(dst, nameOf f), true)

    for d in Directory.GetDirectories src do
        copyTree d (Path.Combine(dst, nameOf d))

/// A generated product's scaffolded `.fsgg/`, as `fsgg-sdd init` writes it. Returns the SOURCE of
/// the bytes and the directory holding them, so the caller can disclose which route ran.
///
/// `fsgg-sdd` is a global dotnet tool published from FS-GG/FS.GG.SDD; it is deliberately not in this
/// repo's `.config/dotnet-tools.json`, because restoring it would couple this gate to the org's
/// credentialed feed that these tests blank on purpose. So the REAL tool runs when it is on PATH,
/// and the captured-real fixture stands in when it is not. Never a skip, and never silent.
/// Exactly the files `fsgg-sdd init` 1.0.0 writes into `.fsgg/`. Asserted on BOTH branches below,
/// which is what keeps the captured fixture honest rather than merely claimed.
///
/// #385, repair round 1: the fixture was committed at FIVE of these six. `.gitignore`'s
/// `**/.fsgg/*.json` silently swallowed `scaffold-provenance.json`, and since CI has no `fsgg-sdd`
/// on PATH, CI is exactly where R7 takes the fixture branch — so "every SDD-authored byte survives"
/// was being proven over a capture that was missing a file, while the README claimed six. A scoped
/// negation in `.gitignore` restored it; this list is what stops it silently regressing, and it also
/// reds when the real tool's output shape changes, which is the signal that the fixture is stale.
let private expectedScaffoldFiles =
    set
        [ "agents.yml"
          "constitution.md"
          "early-stage-guidance.md"
          "project.yml"
          "scaffold-provenance.json"
          "sdd.yml" ]

let private assertScaffoldComplete (source: string) (dir: string) =
    let actual =
        Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly)
        |> Array.map nameOf
        |> Set.ofArray

    if actual <> expectedScaffoldFiles then
        failtestf
            "the generated-product scaffold from %s does not carry the expected file set.\nexpected: %A\nactual:   %A\nIf the REAL tool changed shape, re-capture fixtures/generated-product-scaffold/ and update expectedScaffoldFiles. If the FIXTURE is short, a .gitignore rule is probably eating a file (#385)."
            source
            expectedScaffoldFiles
            actual

let private generatedProductScaffold () : string * string =
    let target = Path.Combine(Path.GetTempPath(), "fsgg-scaffold-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory target |> ignore

    let ran =
        try
            let psi = ProcessStartInfo("fsgg-sdd", RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = target)
            [ "init"; "--root"; "."; "--json" ] |> List.iter psi.ArgumentList.Add

            match Process.Start psi |> Option.ofObj with
            | None -> false
            | Some child ->
                child.StandardOutput.ReadToEnd() |> ignore
                child.StandardError.ReadToEnd() |> ignore
                child.WaitForExit()
                child.ExitCode = 0 && Directory.Exists(Path.Combine(target, ".fsgg"))
        with _ ->
            false

    if ran then
        let dir = Path.Combine(target, ".fsgg")
        let source = "real `fsgg-sdd init`"
        assertScaffoldComplete source dir
        source, dir
    else
        let fixtureDir =
            Path.Combine(
                FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot,
                "fixtures",
                "generated-product-scaffold",
                ".fsgg"
            )

        if not (Directory.Exists fixtureDir) then
            failtestf
                "neither `fsgg-sdd` (PATH) nor the captured scaffold fixture at %s is available — the generated-product route cannot be proven, and this fails closed rather than passing by absence"
                fixtureDir

        let copy = Path.Combine(target, ".fsgg")
        copyTree fixtureDir copy

        let source =
            "captured real `fsgg-sdd init` 1.0.0 fixture (fixtures/generated-product-scaffold)"

        assertScaffoldComplete source copy
        source, copy

let private resolveIntoFreshConsumerWith (extraArgs: string list) : string =
    let sourceDir, packagesDir = restoredConsumer.Value
    let target = Path.Combine(Path.GetTempPath(), "fsgg-resolve-" + Guid.NewGuid().ToString("N"))

    // Copy the restored consumer (project, nuget.config, obj/ with the generated nuget targets) so
    // the resolve runs against a real restored project without re-restoring.
    copyTree sourceDir target

    let code, out, err =
        runDotnet target packagesDir ([ "msbuild"; "Consumer.csproj"; "-t:FsggResolveReferenceGateSet" ] @ extraArgs)

    if code <> 0 then
        failtestf
            "the documented resolution verb failed (exit %d)\nSTDOUT:\n%s\nSTDERR:\n%s"
            code
            out
            err

    target

/// The plain, documented invocation.
let private resolveIntoFreshConsumer () : string = resolveIntoFreshConsumerWith []

/// Re-run the resolution verb in a consumer that has already resolved once.
let private reResolve (consumerDir: string) (extraArgs: string list) =
    let _, packagesDir = restoredConsumer.Value

    let code, out, err =
        runDotnet consumerDir packagesDir ([ "msbuild"; "Consumer.csproj"; "-t:FsggResolveReferenceGateSet" ] @ extraArgs)

    if code <> 0 then
        failtestf "re-resolving failed (exit %d)\nSTDOUT:\n%s\nSTDERR:\n%s" code out err

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

          // ── R5 — both documented overwrite modes actually behave as documented ──
          // `FsggReferenceGateSetOverwrite` is a consumer-facing knob on a SHIPPED package asset, and
          // an untested branch there costs an adopter their local edits. Both arms are executed
          // against a real re-resolve of an already-resolved consumer.
          test "R5 re-resolving overwrites by default and preserves local edits when asked not to" {
              let consumerDir = resolveIntoFreshConsumer ()
              let policy = Path.Combine(consumerDir, ".fsgg", "policy.yml")
              let pristine = File.ReadAllText policy
              let edited = pristine + "\n# a local edit\n"

              // (a) overwrite=false must NOT clobber the edit.
              File.WriteAllText(policy, edited)
              reResolve consumerDir [ "-p:FsggReferenceGateSetOverwrite=false" ]

              Expect.equal
                  (File.ReadAllText policy)
                  edited
                  "FsggReferenceGateSetOverwrite=false must refuse to overwrite a file the consumer edited"

              // (b) the default must re-resolve in place, so a version bump is adoptable by re-running
              // the same verb rather than by deleting the directory first.
              reResolve consumerDir []

              Expect.equal
                  (File.ReadAllText policy)
                  pristine
                  "the default (overwrite=true) must restore the published bytes on a re-resolve"
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

          // ── R6 — AN ORDINARY REPOSITORY consumes the composed F# constitution profile (#385 AC4) ──
          // R2/R3 prove that *a* profile resolves and loads. This proves the resolved profile is the
          // COMPOSED one: epic #367's four rule packs, present as four real gates in the registry a
          // consuming repository would run, obtained from the installed package by the documented
          // verb — not from `samples/`, and not by importing the packs directly.
          test "R6 an ordinary repository resolves the composed F# constitution profile into real gates" {
              let consumerDir = resolveIntoFreshConsumer ()

              let facts =
                  match Loader.loadAndValidate consumerDir with
                  | Valid f -> f
                  | Invalid diags -> failtestf "the resolved profile must load Valid: %A" diags

              let composedIds =
                  FS.GG.Governance.SurfaceChecks.Profile.checks
                  |> List.map (fun c ->
                      let (CheckId id) = c.Id
                      let (DomainId d) = c.Domain
                      sprintf "%s:%s" d id)
                  |> Set.ofList

              Expect.equal
                  (Set.count composedIds)
                  4
                  "the composed profile is epic #367's four packs — if this is not 4, the rest of the assertion is measuring the wrong thing"

              // The gates a consuming repository actually gets, through the real registry builder.
              let gateIds =
                  (FS.GG.Governance.Gates.Gates.buildRegistry facts).Gates
                  |> List.map (fun g -> gateIdValue g.Id)
                  |> Set.ofList

              Expect.isTrue
                  (Set.isSubset composedIds gateIds)
                  (sprintf
                      "the resolved profile must yield every composed F# constitution gate; expected %A within %A"
                      composedIds
                      gateIds)

              // Maturity survives the round trip through YAML and the real parser — a consumer that
              // resolves the profile gets #369 blocking and the other three advisory, exactly as
              // `SurfaceChecks.Profile` declares.
              for pack in FS.GG.Governance.SurfaceChecks.Profile.packs do
                  let token = FS.GG.Governance.SurfaceChecks.Profile.packToken pack

                  let gate =
                      (FS.GG.Governance.Gates.Gates.buildRegistry facts).Gates
                      |> List.tryFind (fun g -> gateIdValue g.Id = sprintf "fsharp:%s" token)

                  match gate with
                  | None -> failtestf "the resolved profile is missing the '%s' gate" token
                  | Some g ->
                      Expect.equal
                          g.Maturity
                          (FS.GG.Governance.SurfaceChecks.Profile.declaredMaturity pack)
                          (sprintf
                              "gate 'fsharp:%s' must carry the composed profile's declared maturity after the YAML round trip"
                              token)

              // And the four gates are command-free, so a repository that resolved the set without
              // adopting this repo's tooling has no dangling command reference.
              for g in (FS.GG.Governance.Gates.Gates.buildRegistry facts).Gates do
                  if Set.contains (gateIdValue g.Id) composedIds then
                      Expect.isEmpty
                          g.Prerequisites
                          (sprintf "composed gate %s must carry no command prerequisite" (gateIdValue g.Id))
          }

          // ── R7 — A GENERATED PRODUCT consumes it, by a DIFFERENT resolution path (#385 AC3) ──
          // The generated-product route is not R6 with a different name. Its destination `.fsgg/`
          // ALREADY EXISTS and already has an owner: `fsgg-sdd init` (FS-GG/FS.GG.SDD) writes
          // `project.yml`, `sdd.yml`, `agents.yml`, `constitution.md`, `early-stage-guidance.md` and
          // `scaffold-provenance.json` there, and writes NO Governance file at all. So the profile
          // must MERGE into a populated directory it does not own, under the non-clobbering mode
          // (`FsggReferenceGateSetOverwrite=false`), and the result must still load `Valid`.
          //
          // The scaffolder is a global tool published from another repository and is deliberately
          // NOT restored by this repo's gate (that would couple it to the credentialed feed these
          // tests blank on purpose). So: run the REAL tool when it is on PATH, else use the captured
          // real capture in `fixtures/generated-product-scaffold/`. The choice is PRINTED, never
          // silent, and the test never skips — see that fixture's README for its provenance.
          //
          // #385 changes nothing in FS-GG/FS.GG.SDD. Teaching `init` to do this resolve itself is
          // FS-GG/FS.GG.SDD#845, and is that repository's decision to make.
          test "R7 a generated product's scaffolded .fsgg merges the composed profile without losing SDD's files" {
              let scaffoldSource, scaffoldDir = generatedProductScaffold ()
              printfn "R7 generated-product scaffold source: %s (%s)" scaffoldSource scaffoldDir

              let sddOwned =
                  Directory.GetFiles(scaffoldDir, "*", SearchOption.TopDirectoryOnly)
                  |> Array.map (fun f -> nameOf f, File.ReadAllBytes f)
                  |> Map.ofArray

              Expect.isTrue (Map.count sddOwned >= 2) "the scaffold must actually contain SDD-authored files"

              // The measured gap this route closes: the scaffolder writes NO Governance file.
              for governanceFile in orderedFiles do
                  Expect.isFalse
                      (Map.containsKey governanceFile sddOwned)
                      (sprintf
                          "the scaffolded workspace must not already contain '%s' — if it does, this test is not measuring the generated-product gap (FS-GG/FS.GG.SDD#845)"
                          governanceFile)

              // Place the scaffolded `.fsgg/` into a restored consumer and resolve INTO it.
              let sourceDir, packagesDir = restoredConsumer.Value
              let target = Path.Combine(Path.GetTempPath(), "fsgg-generated-" + Guid.NewGuid().ToString("N"))
              copyTree sourceDir target
              copyTree scaffoldDir (Path.Combine(target, ".fsgg"))

              let code, out, err =
                  runDotnet
                      target
                      packagesDir
                      [ "msbuild"
                        "Consumer.csproj"
                        "-t:FsggResolveReferenceGateSet"
                        "-p:FsggReferenceGateSetOverwrite=false" ]

              if code <> 0 then
                  failtestf
                      "resolving into a scaffolded generated product failed (exit %d)\nSTDOUT:\n%s\nSTDERR:\n%s"
                      code
                      out
                      err

              let resolved = Path.Combine(target, ".fsgg")

              // (a) Every byte the generator wrote survives. A resolution that silently rewrote
              // another owner's files would be a worse contract than no resolution at all.
              for KeyValue(name, bytes) in sddOwned do
                  let after = Path.Combine(resolved, name)
                  Expect.isTrue (File.Exists after) (sprintf "the resolve must not delete the generator's '%s'" name)

                  Expect.equal
                      (File.ReadAllBytes after)
                      bytes
                      (sprintf "the resolve must not modify the generator's '%s'" name)

              // (b) The published payload landed alongside it.
              for name in shippedFiles do
                  Expect.isTrue
                      (File.Exists(Path.Combine(resolved, name)))
                      (sprintf "the resolved profile must supply '%s' into the scaffolded .fsgg/" name)

              // (c) The merged directory is a loadable product — the scaffolder's extra files are
              // ignored by the loader rather than making the set Invalid.
              let facts =
                  match Loader.loadAndValidate target with
                  | Valid f -> f
                  | Invalid diags ->
                      failtestf "a scaffolded generated product with the profile resolved into it must load Valid: %A" diags

              // (d) …and it carries the COMPOSED profile's four gates, which is what AC3 asks.
              let gateIds =
                  (FS.GG.Governance.Gates.Gates.buildRegistry facts).Gates
                  |> List.map (fun g -> gateIdValue g.Id)
                  |> Set.ofList

              for pack in FS.GG.Governance.SurfaceChecks.Profile.packs do
                  let id = sprintf "fsharp:%s" (FS.GG.Governance.SurfaceChecks.Profile.packToken pack)

                  Expect.isTrue
                      (Set.contains id gateIds)
                      (sprintf "the generated product must carry the composed gate '%s' after resolving; got %A" id gateIds)

              Directory.Delete(target, true)
          }
        ]
