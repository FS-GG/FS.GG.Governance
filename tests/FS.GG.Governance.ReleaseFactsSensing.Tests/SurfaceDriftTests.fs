module FS.GG.Governance.ReleaseFactsSensing.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.ReleaseFactsSensing.Model

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, research D4/D5), now via the
// shared SurfaceDrift helper (101/M-CI-3).

let private library = typeof<SensingDiagnostic>.Assembly

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "ReleaseFactsSensing" "FS.GG.Governance.ReleaseFactsSensing" library

          SurfaceDrift.referencesOnly "ReleaseFactsSensing" (fun n -> n.StartsWith "FS.GG.Governance.") library

          test "no network / hosting-provider / registry symbol is referenced (SC-004, FR-007)" {
              // Read-only LOCAL files only — never a registry, publishing provider, or other endpoint. Guard
              // against a network namespace or VCS SDK creeping into the sensing library.
              let banned =
                  [ "System.Net.Http"
                    "System.Net.Sockets"
                    "Octokit"
                    "GitHub"
                    "LibGit2Sharp" ]

              let referenced =
                  library.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)

              for b in banned do
                  Expect.isFalse
                      (referenced |> Array.exists (fun n -> n.Contains b))
                      (sprintf "ReleaseFactsSensing must not reference %s (no network / hosting-provider, SC-004)" b)
          } ]
