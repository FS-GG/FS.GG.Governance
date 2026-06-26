module FS.GG.Governance.Sample.SddReferenceProvider.Tests.WorkedExampleTests

open System
open System.IO
open Expecto
open FS.GG.Governance.Scaffold
open FS.GG.Governance.Scaffold.Model
open FS.GG.Governance.ScaffoldManifestJson
open FS.GG.Governance.Sample.SddReferenceProvider
open FS.GG.Governance.Sample.SddReferenceProvider.Tests.Support

// The layered worked example (US1): empty dir → disclosed lifecycle precondition → runtime layer via the
// UNCHANGED 071 seam → the skeleton appears, `dotnet build`s first-attempt, and the manifest matches a
// byte-stable golden (FR-001/FR-003/FR-004/FR-008; SC-001/SC-002/SC-003/SC-005). It drives the PUBLIC
// `Interpreter.run` surface a host would use — not internal helpers (vertical-slice rule).

/// The §R2 emitted skeleton, target-relative, for the pinned `MyApp` leaf.
let private expectedPaths =
    [ "MyApp.sln"
      "src/MyApp/MyApp.fsproj"
      "src/MyApp/Program.fs"
      "tests/MyApp.Tests/MyApp.Tests.fsproj"
      "tests/MyApp.Tests/Tests.fs"
      "README.md" ]

/// Run the worked example over a fresh empty target and return (terminal model, target).
let private runWorkedExample () =
    let target = freshTarget ()
    let req = runRequest target lifecycleReservedPaths (Some SddReferenceProvider.provider)
    let model = Interpreter.run (Interpreter.realPorts target) req
    model, target

[<Tests>]
let tests =
    testList
        "WorkedExample"
        [ test "empty dir → seam scaffolds the runtime skeleton, provider-owned, no collisions" {
              let model, target = runWorkedExample ()

              try
                  let manifest =
                      match model.Manifest with
                      | Some m -> m
                      | None -> failtest "no terminal manifest"

                  Expect.equal manifest.Outcome Scaffolded "terminal outcome is Scaffolded"
                  Expect.equal manifest.Collisions [] "no collisions against the lifecycle layer"

                  // Every emitted path is recorded ProviderOwned and exists on disk under the target.
                  let generatedPaths =
                      manifest.Generated |> List.map (fun g -> g.RelativePath) |> List.sort

                  Expect.equal generatedPaths (List.sort expectedPaths) "manifest records exactly the §R2 skeleton"

                  for g in manifest.Generated do
                      Expect.equal g.Ownership ProviderOwned (sprintf "%s is provider-owned" g.RelativePath)

                  for rel in expectedPaths do
                      Expect.isTrue
                          (File.Exists(Path.Combine(target, rel.Replace('/', Path.DirectorySeparatorChar))))
                          (sprintf "emitted file exists on disk: %s" rel)
              finally
                  cleanup target
          }

          // Real-evidence build step (FR-004, SC-002): the emitted skeleton builds first-attempt with no
          // hand-editing. A missing SDK ⇒ a NAMED skip, not a failure (research D3, Principle VI).
          test "emitted skeleton `dotnet build`s first-attempt (real evidence)" {
              let _, target = runWorkedExample ()

              try
                  let slnPath = Path.Combine(target, "MyApp.sln")

                  match dotnetBuild slnPath with
                  | SdkMissing detail ->
                      skiptestf "PREREQUISITE: .NET SDK not available to build the emitted skeleton (%s)" detail
                  | Built(exitCode, output) ->
                      Expect.equal exitCode 0 (sprintf "dotnet build MyApp.sln must succeed first-attempt:\n%s" output)
              finally
                  cleanup target
          }

          // Golden + determinism (FR-008, SC-003, SC-005). Regenerate intentionally with BLESS_FIXTURES=1.
          test "manifest projection matches the committed golden, byte-for-byte and deterministically" {
              let model1, target1 = runWorkedExample ()

              try
                  let manifest1 =
                      match model1.Manifest with
                      | Some m -> m
                      | None -> failtest "no terminal manifest"

                  let actual = ScaffoldManifestJson.ofManifest manifest1

                  if Environment.GetEnvironmentVariable "BLESS_FIXTURES" = "1" then
                      match Path.GetDirectoryName goldenPath with
                      | null -> ()
                      | dir -> Directory.CreateDirectory dir |> ignore

                      File.WriteAllText(goldenPath, actual)

                  let golden = File.ReadAllText goldenPath
                  Expect.equal actual golden "manifest projection drifted from the golden (BLESS_FIXTURES=1 to regenerate)"

                  // A SECOND fresh-target run yields the byte-identical golden — no absolute path/clock/env
                  // leakage (SC-003).
                  let model2, target2 = runWorkedExample ()

                  try
                      let manifest2 =
                          match model2.Manifest with
                          | Some m -> m
                          | None -> failtest "no terminal manifest on the second run"

                      Expect.equal (ScaffoldManifestJson.ofManifest manifest2) golden "second run is byte-identical"
                  finally
                      cleanup target2
              finally
                  cleanup target1
          } ]
