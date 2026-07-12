module FS.GG.Governance.VerifyCommand.Tests.ScopeGuardTests

open System
open Expecto
open FS.GG.Governance.VerifyCommand
open FS.GG.Governance.Tests.Common

// T034 (Polish) — the network-free guarantee (FR-014/SC-007): the command's reachable assembly surface
// references no networking assembly; reads are System.IO-only (the F054/ShipCommand scope-guard precedent).

let private verifyCommand = SurfaceDrift.assemblyNamed "FS.GG.Governance.VerifyCommand"

[<Tests>]
let tests =
    testList
        "ScopeGuard (Polish)"
        [ test "VerifyCommand references no networking assembly (network-free own logic)" {
              let networking =
                  verifyCommand.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (fun n ->
                      n.StartsWith "System.Net"
                      || n = "System.Net.Http"
                      || n.Contains "HttpClient"
                      || n.StartsWith "System.Net.Sockets")

              Expect.isEmpty networking (sprintf "no networking assembly may be referenced; found: %A" networking)
          } ]
