module FS.GG.Governance.AdvisoryPromotion.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.AdvisoryPromotion
open FS.GG.Governance.AdvisoryPromotion.Tests.Support

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, plan D1/D3, SC-007). Reflection
// lives ONLY in these tests, never in the library.

// Touch a member of each public module to force the library assembly to load, then locate it by name.
let private advisoryPromotionAsm =
    AdvisoryPromotion.satisfiedBases (AdvisoryPromotion.decide (facts None 0 3 None)) |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.AdvisoryPromotion"
        | None -> false)

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.AdvisoryPromotion.surface.txt")

/// Render the assembly's public surface to canonical, sorted text. Any change to the public surface changes
/// this text and trips the baseline assertion.
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
        [ test "AdvisoryPromotion public surface equals the committed baseline" {
              let actual = renderSurface advisoryPromotionAsm

              // Bless path: BLESS_SURFACE=1 (re)writes the baseline intentionally.
              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "the public surface is exactly the two modules (Model + AdvisoryPromotion), nothing else" {
              let typeNames =
                  advisoryPromotionAsm.GetExportedTypes() |> Array.choose (fun t -> Option.ofObj t.FullName)

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.AdvisoryPromotion.ModelModule"))
                  "Model module is public"
              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.AdvisoryPromotion.AdvisoryPromotionModule"))
                  "AdvisoryPromotion operations module is public"
              Expect.isFalse
                  (typeNames
                   |> Array.exists (fun n ->
                       let l = n.ToLowerInvariant()
                       l.Contains "helper" || l.Contains "internal"))
                  "no helper/internal module leaks into the public surface"
          }

          test "AdvisoryPromotion references only EvidenceReuse (+transitive cores)/BCL/FSharp.Core (plan D1/D3 scope guard)" {
              // F036 single-sibling shape: AdvisoryPromotion -> EvidenceReuse (F030) -> FreshnessKey (F029) ->
              // Config (F014). No Gates/Snapshot/Route/Findings/Enforcement/VerdictReuse/ReviewRecord/
              // PromptIsolation/AgentReviewKey, no host/adapter/CLI edge, and no new third-party package.
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.EvidenceReuse"
                  || name = "FS.GG.Governance.FreshnessKey"
                  || name = "FS.GG.Governance.Config"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let offending =
                  advisoryPromotionAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty
                  offending
                  (sprintf "AdvisoryPromotion must depend on EvidenceReuse/(transitive cores)/BCL/FSharp.Core only; found: %A" offending)

              // Specifically: NOT Gates/Snapshot/Route/Findings/Enforcement/VerdictReuse/ReviewRecord/
              // PromptIsolation/AgentReviewKey/Adapters/Host/CLI/etc.
              let forbidden =
                  advisoryPromotionAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (fun n ->
                      n = "FS.GG.Governance.Gates"
                      || n = "FS.GG.Governance.Snapshot"
                      || n = "FS.GG.Governance.Route"
                      || n = "FS.GG.Governance.Routing"
                      || n = "FS.GG.Governance.Findings"
                      || n = "FS.GG.Governance.Enforcement"
                      || n = "FS.GG.Governance.VerdictReuse"
                      || n = "FS.GG.Governance.ReviewRecord"
                      || n = "FS.GG.Governance.PromptIsolation"
                      || n = "FS.GG.Governance.AgentReviewKey"
                      || n = "FS.GG.Governance.Ship"
                      || n = "FS.GG.Governance.AuditJson"
                      || n = "FS.GG.Governance.Host"
                      || n = "FS.GG.Governance.Cli"
                      || n.StartsWith "FS.GG.Governance.Adapters")

              Expect.isEmpty
                  forbidden
                  (sprintf "AdvisoryPromotion must not reference Gates/Snapshot/Route/Findings/Enforcement/VerdictReuse/ReviewRecord/PromptIsolation/AgentReviewKey/host/adapters/CLI; found: %A" forbidden)
          } ]
