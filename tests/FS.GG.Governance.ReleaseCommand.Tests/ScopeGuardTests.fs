module FS.GG.Governance.ReleaseCommand.Tests.ScopeGuardTests

open System
open Expecto
open FS.GG.Governance.ReleaseCommand

// Network-free guarantee (SC-008, the F054 scope-guard precedent): the command's reachable assembly surface
// has NO network / hosting-provider / registry / VCS-SDK dependency. Reads are `System.IO`-only; the only
// third-party package is YamlDotNet (the row-local release.yml parse). Reflection lives ONLY in this test.

let private library = typeof<Loop.RunRequest>.Assembly

[<Tests>]
let tests =
    testList
        "ScopeGuard"
        [ test "ReleaseCommand references only FS.GG.Governance.*/BCL/FSharp.Core/YamlDotNet" {
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "YamlDotNet"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."
                  || name.StartsWith "FS.GG.Governance."

              let offending =
                  library.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty
                  offending
                  (sprintf "ReleaseCommand must depend on FS.GG.Governance.*/BCL/FSharp.Core/YamlDotNet only; found: %A" offending)
          }

          test "no network / hosting-provider / registry / VCS symbol is referenced (SC-008)" {
              let banned =
                  [ "System.Net.Http"; "System.Net.Sockets"; "Octokit"; "GitHub"; "LibGit2Sharp" ]

              let referenced =
                  library.GetReferencedAssemblies() |> Array.choose (fun a -> Option.ofObj a.Name)

              for b in banned do
                  Expect.isFalse
                      (referenced |> Array.exists (fun n -> n.Contains b))
                      (sprintf "ReleaseCommand must not reference %s (network-free, SC-008)" b)
          } ]
