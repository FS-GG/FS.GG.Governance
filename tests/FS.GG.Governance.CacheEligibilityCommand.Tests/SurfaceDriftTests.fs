module FS.GG.Governance.CacheEligibilityCommand.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.CacheEligibilityCommand
open FS.GG.Governance.CacheEligibilityCommand.Tests.Support

// T026/T027 (Principle II, C6) — reflective API surface-drift baseline + dependency/scope-hygiene guard.
// Reflection lives ONLY in these tests. The public surface is exactly the `Loop` + `Interpreter` modules
// (the two `.fsi` contracts); the dependency boundary is the F022 selection cores + the cache cores (+
// transitive FreshnessKey) + BCL + FSharp.Core — and NO RouteJson/GatesJson/AuditJson/RouteCommand (C6).

let private commandAsm =
    Loop.exitCode Loop.Success |> ignore

    AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.CacheEligibilityCommand"
        | None -> false)

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.CacheEligibilityCommand.surface.txt")

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
        [ test "CacheEligibilityCommand public surface equals the committed baseline" {
              let actual = renderSurface commandAsm

              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "the public API surface is exactly the Loop + Interpreter modules (plus the Exe entry)" {
              let typeNames = commandAsm.GetExportedTypes() |> Array.choose (fun t -> Option.ofObj t.FullName)

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.CacheEligibilityCommand.LoopModule"))
                  "Loop module is public"

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.CacheEligibilityCommand.InterpreterModule"))
                  "Interpreter module is public"

              let unexpected =
                  typeNames
                  |> Array.filter (fun n ->
                      not (
                          n.Contains "CacheEligibilityCommand.LoopModule"
                          || n.Contains "CacheEligibilityCommand.InterpreterModule"
                          || n.Contains "CacheEligibilityCommand.Loop+"
                          || n.Contains "CacheEligibilityCommand.Interpreter+"
                          || n.Contains "CacheEligibilityCommand.Program"))

              Expect.isEmpty unexpected (sprintf "only Loop/Interpreter (+ Program entry) are public; found extra: %A" unexpected)
          }

          test "references only the F022 selection + cache cores (+ transitive) + BCL — no RouteJson/GatesJson/AuditJson/RouteCommand (C6)" {
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.Config"
                  || name = "FS.GG.Governance.Snapshot"
                  || name = "FS.GG.Governance.Routing"
                  || name = "FS.GG.Governance.Findings"
                  || name = "FS.GG.Governance.Gates"
                  || name = "FS.GG.Governance.Route"
                  || name = "FS.GG.Governance.FreshnessKey"
                  || name = "FS.GG.Governance.FreshnessResolution"
                  || name = "FS.GG.Governance.CacheEligibility"
                  || name = "FS.GG.Governance.CacheEligibilityJson"
                  || name = "FS.GG.Governance.EvidenceReuse"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let offending =
                  commandAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty offending (sprintf "must depend on the selection/cache cores + BCL/FSharp.Core only; found: %A" offending)

              let forbidden =
                  commandAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (fun n ->
                      n = "FS.GG.Governance.RouteJson"
                      || n = "FS.GG.Governance.GatesJson"
                      || n = "FS.GG.Governance.AuditJson"
                      || n = "FS.GG.Governance.RouteCommand"
                      || n = "FS.GG.Governance.Kernel"
                      || n = "FS.GG.Governance.Host"
                      || n = "FS.GG.Governance.Cli"
                      || n.StartsWith "FS.GG.Governance.Adapters")

              Expect.isEmpty forbidden (sprintf "must not reference route/audit json, RouteCommand, kernel/host/cli/adapters; found: %A" forbidden)
          } ]
