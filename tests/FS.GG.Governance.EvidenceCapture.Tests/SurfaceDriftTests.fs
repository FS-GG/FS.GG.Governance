module FS.GG.Governance.EvidenceCapture.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.EvidenceCapture
open FS.GG.Governance.EvidenceCapture.Tests.Support

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II). Reflection lives ONLY in
// these tests, never in the library.

// EvidenceCapture exports only the module (no public types). Touch a member to force the library assembly to
// load, then locate it by name among the loaded assemblies.
let private evidenceCapture =
    EvidenceCapture.referenceOf baseRecord |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.EvidenceCapture"
        | None -> false)

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.EvidenceCapture.surface.txt")

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
        [ test "EvidenceCapture public surface equals the committed baseline" {
              let actual = renderSurface evidenceCapture

              // Bless path: BLESS_SURFACE=1 (re)writes the baseline intentionally.
              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "the public surface is exactly the EvidenceCapture module, nothing private" {
              let typeNames =
                  evidenceCapture.GetExportedTypes()
                  |> Array.choose (fun t -> Option.ofObj t.FullName)
                  |> Array.sort

              // exactly one exported type: the EvidenceCapture module. No helper leak (there are no private
              // helpers — both bodies are one-line compositions).
              Expect.equal typeNames.Length 1 "exactly one exported type (the EvidenceCapture module)"

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.EvidenceCapture.EvidenceCaptureModule"))
                  "EvidenceCapture module is public"
          }

          test "EvidenceCapture references only the EvidenceReuse + CommandRecord transitive graph + BCL + FSharp.Core (scope guard)" {
              // One-way dependency: EvidenceCapture -> EvidenceReuse + CommandRecord -> FreshnessKey -> Config. No
              // FreshnessSensing/EvidenceReuseStore/CacheEligibility/host/adapter/CLI edge and no new third-party
              // package — the test project's F047/F046 references are deliberately excluded (this inspects the
              // PRODUCTION assembly, not the test assembly).
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.EvidenceReuse"
                  || name = "FS.GG.Governance.CommandRecord"
                  || name = "FS.GG.Governance.FreshnessKey"
                  || name = "FS.GG.Governance.Config"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let offending =
                  evidenceCapture.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty
                  offending
                  (sprintf "EvidenceCapture must depend on the EvidenceReuse + CommandRecord graph/BCL/FSharp.Core only; found: %A" offending)

              // Specifically NOT the reader, the store writer, the sibling projections, or any host/adapter/CLI.
              let forbidden =
                  evidenceCapture.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (fun n ->
                      n = "FS.GG.Governance.FreshnessSensing"
                      || n = "FS.GG.Governance.EvidenceReuseStore"
                      || n = "FS.GG.Governance.CacheEligibility"
                      || n = "FS.GG.Governance.CacheEligibilityJson"
                      || n = "FS.GG.Governance.RouteJson"
                      || n = "FS.GG.Governance.AuditJson"
                      || n = "FS.GG.Governance.Enforcement"
                      || n = "FS.GG.Governance.Ship"
                      || n = "FS.GG.Governance.Snapshot"
                      || n = "FS.GG.Governance.Routing"
                      || n = "FS.GG.Governance.Findings"
                      || n = "FS.GG.Governance.Host"
                      || n = "FS.GG.Governance.Cli"
                      || n.StartsWith "FS.GG.Governance.Adapters")

              Expect.isEmpty
                  forbidden
                  (sprintf "EvidenceCapture must not reference FreshnessSensing/EvidenceReuseStore/CacheEligibility*/host/adapters/CLI; found: %A" forbidden)
          } ]
