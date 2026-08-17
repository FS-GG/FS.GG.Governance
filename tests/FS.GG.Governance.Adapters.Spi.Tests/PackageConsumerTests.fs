module FS.GG.Governance.Adapters.Spi.Tests.PackageConsumerTests

open System
open System.Diagnostics
open System.IO
open System.IO.Compression
open System.Security
open System.Xml.Linq
open Expecto

type private CommandResult = { ExitCode: int; Output: string }

let private run workingDirectory environment arguments =
    let start = ProcessStartInfo("dotnet")
    start.WorkingDirectory <- workingDirectory
    start.UseShellExecute <- false
    start.RedirectStandardOutput <- true
    start.RedirectStandardError <- true
    arguments |> List.iter start.ArgumentList.Add
    environment |> List.iter (fun (name, value) -> start.Environment.[name] <- value)
    match Process.Start start with
    | null -> failwith "dotnet process did not start"
    | child ->
        use child = child
        let stdout = child.StandardOutput.ReadToEndAsync()
        let stderr = child.StandardError.ReadToEndAsync()
        child.WaitForExit()
        { ExitCode = child.ExitCode; Output = stdout.Result + stderr.Result }

let private requireGreen label result =
    if result.ExitCode <> 0 then
        failtestf "%s failed with exit %d:\n%s" label result.ExitCode result.Output

let rec private copyDirectory source destination =
    Directory.CreateDirectory destination |> ignore
    for file in Directory.GetFiles source do
        let name = match Path.GetFileName file with null -> failwith "fixture file has no name" | value -> value
        File.Copy(file, Path.Combine(destination, name), true)
    for directory in Directory.GetDirectories source do
        let name = match Path.GetFileName directory with null -> failwith "fixture directory has no name" | value -> value
        copyDirectory directory (Path.Combine(destination, name))

let private writeNuGetConfig path feed =
    let escapedFeed = match SecurityElement.Escape feed with null -> failwith "feed path could not be escaped" | value -> value
    File.WriteAllText(
        path,
        $"""<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="adapter-spi-fixture" value="{escapedFeed}" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="adapter-spi-fixture"><package pattern="FS.GG.Governance.*" /></packageSource>
    <packageSource key="nuget.org"><package pattern="FSharp.Core" /></packageSource>
  </packageSourceMapping>
</configuration>
""")

let private deleteIfPresent path =
    if Directory.Exists path then Directory.Delete(path, true)

let private deleteFileIfPresent path =
    if File.Exists path then File.Delete path

let private packagePath feed id version = Path.Combine(feed, $"{id}.{version}.nupkg")

let private inspectSpiPackage nupkg =
    use archive = ZipFile.OpenRead nupkg
    let entries = archive.Entries |> Seq.map _.FullName |> Seq.toList
    Expect.contains entries "lib/net10.0/FS.GG.Governance.Adapters.Spi.dll" "SPI assembly is a compile-time package asset"
    Expect.isFalse (entries |> List.exists (fun name -> name.Contains("CommandHost", StringComparison.OrdinalIgnoreCase))) "command-host assemblies are absent"

    let nuspec =
        archive.Entries
        |> Seq.find (fun entry -> entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase))
    use stream = nuspec.Open()
    let document = XDocument.Load stream
    let dependencies =
        document.Descendants()
        |> Seq.filter (fun node -> node.Name.LocalName = "dependency")
        |> Seq.map (fun node ->
            let id = match node.Attribute(XName.Get "id") with null -> failtest "nuspec dependency has no id" | value -> value.Value
            let version = match node.Attribute(XName.Get "version") with null -> failtestf "nuspec dependency %s has no version" id | value -> value.Value
            id, version)
        |> Seq.toList

    let governanceDependencies = dependencies |> List.filter (fun (id, _) -> id.StartsWith("FS.GG.Governance.", StringComparison.Ordinal))
    Expect.equal governanceDependencies [ "FS.GG.Governance.Kernel", "[0.1.1]" ] "Kernel is the sole exact Governance package dependency"
    Expect.isFalse (dependencies |> List.exists (fun (id, _) -> id.Contains("Command", StringComparison.OrdinalIgnoreCase))) "command packages are absent"
    Expect.isFalse (dependencies |> List.exists (fun (id, _) -> id.Contains("Host", StringComparison.OrdinalIgnoreCase))) "host packages are absent"

