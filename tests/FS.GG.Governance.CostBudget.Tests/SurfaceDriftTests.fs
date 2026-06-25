module FS.GG.Governance.CostBudget.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.CostBudget
open FS.GG.Governance.CostBudget.Tests.Support

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, SC-008). Reflection lives ONLY
// in these tests, never in the library.

let private costBudgetAsm =
    Budget.budgetFor FS.GG.Governance.Enforcement.Enforcement.Light FS.GG.Governance.Enforcement.Enforcement.Inner |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.CostBudget"
        | None -> false)

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.CostBudget.surface.txt")

let private renderSurface (asm: Assembly) =
    let memberFlags =
        BindingFlags.Public
        ||| BindingFlags.Instance
        ||| BindingFlags.Static
        ||| BindingFlags.DeclaredOnly

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
        [ test "CostBudget public surface equals the committed baseline" {
              let actual = renderSurface costBudgetAsm

              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "CostBudget never references AdvisoryPromotion (F039) or any host/CLI assembly (FR-010 scope guard)" {
              let referenced =
                  costBudgetAsm.GetReferencedAssemblies() |> Array.choose (fun a -> Option.ofObj a.Name)

              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.Config"
                  || name = "FS.GG.Governance.Enforcement"
                  || name = "FS.GG.Governance.Gates"
                  || name = "FS.GG.Governance.EvidenceReuse"
                  || name = "FS.GG.Governance.CacheEligibility"
                  || name = "FS.GG.Governance.FreshnessKey"
                  || name = "FS.GG.Governance.AgentReviewKey"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              Expect.isEmpty (referenced |> Array.filter (allowed >> not)) "only the F25 leaf deps + BCL/FSharp.Core"

              Expect.isFalse
                  (referenced |> Array.contains "FS.GG.Governance.AdvisoryPromotion")
                  "F25 never invokes AdvisoryPromotion (F039) — agent-reviewed checks stay advisory"
          } ]
