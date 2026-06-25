module FS.GG.Governance.DesignChecks.Tests.RenderFenceTests

open Expecto
open FS.GG.Governance.DesignChecks.Model

// T038 — the SC-004 render fence: FS.GG.Governance.DesignChecks references NO rendering/UI/registry/network
// API. The catalog is read only by Interpreter.DesignPort via System.IO / System.Text.Json.

let private library = typeof<DesignFacts>.Assembly

[<Tests>]
let tests =
    testList
        "DesignChecks.renderFence"
        [ test "references only FS.GG.Governance.*/BCL/FSharp.Core (no rendering/UI/registry/network)" {
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."
                  || name.StartsWith "FS.GG.Governance."

              let offending =
                  library.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty offending (sprintf "DesignChecks pulled an unexpected dependency: %A" offending)
          }

          test "no Skia/rendering/UI/registry symbol is referenced (SC-004)" {
              let banned =
                  [ "Skia"
                    "SkiaSharp"
                    "Avalonia"
                    "System.Drawing"
                    "System.Windows"
                    "Microsoft.Win32"
                    "System.Net.Http"
                    "System.Net.Sockets" ]

              let referenced =
                  library.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)

              for b in banned do
                  Expect.isFalse
                      (referenced |> Array.exists (fun n -> n.Contains b))
                      (sprintf "DesignChecks must not reference %s (render fence, SC-004)" b)
          } ]
