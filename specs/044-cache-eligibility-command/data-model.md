# Phase 1 Data Model: Cache-Eligibility Host Command

**Feature**: `044-cache-eligibility-command` | **Date**: 2026-06-22

The command is a host/edge composition, so its "data model" is (a) the **pure `Loop` vocabulary** it adds
(`RunRequest`/`Model`/`Msg`/`Effect`/`Phase`/`ExitDecision`), (b) the **edge `Interpreter` ports**, and (c) the
**sense→resolve→evaluate→emit pipeline** that threads merged-core types end to end. All merged types are
**reused verbatim** — never redefined (FR-012). New types live in namespace
`FS.GG.Governance.CacheEligibilityCommand`, split `Loop` (vocabulary + pure transitions) then `Interpreter`
(ports + edge). New types are marked **NEW**.

## Reused vocabulary (verbatim, not redefined — FR-012)

| Type / function | Origin | Role here |
|---|---|---|
| `ScopeSelector`, atomic `ArtifactWriter`, `OutputSink` pattern | F022 `RouteCommand` | the scope flags and the edge-write/sink shape, mirrored |
| `RepoSnapshot` (`{ Range: DiffRange option; Changed; … }`), `DiffRange` (`{ Base; Head; MergeBase: CommitId }`), `Ports`/`GitPort`, `realPorts` | F016 `Snapshot` | scope sensing **and** base/head revisions (D4) |
| `Loader.FileReader`, catalog load + `Validation` (`Valid of TypedFacts` / invalid) | F014 `Config` | catalog read/validate |
| `Routing.route`, `Gates.buildRegistry`, `GateRegistry` (`{ Gates: Gate list }`), `Findings.findUnknownGovernedPaths`, `Route.select`, `RouteResult` (`{ SelectedGates: SelectedGate list; … }`), `SelectedGate` (`{ Gate: Gate; … }`) | F015/F018/F017/F019 | the F022 selection call-sequence → selected `Gate list` (D2/D3) |
| `Gate`, `GateId`, `FreshnessKey` (`{Check;Domain;Cost;Environment;Command}`), `gateIdValue` | F018 `Gates` | the selected gate, its identity, its carried five-field key |
| `SensedFacts` (`{ RuleHash:_ option; GeneratorVersion:_ option; Base:_ option; Head:_ option; CoveredArtifacts: Map<GateId, ArtifactHash list>; CommandVersions: Map<CommandId, CommandVersion> }`), `resolve`, `FreshnessResolutionReport`/`Entry`, `ResolutionOutcome` (`Resolved`/`Unresolved`), `MissingFact`, `entries`, `candidate`, `isResolved`, `missingFacts`, `missingFactToken` | F043 `FreshnessResolution` | the join the host feeds and the no-hide attribution it renders |
| `RuleHash`, `ArtifactHash`, `CommandVersion`, `GeneratorVersion`, `Revision`, `FreshnessInputs` | F029 `FreshnessKey` | the opaque sensed-fact newtypes assembled into `SensedFacts` |
| `CandidateGate` (`{ Gate: GateId; Inputs: FreshnessInputs }`), `evaluate`, `CacheEligibilityReport`, `CacheEligibilityVerdict` (`Reusable`/`MustRecompute`), `RecomputeCause` | F041 `CacheEligibility` | the reuse evaluation over resolved candidates |
| `ReuseStore` (`ReuseStore of RecordedEvidence list`), `RecordedEvidence` (`{ Inputs; Evidence }`), `EvidenceRef`, `empty` | F030 `EvidenceReuse` | the read-only store input (absent ⇒ `empty`) |
| `ofReport : CacheEligibilityReport -> string`, `schemaVersion` (`"fsgg.cache-eligibility/v1"`) | F042 `CacheEligibilityJson` | the verbatim resolved-verdict projection |

## NEW vocabulary (the minimal set the host edge adds)

### `RunRequest` — the parsed invocation (mirrors `RouteCommand`)

