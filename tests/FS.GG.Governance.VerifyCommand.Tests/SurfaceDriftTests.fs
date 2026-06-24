module FS.GG.Governance.VerifyCommand.Tests.SurfaceDriftTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.VerifyCommand
open FS.GG.Governance.VerifyCommand.Tests.Support

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II). Reflection lives ONLY in
// these tests, never in the library. The public surface is exactly the `Loop` + `Interpreter` modules (the
// two `.fsi` contracts). There is NO external surface generator — this test IS the renderer; the committed
// baseline is produced by running it once (BLESS_SURFACE=1).

let private verifyCommand =
    Loop.exitCode Loop.Success |> ignore

    AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.VerifyCommand"
        | None -> false)

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.VerifyCommand.surface.txt")

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
        [ test "VerifyCommand public surface equals the committed baseline" {
              let actual = renderSurface verifyCommand

              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "the public API surface is exactly the Loop + Interpreter modules (plus the Exe entry)" {
              let typeNames = verifyCommand.GetExportedTypes() |> Array.choose (fun t -> Option.ofObj t.FullName)

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.VerifyCommand.LoopModule"))
                  "Loop module is public"

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.VerifyCommand.InterpreterModule"))
                  "Interpreter module is public"

              let unexpected =
                  typeNames
                  |> Array.filter (fun n ->
                      not (
                          n.Contains "VerifyCommand.LoopModule"
                          || n.Contains "VerifyCommand.InterpreterModule"
                          || n.Contains "VerifyCommand.Loop+"
                          || n.Contains "VerifyCommand.Interpreter+"
                          || n.Contains "VerifyCommand.Program"))

              Expect.isEmpty unexpected (sprintf "only Loop/Interpreter (+ Program entry) are public; found extra: %A" unexpected)
          }

          test "VerifyCommand references VerifyJson (not AuditJson) and no kernel/host/cli" {
              let referenced =
                  verifyCommand.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)

              Expect.isTrue (referenced |> Array.contains "FS.GG.Governance.VerifyJson") "references VerifyJson"

              let forbidden =
                  referenced
                  |> Array.filter (fun n ->
                      n = "FS.GG.Governance.AuditJson"
                      || n = "FS.GG.Governance.RouteJson"
                      || n = "FS.GG.Governance.GatesJson"
                      || n = "FS.GG.Governance.Kernel"
                      || n = "FS.GG.Governance.Host"
                      || n = "FS.GG.Governance.Cli"
                      || n.StartsWith "FS.GG.Governance.Adapters")

              Expect.isEmpty forbidden (sprintf "verify must not reference AuditJson/RouteJson/GatesJson/kernel/host/cli/adapters; found: %A" forbidden)
          } ]
