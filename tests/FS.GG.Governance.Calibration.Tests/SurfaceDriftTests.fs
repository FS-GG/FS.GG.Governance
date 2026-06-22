module FS.GG.Governance.Calibration.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.Calibration
open FS.GG.Governance.Calibration.Tests.Support

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, plan D1/D3, SC-007). Reflection
// lives ONLY in these tests, never in the library.

// Touch a member of each public module to force the library assembly to load, then locate it by name.
let private calibrationAsm =
    Calibration.sampleCountValue (Calibration.observedSampleCount (evidenceOf 0 0)) |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.Calibration"
        | None -> false)

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.Calibration.surface.txt")

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
        [ test "Calibration public surface equals the committed baseline" {
              let actual = renderSurface calibrationAsm

              // Bless path: BLESS_SURFACE=1 (re)writes the baseline intentionally.
              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "the public surface is exactly the two modules (Model + Calibration), nothing else" {
              let typeNames =
                  calibrationAsm.GetExportedTypes() |> Array.choose (fun t -> Option.ofObj t.FullName)

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.Calibration.ModelModule"))
                  "Model module is public"
              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.Calibration.CalibrationModule"))
                  "Calibration operations module is public"
              Expect.isFalse
                  (typeNames
                   |> Array.exists (fun n ->
                       let l = n.ToLowerInvariant()
                       l.Contains "helper" || l.Contains "internal"))
                  "no helper/internal module leaks into the public surface"
          }

          test "Calibration references only AgentReviewKey/ReviewRecord (+transitive cores)/BCL/FSharp.Core (plan D1/D3 scope guard)" {
              // F040 two-sibling shape: Calibration -> AgentReviewKey (F035) and -> ReviewRecord (F038), whose
              // transitive pure cores (PromptIsolation, SensedMetadata, FreshnessKey, Config) arrive unused. No
              // Gates/Snapshot/Route/Findings/Enforcement/VerdictReuse/AdvisoryPromotion, no host/adapter/CLI
              // edge, and no new third-party package.
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.AgentReviewKey"
                  || name = "FS.GG.Governance.ReviewRecord"
                  || name = "FS.GG.Governance.PromptIsolation"
                  || name = "FS.GG.Governance.SensedMetadata"
                  || name = "FS.GG.Governance.FreshnessKey"
                  || name = "FS.GG.Governance.Config"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let offending =
                  calibrationAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty
                  offending
                  (sprintf "Calibration must depend on AgentReviewKey/ReviewRecord/(transitive cores)/BCL/FSharp.Core only; found: %A" offending)

              // Specifically: NOT Gates/Snapshot/Route/Findings/Enforcement/VerdictReuse/AdvisoryPromotion/
              // Adapters/Host/CLI/Ship/AuditJson/etc.
              let forbidden =
                  calibrationAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (fun n ->
                      n = "FS.GG.Governance.Gates"
                      || n = "FS.GG.Governance.Snapshot"
                      || n = "FS.GG.Governance.Route"
                      || n = "FS.GG.Governance.Routing"
                      || n = "FS.GG.Governance.Findings"
                      || n = "FS.GG.Governance.Enforcement"
                      || n = "FS.GG.Governance.VerdictReuse"
                      || n = "FS.GG.Governance.AdvisoryPromotion"
                      || n = "FS.GG.Governance.Ship"
                      || n = "FS.GG.Governance.AuditJson"
                      || n = "FS.GG.Governance.Host"
                      || n = "FS.GG.Governance.Cli"
                      || n.StartsWith "FS.GG.Governance.Adapters")

              Expect.isEmpty
                  forbidden
                  (sprintf "Calibration must not reference Gates/Snapshot/Route/Findings/Enforcement/VerdictReuse/AdvisoryPromotion/Ship/AuditJson/host/adapters/CLI; found: %A" forbidden)
          } ]
