module FS.GG.Governance.Snapshot.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.Snapshot.Model
open FS.GG.Governance.Snapshot.Tests.Support

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, research D1).
// Reflection lives ONLY in these tests, never in the library.

let private snapshot = typeof<SensingDiagnosticId>.Assembly

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.Snapshot.surface.txt")

let private renderSurface (asm: Assembly) =
    let memberFlags =
        BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.Static ||| BindingFlags.DeclaredOnly

    asm.GetExportedTypes()
    |> Array.sortBy (fun t -> t.FullName)
    |> Array.map (fun t ->
        let members =
            t.GetMembers(memberFlags)
            |> Array.map (fun m -> sprintf "  [%A] %s" m.MemberType (m.ToString()))
            |> Array.sort

        String.concat "\n" (Array.append [| sprintf "TYPE %s" t.FullName |] members))
    |> String.concat "\n"

let private normalize (s: string) = s.Replace("\r\n", "\n").TrimEnd()

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ test "Snapshot public surface equals the committed baseline" {
              let actual = renderSurface snapshot

              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "Snapshot references only FS.GG.Governance.Config + BCL + FSharp.Core (FR-013/FR-014 scope guard)" {
              // No kernel/host/adapter/CLI/Routing dependency, and no git/CI/gate/enforcement package
              // — the absence confirms no later-phase capability leaked in (FR-013) and that Snapshot
              // does not pull in the Routing surface (the SC-001 feed-through lives in TESTS only).
              // The transitive YamlDotNet arrives only via Config and is unused by Snapshot's code.
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.Config"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let offending =
                  snapshot.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty
                  offending
                  (sprintf "Snapshot must depend on FS.GG.Governance.Config/BCL/FSharp.Core only; found: %A" offending)
          }

          test "no hosting-provider/network symbol is referenced anywhere in the library (SC-007)" {
              // Read-only git + environment only — never a hosting-provider API. Guard against a
              // network namespace creeping into the sensing library.
              let banned =
                  [ "System.Net.Http"; "System.Net.Sockets"; "Octokit"; "GitHub"; "LibGit2Sharp" ]

              let referenced =
                  snapshot.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)

              for b in banned do
                  Expect.isFalse
                      (referenced |> Array.exists (fun n -> n.Contains b))
                      (sprintf "Snapshot must not reference %s (no network / hosting-provider API, SC-007)" b)
          } ]
