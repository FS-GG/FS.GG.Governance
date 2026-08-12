module FS.GG.Governance.DesignChecks.Tests.FSharpSurfaceCommandTests

open System
open System.Diagnostics
open System.IO
open System.Text.Json
open Expecto
open FS.GG.Governance.DesignChecks.Tests.Support

// Real command-edge coverage for the persisted v1 producer.  These fixtures deliberately create
// actual SDK projects and execute the production command; they are not synthetic receipt JSON.
let private withTemporaryProject (files: (string * string) list) action =
    let root = Path.Combine(Path.GetTempPath(), "fsgg-fsharp-surface-command-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory root |> ignore
    try
        for relative, content in files do
            let path = Path.Combine(root, relative)
            Path.GetDirectoryName(path) |> Option.ofObj |> Option.iter (Directory.CreateDirectory >> ignore)
            File.WriteAllText(path, content)
        action root
    finally
        Directory.Delete(root, true)

let private run root project extra =
    let info = ProcessStartInfo("dotnet")
    let assemblyDirectory =
        System.Reflection.Assembly.GetExecutingAssembly().Location
        |> Path.GetDirectoryName
        |> Option.ofObj
        |> Option.defaultWith (fun () -> failtest "could not determine the test assembly directory")

    let configuration =
        assemblyDirectory
        |> Path.GetDirectoryName
        |> Option.ofObj
        |> Option.bind (Path.GetFileName >> Option.ofObj)
        |> Option.defaultWith (fun () -> failtest "could not determine the active test configuration")

    let commandAssembly =
        Path.Combine(
            repoRoot,
            "src",
            "FS.GG.Governance.FSharpSurfaceCommand",
            "bin",
            configuration,
            "net10.0",
            "FS.GG.Governance.FSharpSurfaceCommand.dll"
        )

    [ commandAssembly; "--root"; root; "--project"; project ]
    @ extra
    |> List.iter info.ArgumentList.Add
    info.WorkingDirectory <- repoRoot
    info.RedirectStandardOutput <- true
    info.RedirectStandardError <- true
    info.UseShellExecute <- false
    match Process.Start(info) |> Option.ofObj with
    | None -> failtest "fsharp-surface command did not start"
    | Some child ->
        use child = child
        let output = child.StandardOutput.ReadToEnd()
        let error = child.StandardError.ReadToEnd()
        child.WaitForExit()
        if String.IsNullOrWhiteSpace output then failtestf "fsharp-surface command emitted no JSON (stderr: %s)" error
        child.ExitCode, output

let private stringField (name: string) (json: string) =
    use document = JsonDocument.Parse json
    document.RootElement.GetProperty(name).GetString()

let private requiredPublicationRoute (workflow: string) =
    [ "publish-fsharp-surface-command:"
      "FS.GG.Governance.FSharpSurfaceCommand"
      "Package-only installed-tool smoke before publication"
      "fsgg-fsharp-surface"
      "Push to the org feed"
      "Push same bytes to nuget.org" ]
    |> List.forall workflow.Contains

let private runProcess workingDirectory executable arguments =
    let info = ProcessStartInfo(executable)
    arguments |> List.iter info.ArgumentList.Add
    info.WorkingDirectory <- workingDirectory
    info.RedirectStandardOutput <- true
    info.RedirectStandardError <- true
    info.UseShellExecute <- false
    match Process.Start(info) |> Option.ofObj with
    | None -> failtestf "process did not start: %s" executable
    | Some child ->
        use child = child
        let output = child.StandardOutput.ReadToEnd()
        let error = child.StandardError.ReadToEnd()
        child.WaitForExit()
        child.ExitCode, output, error

[<Tests>]
let tests =
    testSequenced <|
        testList
            "FSharpSurfaceCommand"
            [ test "production command persists deterministic configured-policy receipts across zero, populated, non-applicable, internal, and malformed fixtures" {
                  withTemporaryProject
                      [ "Zero.fsproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType></PropertyGroup><ItemGroup><Compile Include=\"Program.fs\" /><Compile Include=\"World.fs\" /></ItemGroup></Project>"
                        "Program.fs", "[<EntryPoint>] let main _ = 0"
                        "World.fs", "module World\nlet tick = 1"
                        "Populated.fsproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><Compile Include=\"Public.fsi\" /><Compile Include=\"Public.fs\" /></ItemGroup></Project>"
                        "Public.fsi", "/// Public contract\nmodule Public\n/// value\nval value: int"
                        "Public.fs", "module Public\nlet value = 1"
                        "Internal.fsproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><Compile Include=\"Internal.fs\" /></ItemGroup></Project>"
                        "Internal.fs", "module internal Internal\nlet value = 1"
                        ".fsgg/fsharp-surface.json", "{\"maturity\":\"block-on-ship\",\"declaredGlob\":\"**/*.fsi\"}" ]
                      (fun root ->
                          let zeroExit, zeroFirst = run root "Zero.fsproj" []
                          let _, zeroSecond = run root "Zero.fsproj" []
                          Expect.equal zeroExit 0 "configured zero-signature project is a valid receipt"
                          Expect.equal zeroFirst zeroSecond "same production command inputs persist byte-identical JSON"
                          Expect.equal (stringField "maturity" zeroFirst, stringField "cardinality" zeroFirst) ("block-on-ship", "zero") "zero receipt carries configured blocking maturity"
                          Expect.equal (File.ReadAllText(Path.Combine(root, "readiness", "fsharp-public-surface.json"))) (zeroSecond.TrimEnd()) "persisted bytes equal stdout aside from CLI line termination"

                          let populatedExit, populated = run root "Populated.fsproj" []
                          Expect.equal populatedExit 0 "populated project produces a receipt"
                          Expect.equal (stringField "maturity" populated, stringField "cardinality" populated) ("block-on-ship", "one") "populated receipt keeps configured maturity"

                          let nonApplicableExit, nonApplicable = run root "Populated.fsproj" [ "--test-project" ]
                          Expect.equal nonApplicableExit 0 "validated test-project exclusion is not malformed"
                          Expect.equal (stringField "applicability" nonApplicable) "not-applicable" "explicit non-applicability remains distinct"

                          let internalExit, internalReceipt = run root "Internal.fsproj" []
                          Expect.equal internalExit 0 "internal control is valid"
                          Expect.equal (stringField "maturity" internalReceipt) "block-on-ship" "internal control cannot forge or erase policy maturity"

                          File.WriteAllText(Path.Combine(root, ".fsgg", "fsharp-surface.json"), "{\"maturity\":\"forged\"}")
                          let malformedExit, malformed = run root "Zero.fsproj" []
                          Expect.equal malformedExit 3 "malformed policy produces the documented input exit"
                          use document = JsonDocument.Parse malformed
                          Expect.equal (document.RootElement.GetProperty("malformed").ValueKind) JsonValueKind.String "malformed input has no clean verdict") }

              test "packed global tool runs in a clean consumer and carries its runtime closure" {
                  let packageDirectory = Path.Combine(Path.GetTempPath(), "fsgg-fsharp-surface-package-" + Guid.NewGuid().ToString("N"))
                  let toolDirectory = Path.Combine(packageDirectory, "tool")
                  Directory.CreateDirectory packageDirectory |> ignore
                  try
                      let packExit, _, packError =
                          runProcess repoRoot "dotnet"
                              [ "pack"; "src/FS.GG.Governance.FSharpSurfaceCommand/FS.GG.Governance.FSharpSurfaceCommand.fsproj"; "-c"; "Debug"; "--no-restore"; "-o"; packageDirectory ]
                      Expect.equal packExit 0 (sprintf "packed tool succeeds: %s" packError)
                      let package = Directory.GetFiles(packageDirectory, "FS.GG.Governance.FSharpSurfaceCommand.*.nupkg") |> Array.exactlyOne
                      let paths =
                          use archive = System.IO.Compression.ZipFile.OpenRead package
                          archive.Entries |> Seq.map (fun entry -> entry.FullName) |> Set.ofSeq
                      Expect.isTrue (paths |> Set.exists (fun path -> path.EndsWith("/FS.GG.Governance.FSharpSurfaceCommand.dll", StringComparison.Ordinal))) "package contains producer"
                      Expect.isTrue (paths |> Set.exists (fun path -> path.EndsWith("/FS.GG.Governance.DesignChecks.dll", StringComparison.Ordinal))) "package contains runtime dependency"
                      File.WriteAllText(Path.Combine(packageDirectory, "NuGet.config"), "<?xml version=\"1.0\"?><configuration><packageSources><clear /><add key=\"local\" value=\"" + packageDirectory + "\" /></packageSources></configuration>")
                      let installExit, _, installError =
                          runProcess repoRoot "dotnet" [ "tool"; "install"; "--tool-path"; toolDirectory; "--configfile"; Path.Combine(packageDirectory, "NuGet.config"); "FS.GG.Governance.FSharpSurfaceCommand"; "--version"; "1.12.1" ]
                      Expect.equal installExit 0 (sprintf "clean tool install succeeds: %s" installError)
                      withTemporaryProject
                          [ "Consumer.fsproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><Compile Include=\"Api.fsi\" /><Compile Include=\"Api.fs\" /></ItemGroup></Project>"
                            "Api.fsi", "module Api\nval value: int"
                            "Api.fs", "module Api\nlet value = 1" ]
                          (fun root ->
                              let command = Path.Combine(toolDirectory, "fsgg-fsharp-surface")
                              let exitCode, output, error = runProcess root command [ "--root"; root; "--project"; "Consumer.fsproj" ]
                              Expect.equal exitCode 0 (sprintf "installed producer runs: %s" error)
                              Expect.equal output (File.ReadAllText(Path.Combine(root, "readiness", "fsharp-public-surface.json")) + Environment.NewLine) "installed stdout exactly projects the receipt"
                              Directory.CreateDirectory(Path.Combine(root, ".fsgg")) |> ignore
                              File.WriteAllText(Path.Combine(root, ".fsgg", "fsharp-surface.json"), "{\"maturity\":\"forged\"}")
                              let malformedExit, malformed, _ = runProcess root command [ "--root"; root; "--project"; "Consumer.fsproj" ]
                              Expect.equal malformedExit 3 "installed producer keeps malformed input as exit 3"
                              use document = JsonDocument.Parse malformed
                              Expect.equal (document.RootElement.GetProperty("malformed").ValueKind) JsonValueKind.String "installed producer emits no clean verdict for malformed policy")
                      let mutated = System.IO.Compression.ZipFile.Open(package, System.IO.Compression.ZipArchiveMode.Update)
                      match mutated.GetEntry("tools/net10.0/any/FS.GG.Governance.DesignChecks.dll") |> Option.ofObj with
                      | None -> failtest "mutation target is packaged before removal"
                      | Some dependency -> dependency.Delete()
                      mutated.Dispose()
                      let brokenInstallExit, _, _ =
                          runProcess repoRoot "dotnet" [ "tool"; "install"; "--tool-path"; Path.Combine(packageDirectory, "broken-tool"); "--configfile"; Path.Combine(packageDirectory, "NuGet.config"); "FS.GG.Governance.FSharpSurfaceCommand"; "--version"; "1.12.1" ]
                      Expect.equal brokenInstallExit 0 "NuGet can install a structurally incomplete package, so the command smoke must prove the closure"
                      withTemporaryProject
                          [ "Broken.fsproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><Compile Include=\"Api.fs\" /></ItemGroup></Project>"
                            "Api.fs", "module Api\nlet value = 1" ]
                          (fun root ->
                              let brokenCommand = Path.Combine(packageDirectory, "broken-tool", "fsgg-fsharp-surface")
                              let brokenExit, _, _ = runProcess root brokenCommand [ "--root"; root; "--project"; "Broken.fsproj" ]
                              Expect.notEqual brokenExit 0 "a package with its required DesignChecks dependency removed cannot execute the producer")
                  finally Directory.Delete(packageDirectory, true) }

              test "release workflow publishes and smoke-gates the package-only producer" {
                  let workflow = File.ReadAllText(Path.Combine(repoRoot, ".github", "workflows", "publish.yml"))
                  Expect.isTrue (requiredPublicationRoute workflow) "the release topology includes pack, installed-tool smoke, and both feed pushes"
                  let mutation = workflow.Replace("publish-fsharp-surface-command:", "publish-fsharp-surface-command-removed:")
                  Expect.isFalse (requiredPublicationRoute mutation) "MUTATION: removing the producer publication route makes the release topology guard red" }
            ]
