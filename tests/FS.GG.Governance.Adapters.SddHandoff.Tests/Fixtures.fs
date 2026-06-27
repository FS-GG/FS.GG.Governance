module FS.GG.Governance.Adapters.SddHandoff.Tests.Fixtures

open System
open System.IO
open FS.GG.Governance.Adapters.SddHandoff

// Shared fixture access for the consumer test suite. Fixtures are committed JSON handoffs read
// from the repo tree (not copied to the build output) — located via the repo root, mirroring the
// surface-baseline lookup. Real on-disk evidence (Constitution V).

let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        let here ext = File.Exists(Path.Combine(d.FullName, "FS.GG.Governance." + ext))
        if here "sln" || here "slnx" then d.FullName else findRepoRoot d.Parent

let repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))

let fixturesDir =
    Path.Combine(repoRoot, "tests", "FS.GG.Governance.Adapters.SddHandoff.Tests", "fixtures")

/// The raw JSON text of fixture `name` (without the `.json` suffix).
let json (name: string) : string =
    File.ReadAllText(Path.Combine(fixturesDir, name + ".json"))

/// A `Reader.HandoffRead` over fixture `name`, with a deterministic `readiness/<name>/...` source.
let read (name: string) : Reader.HandoffRead =
    { Source = sprintf "readiness/%s/governance-handoff.json" name
      Json = json name }