```fsharp
type ScopeSelector =                       // mirrors RouteCommand
    | ExplicitPaths of GovernedPath list
    | Since of rev: string
    | DefaultRange

type OutputFormat = Human | Json

type RunRequest =
    { Repo: string
      Scope: ScopeSelector
      StorePath: string                    // read-only evidence-reuse store (absent on disk ⇒ empty)
      CacheOut: string                      // cache-eligibility.json path
      UnresolvedOut: string                 // cache-eligibility.unresolved.json path (derived from CacheOut)
      Format: OutputFormat }

// NEW, local (mirrors RouteCommand.Loop.UsageError) — pure-parser rejections, each ⇒ UsageError'/exit 2.
type UsageError =
    | UnknownFlag of string
    | MissingValue of flag: string
    | PathsAndSinceTogether                 // --paths and --since together
    | EmptyPaths                            // --paths with no value
    | BadFormat of value: string            // --format other than human|json
```

**Defaults (applied in `parse`, so every path field is populated — mirrors the F022 `readiness/`-dir
precedent, D8):** `Repo = "."`; `CacheOut = <repo>/readiness/cache-eligibility.json` when `--out` is omitted;
`StorePath = <repo>/readiness/evidence-reuse.json` when `--store` is omitted; `UnresolvedOut` is **always
derived** from `CacheOut` (`…unresolved.json` stem, never a flag); `Format = Human` when `--format` is omitted.
For `--repo .` the clean relative form is used; any other repo is prefixed. The read-only store on-disk format
is fixed in [contracts/cache-eligibility-artifacts.md §A5](./contracts/cache-eligibility-artifacts.md)
(`fsgg.evidence-reuse-store/v1`).

### `Effect` — the I/O the pure `update` requests but never executes (NEW)

```fsharp
type Effect =
    | SenseScope of ScopeSelector                        // reuse Snapshot scope sensing (→ RepoSnapshot incl. Range)
    | LoadCatalog of repo: string                         // reuse Config load+validate
    | SenseFreshness of gates: Gate list * baseHead: (Revision option * Revision option)
                                                          // NEW FreshnessSensor port → SensedFacts (base/head passed through from Range)
    | LoadStore of path: string                           // NEW read-only StoreReader → ReuseStore (absent ⇒ empty)
    | WriteArtifact of kind: ArtifactKind * path: string * content: string   // atomic write
    | EmitSummary of text: string                         // stdout

and ArtifactKind = CacheArtifact | UnresolvedArtifact
```

### `Msg` — reified effect results (NEW)

```fsharp
type Msg =
    | Begin                                              // kept for the RouteCommand-parity Msg shape; init may seed it
    | Sensed of Result<RepoSnapshot, string>
    | Loaded of Validation                               // F014 Valid/invalid
    | FreshnessSensed of Result<SensedFacts, string>
    | StoreLoaded of Result<ReuseStore, string>          // absent file ⇒ Ok empty, never Error
    | Wrote of ArtifactKind * Result<unit, string>
    | Emitted
```

### `Phase`, `Model`, `ExitDecision` (NEW)

```fsharp
type Phase = Parsed | Sensed' | Loaded' | Selected | Resolved' | Evaluated | Projected | Persisted | Done

type ExitDecision = Success | UsageError' | InputUnavailable | ToolError   // NO ship/blocking verdict (FR-009)

type Model =
    { Request: RunRequest
      Phase: Phase
      Snapshot: RepoSnapshot option
      SelectedGates: Gate list                            // off RouteResult.SelectedGates (D3)
      Sensed: SensedFacts option
      Store: ReuseStore option
      Resolution: FreshnessResolutionReport option        // F043 output (resolved + unresolved entries)
      CacheDoc: string option                             // F042 ofReport of the resolved verdicts
      UnresolvedDoc: string option                        // the sidecar render
      Diagnostics: string list
      Exit: ExitDecision }
```

## The pipeline (`init` → `update`), end to end

Mirrors `RouteCommand.Loop`; the **NEW** steps are the freshness/eval/emit tail.

