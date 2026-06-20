module FS.GG.Governance.Config.Tests.Support

open System
open System.IO
open FS.GG.Governance.Config

// Locate fixtures on the REAL filesystem (Principle V): walk up from the test binary to
// the repo root (the dir holding the solution), then into the test project's fixtures.

let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        let here ext = File.Exists(Path.Combine(d.FullName, "FS.GG.Governance." + ext))
        if here "sln" || here "slnx" then d.FullName else findRepoRoot d.Parent

let repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))

/// The on-disk `.fsgg` parent directory for a named fixture.
let fixtureDir (name: string) =
    Path.Combine(repoRoot, "tests", "FS.GG.Governance.Config.Tests", "fixtures", name)

/// Validate a named fixture through the real Loader edge (real filesystem read).
let validateFixture (name: string) = Loader.loadAndValidate (fixtureDir name)