let private mutateSpiPackage original mutated =
    File.Copy(original, mutated, true)
    use archive = ZipFile.Open(mutated, ZipArchiveMode.Update)
    match archive.GetEntry "lib/net10.0/FS.GG.Governance.Adapters.Spi.dll" with
    | null -> failtest "cannot mutate SPI package: assembly entry is absent"
    | assembly -> assembly.Delete()

[<Tests>]
let packageTests =
    testList "PackageConsumer" [
        testCase "package-only locked consumer and negative package mutation" <| fun _ ->
            let repoRoot = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "../.."))
            let fixtureSource = Path.Combine(repoRoot, "tests/fixtures/adapter-spi-package-consumer")
            let tempRoot = Path.Combine(Path.GetTempPath(), "fsgg-adapter-spi-package-" + Guid.NewGuid().ToString("N"))
            let feed = Path.Combine(tempRoot, "feed")
            let negativeFeed = Path.Combine(tempRoot, "negative-feed")
            let substitutionFeed = Path.Combine(tempRoot, "substitution-feed")
            let consumer = Path.Combine(tempRoot, "consumer")
            let project = Path.Combine(consumer, "AdapterSpiConsumer.fsproj")

            try
                Directory.CreateDirectory feed |> ignore
                Directory.CreateDirectory negativeFeed |> ignore
                Directory.CreateDirectory substitutionFeed |> ignore
                copyDirectory fixtureSource consumer

                let kernelProject = Path.Combine(repoRoot, "src/FS.GG.Governance.Kernel/FS.GG.Governance.Kernel.fsproj")
                let spiProject = Path.Combine(repoRoot, "src/FS.GG.Governance.Adapters.Spi/FS.GG.Governance.Adapters.Spi.fsproj")
                requireGreen "pack Kernel" (run repoRoot [] [ "pack"; kernelProject; "-c"; "Release"; "--no-restore"; "-o"; feed ])
                requireGreen "pack adapter SPI" (run repoRoot [] [ "pack"; spiProject; "-c"; "Release"; "--no-restore"; "-o"; feed ])

                let kernelPackage = packagePath feed "FS.GG.Governance.Kernel" "0.1.1"
                let spiPackage = packagePath feed "FS.GG.Governance.Adapters.Spi" "0.1.0"
                Expect.isTrue (File.Exists kernelPackage) "Kernel package was produced"
                Expect.isTrue (File.Exists spiPackage) "SPI package was produced"
                inspectSpiPackage spiPackage

                let config = Path.Combine(tempRoot, "NuGet.Config")
                writeNuGetConfig config feed
                let seedPackages = Path.Combine(tempRoot, "packages-seed")
                requireGreen "seed consumer lock" (run consumer [ "NUGET_PACKAGES", seedPackages ] [ "restore"; project; "--use-lock-file"; "--configfile"; config ])
                Expect.isTrue (File.Exists(Path.Combine(consumer, "packages.lock.json"))) "consumer lock was generated"

                deleteIfPresent (Path.Combine(consumer, "obj"))
                deleteIfPresent seedPackages
                let lockedPackages = Path.Combine(tempRoot, "packages-locked")
                requireGreen "cold locked consumer restore" (run consumer [ "NUGET_PACKAGES", lockedPackages ] [ "restore"; project; "--locked-mode"; "--configfile"; config ])
                requireGreen "external consumer build" (run consumer [ "NUGET_PACKAGES", lockedPackages ] [ "build"; project; "--no-restore"; "-c"; "Release" ])
                let execution = run consumer [ "NUGET_PACKAGES", lockedPackages ] [ "run"; "--project"; project; "--no-build"; "-c"; "Release" ]
                requireGreen "external consumer execution" execution
                let expected = File.ReadAllText(Path.Combine(consumer, "expected.txt")).Replace("\r\n", "\n").Trim()
                let actual = execution.Output.Replace("\r\n", "\n").Trim()
                Expect.equal actual expected "package-only consumer projection is stable"

                let kernelName = match Path.GetFileName kernelPackage with null -> failwith "Kernel package has no file name" | value -> value
                let spiName = match Path.GetFileName spiPackage with null -> failwith "SPI package has no file name" | value -> value

                requireGreen "pack non-matching Kernel" (run repoRoot [] [ "pack"; kernelProject; "-c"; "Release"; "--no-restore"; "-p:PackageVersion=0.1.2"; "-o"; substitutionFeed ])
                File.Copy(spiPackage, Path.Combine(substitutionFeed, spiName), true)
                let substitutionConsumer = Path.Combine(tempRoot, "substitution-consumer")
                copyDirectory fixtureSource substitutionConsumer
                let substitutionConfig = Path.Combine(tempRoot, "NuGet.Substitution.Config")
                writeNuGetConfig substitutionConfig substitutionFeed
                let substitutionPackages = Path.Combine(tempRoot, "packages-substitution")
                let substitution =
                    run substitutionConsumer [ "NUGET_PACKAGES", substitutionPackages ]
                        [ "restore"; Path.Combine(substitutionConsumer, "AdapterSpiConsumer.fsproj"); "--use-lock-file"; "--configfile"; substitutionConfig ]
                Expect.notEqual substitution.ExitCode 0 "Kernel 0.1.2 must not substitute for the exact 0.1.1 dependency"
                Expect.stringContains substitution.Output "NU1102" "exact Kernel resolution rejects a feed without version 0.1.1"

                File.Copy(kernelPackage, Path.Combine(negativeFeed, kernelName), true)
                mutateSpiPackage spiPackage (Path.Combine(negativeFeed, spiName))
                let negativeConsumer = Path.Combine(tempRoot, "negative-consumer")
                copyDirectory fixtureSource negativeConsumer
                let negativeProject = Path.Combine(negativeConsumer, "AdapterSpiConsumer.fsproj")
                let negativeConfig = Path.Combine(tempRoot, "NuGet.Negative.Config")
                writeNuGetConfig negativeConfig negativeFeed
                let negativePackages = Path.Combine(tempRoot, "packages-negative")
                let negative =
                    run negativeConsumer [ "NUGET_PACKAGES", negativePackages ]
                        [ "restore"; negativeProject; "--use-lock-file"; "--configfile"; negativeConfig ]
                Expect.notEqual negative.ExitCode 0 "a package whose compile-time SPI assembly was removed must fail fresh restore"
                Expect.stringContains negative.Output "NU1202" "the subject mutation is rejected because the SPI has no compatible compile surface"

                File.Copy(spiPackage, Path.Combine(negativeFeed, spiName), true)
                deleteIfPresent (Path.Combine(negativeConsumer, "obj"))
                deleteIfPresent negativePackages
                deleteFileIfPresent (Path.Combine(negativeConsumer, "packages.lock.json"))
                requireGreen "reattached lock generation" (run negativeConsumer [ "NUGET_PACKAGES", negativePackages ] [ "restore"; negativeProject; "--use-lock-file"; "--configfile"; negativeConfig ])
                requireGreen "reattached compile control" (run negativeConsumer [ "NUGET_PACKAGES", negativePackages ] [ "build"; negativeProject; "--no-restore"; "-c"; "Release" ])
            finally
                if Directory.Exists tempRoot then Directory.Delete(tempRoot, true)
    ]