1. **`parse : string list -> Result<RunRequest, UsageError>`** — pure, total argv matcher (verb already
   stripped by `Program`); usage problems are values, mapped to `UsageError'` / exit 2.
2. **`init : RunRequest -> Model * Effect list`** — `ExplicitPaths` may `LoadCatalog` directly;
   `Since`/`DefaultRange` emit `SenseScope` first (the `RouteCommand` shape).
3. **`update`** (pure, total):
   - `Sensed (Ok snap)` → store snapshot (incl. `Range`), proceed to `LoadCatalog`.
   - `Loaded (Valid facts)` → run the **F022 selection** verbatim: `Routing.route` → `Gates.buildRegistry` →
     `Findings.findUnknownGovernedPaths` → `Route.select`; set `SelectedGates =
     result.SelectedGates |> List.map (fun sg -> sg.Gate)` (Phase `Selected`); emit `SenseFreshness
     (SelectedGates, (Range |> Option.map base, Range |> Option.map head))` and `LoadStore Request.StorePath`.
   - `FreshnessSensed (Ok sensed)` → store `sensed` (Phase `Resolved'` once both sensed+store present).
   - `StoreLoaded (Ok store)` → store it. When **both** `Sensed` and `Store` are present:
     - `report = FreshnessResolution.resolve SelectedGates sensed` (Phase `Resolved'`).
     - `candidates = entries report |> List.choose candidate` (only `Resolved` → `CandidateGate`; FR-005).
     - `cacheReport = CacheEligibility.evaluate candidates store` (Phase `Evaluated`).
     - `CacheDoc = CacheEligibilityJson.ofReport cacheReport` (F042 verbatim).
     - `UnresolvedDoc =` deterministic render of `entries report |> List.filter (outcome = Unresolved)` using
       `gateIdValue` + `missingFactToken` (the sidecar, D7) — **computed before either write** (Phase
       `Projected`).
     - emit `WriteArtifact (CacheArtifact, CacheOut, CacheDoc)` then `WriteArtifact (UnresolvedArtifact,
       UnresolvedOut, UnresolvedDoc)`.
   - `Wrote (_, Ok ())` → first ack → `Persisted`; second ack → emit `EmitSummary (render model Format)`.
   - any `Sensed`/`Loaded`/`FreshnessSensed`/`StoreLoaded`/`Wrote` failure → short-circuit to `Done` with the
     mapped `ExitDecision`, **no further effects** (no partial artifact); `StoreLoaded` for an *absent* file is
     `Ok empty`, never a failure.
4. **`render : Model -> OutputFormat -> string`** — deterministic human (reusable / must-recompute /
   recompute-by-default-unresolved gate lists with causes + named missing facts) or JSON summary; no clock,
   no cwd, no absolute paths.
5. **`exitCode : ExitDecision -> int`** — `Success=0`, `UsageError'=2`, `InputUnavailable=3`, `ToolError=4`.

## Edge `Interpreter` ports (NEW)

```fsharp
type FreshnessSensor =                                   // NEW — the only genuinely new sensing
    { SenseRuleHash: unit -> RuleHash option              // hash the rule pack (BCL crypto), None if unsensable
      SenseGeneratorVersion: unit -> GeneratorVersion option
      SenseCoveredArtifacts: Gate -> ArtifactHash list option   // Some [] = sensed-empty; None = unsensed (D4)
      SenseCommandVersion: CommandId -> CommandVersion option }  // None = unsensed ⇒ unresolved (no-hide)

type StoreReader = string -> Result<ReuseStore option, string>   // Ok None = absent file ⇒ empty; Error = malformed present

type Ports =
    { Files: Loader.FileReader                            // reused (F014)
      Git: Snapshot.Ports                                 // reused (F016) — scope + base/head
      Freshness: FreshnessSensor                          // NEW
      Store: StoreReader                                  // NEW (read-only; D6)
      Write: string -> string -> Result<unit, string>     // atomic temp+rename (RouteCommand pattern)
      Out: string -> unit }

val realPorts: repo: string -> Ports
val run: ports: Ports -> RunRequest -> Model               // drive init→update* to Done, guarded/total
```

