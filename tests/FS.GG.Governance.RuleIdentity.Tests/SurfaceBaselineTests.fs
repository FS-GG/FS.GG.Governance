module FS.GG.Governance.RuleIdentity.Tests.SurfaceBaselineTests

open System
open System.IO
open System.Reflection
open Expecto
open FS.GG.Governance.RuleIdentity

// Reflective API surface-drift + dependency/scope-hygiene checks for the 068 leaf (Principle II, plan D7).
// Reflection lives ONLY in these tests, never in the library. Blessed via BLESS_SURFACE=1 (T025).

// ── Repo root (for the surface baseline path) ──

let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        if File.Exists(Path.Combine(d.FullName, "FS.GG.Governance.sln")) then d.FullName
        else findRepoRoot d.Parent

let private repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))

// Touch a member of the public module to force the library assembly to load, then locate it by name.
let private ruleIdentityAsm =
    RuleIdentity.ruleIdToken (RuleIdentity.gate "load") |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.RuleIdentity"
        | None -> false)

let private baselinePath =
    Path.Combine(repoRoot, "surface", "FS.GG.Governance.RuleIdentity.surface.txt")

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
        [ test "RuleIdentity public surface equals the committed baseline" {
              let actual = renderSurface ruleIdentityAsm

              // Bless path: BLESS_SURFACE=1 (re)writes the baseline intentionally.
              if Environment.GetEnvironmentVariable "BLESS_SURFACE" = "1" then
                  File.WriteAllText(baselinePath, actual + "\n")

              let baseline = File.ReadAllText baselinePath

              Expect.equal
                  (normalize actual)
                  (normalize baseline)
                  "public surface drifted — if intended, regenerate with BLESS_SURFACE=1 dotnet test"
          }

          test "RuleIdentity references only FSharp.Core + BCL (plan D7 scope guard — no cycle)" {
              // The leaf has NO governance ProjectReference: it cannot introduce a cycle and any
              // projection may reference it (research D7).
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let offending =
                  ruleIdentityAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty
                  offending
                  (sprintf "RuleIdentity must depend on FSharp.Core/BCL only; found: %A" offending)

              // Specifically: NOT any FS.GG.Governance assembly — the leaf is dependency-free upstream.
              let forbidden =
                  ruleIdentityAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (fun n -> n.StartsWith "FS.GG.Governance")

              Expect.isEmpty
                  forbidden
                  (sprintf "RuleIdentity must not reference any governance project; found: %A" forbidden)
          } ]
