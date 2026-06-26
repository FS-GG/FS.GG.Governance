module FS.GG.Governance.Config.Tests.Support

open System
open System.IO
open FS.GG.Governance.Config
// 074: findRepoRoot consolidated into the shared RepositoryHelpers (sln||slnx superset).
let repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot

/// The on-disk `.fsgg` parent directory for a named fixture.
let fixtureDir (name: string) =
    Path.Combine(repoRoot, "tests", "FS.GG.Governance.Config.Tests", "fixtures", name)

/// Validate a named fixture through the real Loader edge (real filesystem read).
let validateFixture (name: string) = Loader.loadAndValidate (fixtureDir name)
