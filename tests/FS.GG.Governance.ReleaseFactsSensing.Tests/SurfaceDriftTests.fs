module FS.GG.Governance.ReleaseFactsSensing.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.ReleaseFactsSensing.Model
open FS.GG.Governance.ReleaseFactsSensing.Tests.Support

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, research D4/D5).
// Reflection lives ONLY in these tests, never in the library.

let private library = typeof<SensingDiagnostic>.Assembly

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.ReleaseFactsSensing.surface.txt")

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
        [ test "ReleaseFactsSensing public surface equals the committed baseline" {
              let actual = renderSurface library

              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "ReleaseFactsSensing references only ReleaseRules/Config + their deps + BCL/FSharp.Core" {
              // No Snapshot/Route/Gates dependency and no third-party package — the library only MIRRORS the
              // sensing shape, it does not consume Snapshot (research D5, plan Structure Decision). The
              // governance deps it DOES pull arrive transitively via ReleaseRules (Enforcement/Ship/Findings/
              // Routing/Gates) + Config — all FS.GG.Governance.*; the guard below bans network/SDK packages.
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

              Expect.isEmpty
                  offending
                  (sprintf
                      "ReleaseFactsSensing must depend on FS.GG.Governance.*/BCL/FSharp.Core only; found: %A"
                      offending)
          }

          test "no network / hosting-provider / registry symbol is referenced (SC-004, FR-007)" {
              // Read-only LOCAL files only — never a registry, publishing provider, or other endpoint. Guard
              // against a network namespace or VCS SDK creeping into the sensing library.
              let banned =
                  [ "System.Net.Http"
                    "System.Net.Sockets"
                    "Octokit"
                    "GitHub"
                    "LibGit2Sharp" ]

              let referenced =
                  library.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)

              for b in banned do
                  Expect.isFalse
                      (referenced |> Array.exists (fun n -> n.Contains b))
                      (sprintf "ReleaseFactsSensing must not reference %s (no network / hosting-provider, SC-004)" b)
          } ]
