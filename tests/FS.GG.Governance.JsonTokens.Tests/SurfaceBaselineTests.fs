module FS.GG.Governance.JsonTokens.Tests.SurfaceBaselineTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.JsonTokens

// Reflective API surface-drift + dependency/scope-hygiene checks for the 073 JsonTokens leaf
// (Principle II), now via the shared SurfaceDrift helper (101/M-CI-3). The bespoke forbidden-edge scope
// guard stays inline (it is a deny-list, not an allow-list).

let private jsonTokensAsm = SurfaceDrift.assemblyNamed "FS.GG.Governance.JsonTokens"

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "JsonTokens" "FS.GG.Governance.JsonTokens" jsonTokensAsm

          test "JsonTokens takes no kernel/host/projection edge (scope guard — pure token leaf)" {
              // The leaf references ONLY the domain-enum owners (Config/GateRun/Enforcement/Ship) and their
              // transitive domain graph. It must NOT reach the kernel/host capability the pure projections
              // exclude, NOT any *Json projection, and NOT a sibling JSON leaf — it sits ABOVE the domain
              // owners and BELOW the projections.
              let forbidden (n: string) =
                  n = "FS.GG.Governance.Kernel"
                  || n = "FS.GG.Governance.Host"
                  || n = "FS.GG.Governance.Cli"
                  || n = "FS.GG.Governance.Snapshot"
                  || n = "FS.GG.Governance.JsonText"
                  || n = "FS.GG.Governance.JsonWriters"
                  || n.StartsWith "FS.GG.Governance.Adapters"
                  || (n.StartsWith "FS.GG.Governance" && n.EndsWith "Json")

              let offending =
                  jsonTokensAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter forbidden

              Expect.isEmpty
                  offending
                  (sprintf "JsonTokens must not reference kernel/host/projection/sibling-leaf; found: %A" offending)
          } ]