The interpreter assembles `SensedFacts` from the `RepoSnapshot.Range` (Base/Head) + the `FreshnessSensor` over
each selected gate / declared command, then reifies it as `FreshnessSensed`. Every `step` is guarded (exceptions
→ `Error`/`Msg`); `run` never throws (the `RouteCommand` `Interpreter` precedent).

## Validation & invariants (the laws the tests assert)

- **L1 (selection reuse, FR-001)**: `SelectedGates` equals `RouteResult.SelectedGates |> map (.Gate)` for the
  same repo/scope a `fsgg route` run would select — no gate added, dropped, merged, or reordered; duplicates
  preserved (Edge).
- **L2 (base/head from Range, D4)**: `SensedFacts.Base`/`Head` come from `RepoSnapshot.Range`; `Range = None` ⇒
  both `None` ⇒ every gate unresolved on base/head (no separate git call, no fabrication).
- **L3 (no fabrication / no-hide, FR-003/FR-005, US2)**: a fact the sensor cannot sense is `None`/absent-key; the
  gate is `Unresolved` naming exactly and only the missing facts in the sidecar; never reusable, never dropped.
- **L4 (sensed-empty ≠ unsensed, FR-003, SC-005)**: `SenseCoveredArtifacts g = Some []` resolves; `= None` is
  unresolved on covered artifacts; the two are never conflated.
- **L5 (consistent command absence, SC-005)**: a gate with `FreshnessKey.Command = None` resolves with absent
  command + absent command version; never reported unresolved on that basis.
- **L6 (F042 verbatim, FR-007/SC-008)**: `CacheDoc = CacheEligibilityJson.ofReport cacheReport` byte-for-byte;
  the F042 schema/core/baseline is unmodified; the sidecar is the **only** place unresolved gates appear on disk.
- **L7 (recompute-by-default, US1.2/FR-006)**: absent store ⇒ `empty` ⇒ every resolvable gate `MustRecompute
  NoPriorEvidence`; still exit 0.
- **L8 (information ⇒ exit 0, FR-009, SC-006)**: all-must-recompute or some-unresolved runs exit 0; non-zero only
  on genuine sensing/catalog/store-malformed/write failure, with **no partial artifact** (atomic write, FR-010).
- **L9 (determinism / byte-stability, FR-008, SC-004)**: identical repo state + store ⇒ byte-identical
  `cache-eligibility.json` **and** `cache-eligibility.unresolved.json` and summary across cwd/process/clock/input
  order; per-gate entries in `GateId` order; no surfaced wall-clock (no F034 reference).
- **L10 (no verdict, FR-009/FR-011)**: the command assigns no severity/profile/mode/enforcement/ship/exit-from-
  blockers/provenance, and writes nothing into `route.json`/`audit.json`; F020/F025 cores and baselines untouched.
- **L11 (totality, Principle VI)**: `update` and `Interpreter.run` never throw for any well-typed input or any
  guarded port failure; every failure is a named diagnostic + mapped exit code.
- **L12 (store read format, FR-006/FR-013, A5)**: the `StoreReader` deserializes `fsgg.evidence-reuse-store/v1`
  into F030's `ReuseStore` via the public F029/F030 constructors only — computing no hash/key/digest; an absent
  file ⇒ `Ok None` ⇒ `empty`; a present-but-malformed file ⇒ `Error` ⇒ `ToolError`, no artifact written.
- **L13 (reusable real evidence, SC-002, US1.1)**: the reusable verdict has a **real**-tier proof — the
  end-to-end run loads a real on-disk `fsgg.evidence-reuse-store/v1` whose newest matching entry makes ≥1
  selected gate `reusable` (so `evaluate`'s reuse branch is exercised over real `realPorts`, not only faked
  `StoreReader`s — Principle V; the faked-port tiers carry the `Synthetic` disclosure).
