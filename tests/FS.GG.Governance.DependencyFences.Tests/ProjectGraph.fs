module FS.GG.Governance.DependencyFences.Tests.ProjectGraph

// 100 — the read-only project-graph the three fence guards assert against (data-model.md).
//
// This module parses the REAL tracked `.fsproj` graph and exposes pure matchers. The I/O edge
// (`git ls-files` + file reads) is isolated in `load`; every matcher below is pure and is exercised
// directly by the red-path unit tests over literal `ProjectNode` values. Following the RenameGuard
// precedent: internal helpers are `private`; the types + pure matchers are public so the red-path
// tests can construct and drive them. No `.fsi` (Tier 2, no surface-area baseline).

open System
open System.IO
open System.Diagnostics
open System.Xml.Linq

// ---------------------------------------------------------------------------------------------------
// data-model §ProjectNode — one per tracked .fsproj.
// ---------------------------------------------------------------------------------------------------

type ProjectNode =
    { Name: string // file name without extension, e.g. "FS.GG.Governance.RouteCommand"
      Path: string // repo-relative, forward-slash
      OutputType: string // "Exe" / "WinExe" / "Library" (absent ⇒ "Library")
      PackAsTool: bool
      ToolCommandName: string option
      IsPackable: bool
      PackageReferences: Set<string> // direct <PackageReference Include=...>
      ProjectReferences: Set<string> } // direct <ProjectReference>, resolved to a node Name

let isExe (n: ProjectNode) : bool =
    n.OutputType = "Exe" || n.OutputType = "WinExe"

// ---------------------------------------------------------------------------------------------------
// data-model §Violation — the per-failure value the matchers emit; drives the actionable diagnostic.
// ---------------------------------------------------------------------------------------------------

type Violation =
    { Rule: string
      Project: string
      Detail: string }

let render (v: Violation) : string =
    sprintf "[%s] %s — %s" v.Rule v.Project v.Detail

// ---------------------------------------------------------------------------------------------------
// The PURE matchers (no I/O) — data-model §FenceRule. Each takes the parsed graph (or a literal one
// in the red-path tests) and returns the violations, so it is testable without touching the tree.
// ---------------------------------------------------------------------------------------------------

/// Shared owner-fence matcher: the set of projects declaring a direct `package` reference must equal
/// `allowed` exactly. Two failure directions: an undocumented owner (has the package, not in `allowed`)
/// and a documented owner that dropped it (in `allowed`, no package). `rule`/`label` shape the
/// diagnostic; `label` is the human name used in the detail text (e.g. "YAML", "Spectre.Console").
let private packageOwnerViolations
    (rule: string)
    (package: string)
    (label: string)
    (allowed: Set<string>)
    (nodes: ProjectNode list)
    : Violation list =
    let owners =
        nodes
        |> List.filter (fun n -> n.PackageReferences.Contains package)
        |> List.map (fun n -> n.Name)
        |> Set.ofList

    let undocumented =
        Set.difference owners allowed
        |> Set.toList
        |> List.map (fun name ->
            { Rule = rule
              Project = name
              Detail = sprintf "declares a direct %s reference but is not in the documented %s-owner allowlist" package label })

    let missing =
        Set.difference allowed owners
        |> Set.toList
        |> List.map (fun name ->
            { Rule = rule
              Project = name
              Detail = sprintf "is a documented %s owner but no longer declares a direct %s reference (update the allowlist)" label package })

    undocumented @ missing

/// INV-1 — the set of projects declaring a direct YamlDotNet reference must equal `allowed` exactly.
/// Two failure directions: an undocumented owner (has YamlDotNet, not in `allowed`) and a documented
/// owner that dropped it (in `allowed`, no YamlDotNet).
let yamlOwnerViolations (allowed: Set<string>) (nodes: ProjectNode list) : Violation list =
    packageOwnerViolations "yaml-owner" "YamlDotNet" "YAML" allowed nodes

/// INV-4 (ARCH-2) — the set of projects declaring a direct Spectre.Console reference must equal
/// `allowed` exactly. Mirrors `yamlOwnerViolations`: the rich-terminal presentation package is confined
/// to a single owner (HumanRender), machine-enforced rather than prose-only.
let spectreOwnerViolations (allowed: Set<string>) (nodes: ProjectNode list) : Violation list =
    packageOwnerViolations "spectre-owner" "Spectre.Console" "Spectre.Console" allowed nodes

/// INV-2 — no Exe node may reach another Exe node via the transitive ProjectReference closure.
let exeExeEdges (nodes: ProjectNode list) : Violation list =
    let byName = nodes |> List.map (fun n -> n.Name, n) |> Map.ofList

    // Transitive reachability over ProjectReferences (the graph is a DAG; a visited-set guards anyway).
    let reachable (start: ProjectNode) : Set<string> =
        let rec walk (seen: Set<string>) (frontier: string list) =
            match frontier with
            | [] -> seen
            | name :: rest when seen.Contains name -> walk seen rest
            | name :: rest ->
                let next =
                    match Map.tryFind name byName with
                    | Some n -> n.ProjectReferences |> Set.toList
                    | None -> [] // a ref outside the tracked set (none today) — ignore.

                walk (Set.add name seen) (next @ rest)

        walk Set.empty (start.ProjectReferences |> Set.toList)

    [ for n in nodes do
          if isExe n then
              for target in reachable n |> Set.toList |> List.sort do
                  match Map.tryFind target byName with
                  | Some t when isExe t ->
                      yield
                          { Rule = "exe-leaf"
                            Project = n.Name
                            Detail = sprintf "executable reaches another executable: %s → %s" n.Name target }
                  | _ -> () ]

