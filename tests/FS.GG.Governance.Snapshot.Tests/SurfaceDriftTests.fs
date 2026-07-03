module FS.GG.Governance.Snapshot.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.Snapshot.Model

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, research D1). The
// surface-equality and scope-guard checks now run via the shared SurfaceDrift helper (101/M-CI-3); the
// SC-007 network/hosting-provider symbol-leak guard stays inline. Reflection lives in the helper and here.

let private snapshot = typeof<SensingDiagnosticId>.Assembly

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "Snapshot" "FS.GG.Governance.Snapshot" snapshot

          SurfaceDrift.referencesOnly "Snapshot" (fun n -> n = "FS.GG.Governance.Config") snapshot

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
