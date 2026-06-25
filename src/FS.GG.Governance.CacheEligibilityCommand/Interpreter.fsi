// Curated public signature contract for the EDGE interpreter of the `fsgg cache-eligibility` host command
// (F044).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Interpreter.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings.
//
// This module is the IMPURE side of the Constitution's MVU boundary (Principle IV): it executes the
// `Loop.Effect`s the pure `update` requests, against INJECTED, FAKEABLE ports, and feeds each result back as
// a `Loop.Msg`. It REUSES the existing sensing edges verbatim — `Config.Loader.FileReader` for catalog reads
// (F014) and `Snapshot.Ports` for git sensing + base/head (F016) — and adds the new `FreshnessSensor` (the
// only genuinely new sensing), the read-only `StoreReader` (F030 store load), the atomic `Write`, and the
// `Out` stdout sink. It is TOTAL and SAFE (FR-010/FR-013): every port `Error` and every thrown exception is
// caught and reified to the matching `Msg` — the interpreter NEVER throws and (via temp+rename) NEVER leaves
// a partial artifact. It assembles `SensedFacts` from `RepoSnapshot.Range` (base/head) + the `FreshnessSensor`
// output and NEVER fabricates an unsensed fact (D4/L3).

namespace FS.GG.Governance.CacheEligibilityCommand

open FS.GG.Governance.Config // Loader.FileReader
open FS.GG.Governance.Config.Model // CommandId
open FS.GG.Governance.Gates.Model // Gate
open FS.GG.Governance.FreshnessKey.Model // RuleHash, ArtifactHash, CommandVersion, GeneratorVersion
open FS.GG.Governance.EvidenceReuse.Model // ReuseStore
open FS.GG.Governance.HumanText // RenderMode, ReportView (F27 wiring 063 US2)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpreter =

    /// The injected FRESHNESS-sensing port — the only genuinely new sensing this row adds. Each accessor
    /// returns `option`, carrying the "sensed-empty vs unsensed" distinction the F043 join depends on
    /// (FR-003): `SenseCoveredArtifacts g = Some []` is a SENSED-EMPTY covered set (resolves); `= None` is
    /// UNSENSED (unresolved on covered artifacts, L4). `SenseCommandVersion c = None` is unsensed ⇒ the gate
    /// resolves unresolved on command version (no-hide, FR-005) — never fabricated. The real port computes
    /// real BCL-crypto digests over real on-disk bytes; tests back it with fixed literal values (Synthetic).
    type FreshnessSensor =
        { SenseRuleHash: unit -> RuleHash option
          SenseGeneratorVersion: unit -> GeneratorVersion option
          SenseCoveredArtifacts: Gate -> ArtifactHash list option
          SenseCommandVersion: CommandId -> CommandVersion option }

    /// The injected READ-ONLY evidence-reuse store port (D6). `Ok None` = the file is ABSENT ⇒ the caller
    /// treats it as `EvidenceReuse.empty` (FR-006); `Ok (Some store)` = a present, well-formed store;
    /// `Error reason` = a present but MALFORMED store (⇒ `ToolError`, no artifact written). Writing/evicting
    /// evidence is OUT OF SCOPE this row — there is no writer here.
    type StoreReader = string -> Result<ReuseStore option, string>

    /// The bundle of injected edge ports — everything impure the command touches. `Files`/`Git` are the
    /// REUSED F014/F016 ports; `Freshness`/`Store` are new; `Write`/`Out` mirror the RouteCommand edges.
    /// Wholly faked in tests so no real `git`/hash/filesystem is reached (FR-012, SC-007).
    type Ports =
        { Files: Loader.FileReader
          Git: FS.GG.Governance.Snapshot.Ports
          Freshness: FreshnessSensor
          Store: StoreReader
          Write: string -> string -> Result<unit, string>
          Out: string -> unit
          /// F27 wiring (063) US2: sense the terminal capability (TTY/NO_COLOR/width) + the `--plain` flag
          /// into a `ColorCapability` — the ONLY sensing point (FR-004). `realPorts` wires
          /// `Capability.senseCapability`; tests inject a synthetic capability to exercise the Rich path.
          SenseCapability: bool -> RenderMode.ColorCapability
          /// F27 wiring (063) US2: render the report view richly to the terminal (the `Rich` path). `realPorts`
          /// wires `RichRender.emitStdout Rich` so NO host references Spectre directly (FR-011, SC-007); tests
          /// inject a capturing renderer. Plain/Json still go via `Out`.
          RenderReport: ReportView.ReportView -> unit }

    /// Build the REAL ports for a repository working directory: `Config.Loader.fileSystemReader repo`,
    /// `Snapshot.Interpreter.realPorts repo`, a real BCL-crypto `FreshnessSensor` (real SHA-256 over the
    /// on-disk catalog/source bytes — a coarse MVP sensing; finer per-gate scoping is a documented later
    /// refinement), a real read-only `StoreReader` deserializing `fsgg.evidence-reuse-store/v1`, a temp+rename
    /// atomic `Write`, and a `Console.Out` sink. Reaches NO network. It NEVER fabricates an unsensed fact.
    val realPorts: repo: string -> Ports

    /// Execute ONE `Loop.Effect` against the ports and return its result `Loop.Msg`. TOTAL and SAFE: catches
    /// every port `Error` and thrown exception, reifying it to the matching `Msg`. NEVER throws.
    val step: ports: Ports -> effect: Loop.Effect -> Loop.Msg

    /// The interpreter loop: `Loop.init` the request, thread each emitted `Effect` through `step`, feed every
    /// result `Msg` back into `Loop.update`, and stop at `Done`. Returns the terminal `Loop.Model` (carrying
    /// the decided `ExitDecision`). TOTAL — never throws.
    val run: ports: Ports -> request: Loop.RunRequest -> Loop.Model