/// INV-3 — at most one project may claim ToolCommandName=fsgg.
let fsggClaimants (nodes: ProjectNode list) : string list =
    nodes
    |> List.filter (fun n -> n.ToolCommandName = Some "fsgg")
    |> List.map (fun n -> n.Name)
    |> List.sort

/// INV-3 (generalized, ARCH-3) — no `ToolCommandName` value may be claimed by more than one
/// publishable tool. `fsggClaimants` guards the specific `fsgg` collision; this closes the general
/// class — a second project colliding on ANY command name (e.g. a duplicate `fsgg-evidence`) installs
/// one tool over the other and would otherwise pass silently. Only packable `PackAsTool` nodes carry a
/// meaningful `ToolCommandName` (that is the value `dotnet tool install` resolves), so the group-by is
/// scoped to them; a shared name across two such projects is the violation.
let toolCommandCollisions (nodes: ProjectNode list) : Violation list =
    nodes
    |> List.filter (fun n -> n.PackAsTool && n.IsPackable)
    |> List.choose (fun n -> n.ToolCommandName |> Option.map (fun tcn -> tcn, n.Name))
    |> List.groupBy fst
    |> List.sortBy fst
    |> List.choose (fun (tcn, pairs) ->
        match pairs |> List.map snd |> List.sort with
        | _ :: _ :: _ as claimants ->
            Some
                { Rule = "tool-command-owner"
                  Project = String.concat ", " claimants
                  Detail =
                    sprintf
                        "ToolCommandName '%s' is claimed by %d publishable projects: %s"
                        tcn
                        (List.length claimants)
                        (String.concat ", " claimants) }
        | _ -> None)

// ---------------------------------------------------------------------------------------------------
// The I/O edge — parse the REAL tracked .fsproj graph. Isolated here; everything above is pure.
// ---------------------------------------------------------------------------------------------------

let private repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot

/// Shell `git ls-files` from repoRoot and keep the tracked `.fsproj` paths. Fails LOUDLY if git cannot
/// start or exits non-zero (Principle VI) — a partial graph would silently weaken every fence.
let private trackedFsprojs () : string list =
    let psi = ProcessStartInfo("git", "ls-files")
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true
    psi.UseShellExecute <- false
    psi.WorkingDirectory <- repoRoot

    use proc =
        match Process.Start psi with
        | null -> failwith "dependency-fences: could not start `git ls-files` to enumerate the tracked tree."
        | p -> p

    let stdout = proc.StandardOutput.ReadToEnd()
    let stderr = proc.StandardError.ReadToEnd()
    proc.WaitForExit()

    if proc.ExitCode <> 0 then
        failwithf "dependency-fences: `git ls-files` failed (exit %d): %s" proc.ExitCode stderr

    stdout.Split('\n')
    |> Array.map (fun s -> s.Trim().Replace('\\', '/'))
    |> Array.filter (fun s -> s.EndsWith(".fsproj", StringComparison.Ordinal))
    |> Array.toList

/// `Path.GetFileNameWithoutExtension` is nullable-typed; every path we pass is a real file name, so
/// fall back to the input rather than propagate a null (keeps the repo's nullness gate happy).
let private fileNameNoExt (p: string) : string =
    match Path.GetFileNameWithoutExtension p with
    | null -> p
    | s -> s

let private localName (e: XElement) = e.Name.LocalName

let private elements (doc: XDocument) (name: string) =
    doc.Descendants() |> Seq.filter (fun e -> localName e = name)

let private firstValue (doc: XDocument) (name: string) : string option =
    elements doc name
    |> Seq.tryHead
    |> Option.map (fun e -> e.Value.Trim())

let private includeAttrs (doc: XDocument) (name: string) : string list =
    elements doc name
    |> Seq.choose (fun e -> Option.ofObj (e.Attribute(XName.Get "Include")))
    |> Seq.map (fun a -> a.Value.Trim())
    |> Seq.toList

let private parseBool (s: string option) (dflt: bool) : bool =
    match s with
    | Some v ->
        match Boolean.TryParse v with
        | true, b -> b
        | _ -> dflt
    | None -> dflt

let private parseProject (repoRel: string) : ProjectNode =
    let full = Path.Combine(repoRoot, repoRel)
    let doc = XDocument.Load full

    let projectRefs =
        includeAttrs doc "ProjectReference"
        |> List.map (fun inc -> fileNameNoExt (inc.Replace('\\', '/')))
        |> Set.ofList

    { Name = fileNameNoExt repoRel
      Path = repoRel
      OutputType = firstValue doc "OutputType" |> Option.defaultValue "Library"
      PackAsTool = parseBool (firstValue doc "PackAsTool") false
      ToolCommandName = firstValue doc "ToolCommandName"
      IsPackable = parseBool (firstValue doc "IsPackable") true
      PackageReferences = includeAttrs doc "PackageReference" |> Set.ofList
      ProjectReferences = projectRefs }

/// The real project graph, parsed from the tracked tree. Deterministic (tracked files only).
let load () : ProjectNode list =
    trackedFsprojs () |> List.map parseProject
