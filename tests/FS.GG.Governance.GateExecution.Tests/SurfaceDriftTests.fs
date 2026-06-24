module FS.GG.Governance.GateExecution.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.GateExecution.Model
open FS.GG.Governance.GateExecution.Tests.Support

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, the Snapshot/F050 precedent).
// Reflection lives ONLY in these tests, never in the library. The check inspects the PRODUCTION assembly, not
// the test assembly — the test project's F049/F030/F029 references are deliberately excluded.

let private gateExec = typeof<ExecutionOutcome>.Assembly

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.GateExecution.surface.txt")

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
        [ test "GateExecution public surface equals the committed baseline" {
              let actual = renderSurface gateExec

              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "GateExecution references only the ExecutionRecord graph + BCL + FSharp.Core (scope guard)" {
              // The PRODUCTION assembly must depend on FS.GG.Governance.ExecutionRecord and — transitively —
              // FS.GG.Governance.CommandRecord / FS.GG.Governance.Config, plus FSharp.Core / BCL only. It must
              // NOT reference EvidenceCapture/EvidenceReuse/FreshnessKey (the close-the-loop deps live in TESTS
              // only) or any host/adapter/CLI/edge package, and no third-party package.
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.ExecutionRecord"
                  || name = "FS.GG.Governance.CommandRecord"
                  || name = "FS.GG.Governance.Config"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let offending =
                  gateExec.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty
                  offending
                  (sprintf "GateExecution must depend on the ExecutionRecord graph/BCL/FSharp.Core only; found: %A" offending)
          }

          test "no later-phase / host / network symbol leaked into the production library (SC-007)" {
              let banned =
                  [ "FS.GG.Governance.EvidenceCapture"
                    "FS.GG.Governance.EvidenceReuse"
                    "FS.GG.Governance.FreshnessKey"
                    "FS.GG.Governance.FreshnessSensing"
                    "FS.GG.Governance.EvidenceReuseStore"
                    "FS.GG.Governance.CacheEligibility"
                    "FS.GG.Governance.RouteJson"
                    "FS.GG.Governance.AuditJson"
                    "FS.GG.Governance.Enforcement"
                    "FS.GG.Governance.Ship"
                    "FS.GG.Governance.Snapshot"
                    "FS.GG.Governance.Routing"
                    "FS.GG.Governance.Host"
                    "FS.GG.Governance.Cli"
                    "System.Net.Http"
                    "System.Net.Sockets"
                    "Octokit"
                    "LibGit2Sharp" ]

              let referenced =
                  gateExec.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)

              for b in banned do
                  Expect.isFalse
                      (referenced |> Array.exists (fun n -> n.Contains b))
                      (sprintf "GateExecution must not reference %s (additive, no host/network leak, SC-007)" b)
          } ]
