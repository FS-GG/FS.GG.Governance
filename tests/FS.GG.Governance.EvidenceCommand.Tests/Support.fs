module FS.GG.Governance.EvidenceCommand.Tests.Support

open System.IO
open FS.GG.Governance.Kernel
open FS.GG.Governance.Cli
open FS.GG.Governance.EvidenceCommand

// Shared builders + capturing fake ports for the 069 host tests. The pure `Loop` is driven with literal
// values; the edge `Interpreter` is driven against an in-memory `Ports` that captures every write and stdout
// line (no real git/filesystem reached) — except the single real-`realPorts` end-to-end proof.

/// Build one report node (the Cli `EvidenceNodeReport` shape `Project.evidenceReport` produces).
let reportNode (id: string) (declared: EvidenceState) (effective: EvidenceState) (freshness: Freshness option) (source: string) : EvidenceNodeReport =
    { Id = id
      Declared = Some declared
      Effective = Some effective
      Freshness = freshness
      Source = source }

/// Build a project evidence report.
let report (nodes: EvidenceNodeReport list) (deps: (string * string) list) : ProjectEvidenceReport =
    { Nodes = nodes
      Dependencies = deps
      Disclosures = []
      Failures = [] }

/// A capturing fake-port bundle: `SenseReport` returns the supplied result; `Write` records the (path,content)
/// pair; `Out` records each emitted line.
type Capture =
    { mutable Writes: (string * string) list
      mutable Out: string list }

let fakePorts (sensed: Result<ProjectEvidenceReport, Loop.ReportFault>) : Interpreter.Ports * Capture =
    let cap = { Writes = []; Out = [] }

    let ports: Interpreter.Ports =
        { SenseReport = fun _repo -> sensed
          Write =
            fun path content ->
                cap.Writes <- cap.Writes @ [ path, content ]
                Ok()
          Out = fun line -> cap.Out <- cap.Out @ [ line ] }

    ports, cap

/// As `fakePorts` but the `Write` port fails (to exercise the ToolError path).
let fakePortsFailingWrite (sensed: Result<ProjectEvidenceReport, Loop.ReportFault>) : Interpreter.Ports * Capture =
    let cap = { Writes = []; Out = [] }

    let ports: Interpreter.Ports =
        { SenseReport = fun _repo -> sensed
          Write = fun _path _content -> Error "disk full by fixture"
          Out = fun line -> cap.Out <- cap.Out @ [ line ] }

    ports, cap

/// A default request pointing at an out path under a unique temp directory.
let requestWith (out: string) (format: Loop.OutputFormat) : Loop.RunRequest =
    { Repo = "."
      Out = out
      Format = format
      ExplicitPlain = false }
// 074: findRepoRoot consolidated into the shared RepositoryHelpers (sln||slnx superset).
let repoRoot = FS.GG.Governance.Tests.Common.RepositoryHelpers.repoRoot

let goldenFixture = Path.Combine(repoRoot, "tests", "golden-fixture")

/// A fresh unique temp file path (never auto-created).
let tempOut (label: string) : string =
    let dir = Path.Combine(Path.GetTempPath(), "fsgg-evidence-tests", label + "-" + System.Guid.NewGuid().ToString("N"))
    Path.Combine(dir, "evidence.json")
