module FS.GG.Governance.ReviewRecord.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.ReviewRecord
open FS.GG.Governance.ReviewRecord.Tests.Support

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, plan D1, SC-006). Reflection
// lives ONLY in these tests, never in the library.

// Touch a member of each public module to force the library assembly to load, then locate it by name.
let private reviewRecordAsm =
    let loadRecord =
        buildOf baseRequest (modelId "m") (modelVersion "v") (promptHash "p") [] (responseDigest "r") (recordedVerdict "x") []

    ReviewRecord.identityValue (ReviewRecord.canonicalId loadRecord) |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.ReviewRecord"
        | None -> false)

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.ReviewRecord.surface.txt")

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
        [ test "ReviewRecord public surface equals the committed baseline" {
              let actual = renderSurface reviewRecordAsm

              // Bless path: BLESS_SURFACE=1 (re)writes the baseline intentionally.
              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "the public surface is exactly the two modules (Model + ReviewRecord), nothing else" {
              let typeNames =
                  reviewRecordAsm.GetExportedTypes() |> Array.choose (fun t -> Option.ofObj t.FullName)

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.ReviewRecord.ModelModule"))
                  "Model module is public"
              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.ReviewRecord.ReviewRecordModule"))
                  "ReviewRecord operations module is public"
              Expect.isFalse
                  (typeNames
                   |> Array.exists (fun n ->
                       let l = n.ToLowerInvariant()
                       l.Contains "helper" || l.Contains "internal"))
                  "no helper/internal module leaks into the public surface"
          }

          test "ReviewRecord references only PromptIsolation/SensedMetadata (+transitive cores)/BCL/FSharp.Core (plan D1 scope guard)" {
              // F033 multi-sibling shape: ReviewRecord -> PromptIsolation (F037) -> AgentReviewKey (F035) ->
              // FreshnessKey (F029) -> Config; and ReviewRecord -> SensedMetadata (F034) -> CommandRecord (F032)
              // -> Config. No git-sensing Snapshot, no Gates/Route/Findings/EvidenceReuse/VerdictReuse, no
              // host/adapter/CLI edge, and no new third-party package.
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.PromptIsolation"
                  || name = "FS.GG.Governance.SensedMetadata"
                  || name = "FS.GG.Governance.AgentReviewKey"
                  || name = "FS.GG.Governance.FreshnessKey"
                  || name = "FS.GG.Governance.CommandRecord"
                  || name = "FS.GG.Governance.Config"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let offending =
                  reviewRecordAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty
                  offending
                  (sprintf "ReviewRecord must depend on PromptIsolation/SensedMetadata/(transitive cores)/BCL/FSharp.Core only; found: %A" offending)

              // Specifically: NOT Gates/Snapshot/Route/Findings/EvidenceReuse/VerdictReuse/Adapters/Host/CLI/etc.
              let forbidden =
                  reviewRecordAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (fun n ->
                      n = "FS.GG.Governance.Gates"
                      || n = "FS.GG.Governance.Snapshot"
                      || n = "FS.GG.Governance.Route"
                      || n = "FS.GG.Governance.Routing"
                      || n = "FS.GG.Governance.Findings"
                      || n = "FS.GG.Governance.EvidenceReuse"
                      || n = "FS.GG.Governance.VerdictReuse"
                      || n = "FS.GG.Governance.Ship"
                      || n = "FS.GG.Governance.Enforcement"
                      || n = "FS.GG.Governance.AuditJson"
                      || n = "FS.GG.Governance.Host"
                      || n = "FS.GG.Governance.Cli"
                      || n.StartsWith "FS.GG.Governance.Adapters")

              Expect.isEmpty
                  forbidden
                  (sprintf "ReviewRecord must not reference Gates/Snapshot/Route/Findings/EvidenceReuse/VerdictReuse/host/adapters/CLI; found: %A" forbidden)
          } ]
