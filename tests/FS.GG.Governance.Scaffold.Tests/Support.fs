module FS.GG.Governance.Scaffold.Tests.Support

open System
open System.IO
open FS.GG.Governance.Scaffold
open FS.GG.Governance.Scaffold.Model

// Fixture builders for the seam tests. The ONLY synthetic element is the deliberately out-of-scope
// provider CONTENT: a fake in-proc `TemplateProvider` value that DESCRIBES a couple of target-relative
// files and never writes (research D8, Principle V). Every filesystem effect is exercised against a
// REAL temp directory via `Interpreter.realPorts`. The fakes carry the `Synthetic` token in the test
// names that exercise them, and a `// SYNTHETIC:` comment at each use site.

// ── repo root (for the surface-drift baseline check, Principle II) ──

let rec private findRepoRoot (dir: DirectoryInfo | null) : string =
    match dir with
    | null -> failwith "repo root (FS.GG.Governance.sln) not found"
    | d ->
        let here ext =
            File.Exists(Path.Combine(d.FullName, "FS.GG.Governance." + ext))

        if here "sln" || here "slnx" then d.FullName else findRepoRoot d.Parent

let repoRoot = findRepoRoot (DirectoryInfo(AppContext.BaseDirectory))

// ── fake in-proc providers (SYNTHETIC provider content) ──

/// A fake provider declaring the supported contract version (1.0) that emits the given target-relative
/// (path, contents) files. The provider only DESCRIBES — the tool writes.
let fakeProvider (id: string) (files: (string * string) list) : TemplateProvider =
    // SYNTHETIC: stands in for the out-of-scope concrete runtime provider (research D8); emits a fixed
    // file description and never touches the filesystem.
    { Id = ProviderId id
      ContractVersion = { Major = 1; Minor = 0 }
      Emit =
        fun _request ->
            Ok
                { Files =
                    files
                    |> List.map (fun (p, c) -> { RelativePath = p; Contents = c }) } }

/// A fake provider at an arbitrary declared contract version (for the version-negotiation tests).
let providerAtVersion (id: string) (major: int) (minor: int) (files: (string * string) list) : TemplateProvider =
    // SYNTHETIC: out-of-scope provider content; only the declared version varies for C2 coverage.
    { fakeProvider id files with
        ContractVersion = { Major = major; Minor = minor } }

/// A fake provider whose own `Emit` fails with the given `ProviderError`.
let failingProvider (id: string) (error: ProviderError) : TemplateProvider =
    // SYNTHETIC: out-of-scope provider content; models a provider that fails to describe its skeleton.
    { Id = ProviderId id
      ContractVersion = { Major = 1; Minor = 0 }
      Emit = fun _request -> Error error }

// ── request builders ──

let requestFor (target: string) (reserved: string list) : ScaffoldRequest =
    { Target = target; ReservedPaths = reserved }

let runRequest (target: string) (reserved: string list) (provider: TemplateProvider option) : Loop.RunRequest =
    { Request = requestFor target reserved
      Provider = provider }

// ── real temp directories (real filesystem evidence) ──

/// Create a fresh, empty temp directory and return its absolute path.
let freshTempDir () : string =
    let dir = Path.Combine(Path.GetTempPath(), "fsgg-scaffold-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory dir |> ignore
    dir

/// Recursively delete a temp directory, ignoring errors.
let cleanup (dir: string) : unit =
    try
        if Directory.Exists dir then
            Directory.Delete(dir, true)
    with _ ->
        ()

/// Every file (relative to `root`) currently under `root`, ascending — for "nothing written" assertions.
let filesUnder (root: string) : string list =
    if Directory.Exists root then
        Directory.GetFiles(root, "*", SearchOption.AllDirectories)
        |> Array.map (fun f -> Path.GetRelativePath(root, f).Replace('\\', '/'))
        |> Array.sort
        |> Array.toList
    else
        []

// ── recording fake ports (for pure-edge assertions without touching disk) ──

/// A fake `Ports` recording probe/write calls, with canned probe/write results. Used to assert the edge
/// drives the pure core's effects without reaching the real filesystem.
type RecordingPorts =
    { Ports: Interpreter.Ports
      Probed: System.Collections.Generic.List<string list>
      Written: System.Collections.Generic.List<(string * string) list> }

let recordingPorts
    (emission: Result<ProviderEmission, ProviderError>)
    (existing: string list)
    (writeResult: Result<unit, string>)
    : RecordingPorts =
    let probed = System.Collections.Generic.List<string list>()
    let written = System.Collections.Generic.List<(string * string) list>()

    { Ports =
        { Invoke = fun _provider _request -> emission
          Probe =
            fun paths ->
                probed.Add paths
                Ok existing
          Write =
            fun files ->
                written.Add files
                writeResult
          Out = ignore }
      Probed = probed
      Written = written }
