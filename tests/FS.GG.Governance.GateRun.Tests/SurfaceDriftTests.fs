module FS.GG.Governance.GateRun.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.GateRun.Model
open FS.GG.Governance.GateRun.Tests.Support

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, the GateExecution precedent).
// Reflection lives ONLY in these tests, never in the library. The check inspects the PRODUCTION assembly, not
// the test assembly — the test project's EvidenceCapture reference is deliberately excluded.

let private gateRun = typeof<GateOutcome>.Assembly

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.GateRun.surface.txt")

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
        [ test "GateRun public surface equals the committed baseline" {
              let actual = renderSurface gateRun

              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "GateRun references only its declared graph + BCL + FSharp.Core (scope guard)" {
              // The PRODUCTION assembly must depend ONLY on its five declared ProjectReferences (+ transitive
              // F032/F014 + FSharp.Core/BCL). It must NOT reference EvidenceCapture (the round-trip dep lives in
              // TESTS only), RouteJson/AuditJson, Enforcement/Ship, RouteCommand/ShipCommand, or any third-party.
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.GateExecution"
                  || name = "FS.GG.Governance.ExecutionRecord"
                  || name = "FS.GG.Governance.CommandRecord"
                  || name = "FS.GG.Governance.EvidenceReuse"
                  || name = "FS.GG.Governance.FreshnessKey"
                  || name = "FS.GG.Governance.Config"
                  || name = "FS.GG.Governance.Gates"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let offending =
                  gateRun.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty
                  offending
                  (sprintf "GateRun must depend on its declared graph/BCL/FSharp.Core only; found: %A" offending)
          }

          test "no capture/host/enforcement symbol leaked into the production library (scope hygiene)" {
              let banned =
                  [ "FS.GG.Governance.EvidenceCapture"
                    "FS.GG.Governance.EvidenceReuseStore"
                    "FS.GG.Governance.FreshnessSensing"
                    "FS.GG.Governance.CacheEligibility"
                    "FS.GG.Governance.RouteJson"
                    "FS.GG.Governance.AuditJson"
                    "FS.GG.Governance.Enforcement"
                    "FS.GG.Governance.Ship"
                    "FS.GG.Governance.RouteCommand"
                    "FS.GG.Governance.ShipCommand"
                    "FS.GG.Governance.Snapshot"
                    "FS.GG.Governance.Host"
                    "FS.GG.Governance.Cli" ]

              let referenced =
                  gateRun.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)

              for b in banned do
                  Expect.isFalse
                      (referenced |> Array.exists (fun n -> n.Contains b))
                      (sprintf "GateRun must not reference %s (pure helper layer, scope hygiene)" b)
          } ]
