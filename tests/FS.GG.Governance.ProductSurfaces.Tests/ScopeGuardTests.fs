module FS.GG.Governance.ProductSurfaces.Tests.ScopeGuardTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Routing
open FS.GG.Governance.ProductSurfaces
open FS.GG.Governance.ProductSurfaces.Model
open FS.GG.Governance.ProductSurfaces.Tests.Support

// US3 — the pure-leaf scope guard (SC-004/SC-007): the ProductSurfaces reachable assembly references no
// network API and depends only on Config + Routing + BCL + FSharp.Core; `classify` is a pure function of
// its three inputs (no I/O/clock/git — identical for identical input).

let private surfaceAsm = typeof<ClassificationReason>.Assembly

[<Tests>]
let tests =
    testList
        "ProductSurfaces.ScopeGuard.US3"
        [ test "references only Config + Routing + BCL + FSharp.Core (no network/host/CLI/git)" {
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.Config"
                  || name = "FS.GG.Governance.Routing"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let offending =
                  surfaceAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty offending (sprintf "ProductSurfaces must depend on Config/Routing/BCL/FSharp.Core only; found: %A" offending)

              let forbidden =
                  surfaceAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (fun n ->
                      n.StartsWith "System.Net"
                      || n = "FS.GG.Governance.Kernel"
                      || n = "FS.GG.Governance.Host"
                      || n = "FS.GG.Governance.Snapshot"
                      || n = "FS.GG.Governance.Cli"
                      || n.StartsWith "FS.GG.Governance.Adapters")

              Expect.isEmpty forbidden (sprintf "ProductSurfaces must not reference network/kernel/host/Snapshot/CLI/adapters; found: %A" forbidden)
          }

          test "classify is a pure function of its three inputs (identical output for identical input)" {
              let facts = factsOf "product-surface-all-kinds"
              let paths = [ "src/Api.fsi"; "docs/guide.md"; "release/notes.md" ] |> List.map normalizePath
              let report = Routing.route facts paths
              let a = ProductSurfaces.classify facts report (ProfileId "standard")
              let b = ProductSurfaces.classify facts report (ProfileId "standard")
              Expect.equal a b "classify is deterministic and side-effect-free"
          } ]
