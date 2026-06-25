module FS.GG.Governance.RefreshCommand.Tests.ScopeGuardTests

open Expecto
open FS.GG.Governance.RefreshCommand

// Network-free guarantee (SC-007, the ReleaseCommand scope-guard precedent): the command's reachable
// assembly surface has NO network / hosting-provider / registry / VCS-SDK dependency. Reads/writes are
// `System.IO`-only and generator execution is the F051 process port. Reflection lives ONLY in this test.

let private library = typeof<Loop.RunRequest>.Assembly

[<Tests>]
let tests =
    testList
        "ScopeGuard"
        [ test "RefreshCommand references only FS.GG.Governance.*/BCL/FSharp.Core/YamlDotNet" {
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

              Expect.isEmpty offending (sprintf "found unexpected references: %A" offending)
          }

          test "no network / hosting-provider / registry / VCS symbol is referenced (SC-007)" {
              let banned = [ "System.Net.Http"; "System.Net.Sockets"; "Octokit"; "GitHub"; "LibGit2Sharp" ]

              let referenced =
                  library.GetReferencedAssemblies() |> Array.choose (fun a -> Option.ofObj a.Name)

              for b in banned do
                  Expect.isFalse
                      (referenced |> Array.exists (fun n -> n.Contains b))
                      (sprintf "RefreshCommand must not reference %s (network-free, SC-007)" b)
          } ]
