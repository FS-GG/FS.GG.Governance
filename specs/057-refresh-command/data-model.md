# Phase 1 Data Model: The `fsgg refresh` Host Command

Entities are drawn from the spec's **Key Entities** and refined into the concrete F# shapes the
`RefreshCommand` (MVU + `Declaration`) and `RefreshJson` projects expose. All types are closed DUs /
records (Principle III). `[opaque]` types are reused verbatim from existing projects. Field-level
validation rules trace to the FR they enforce.

## Reused types (verbatim, not redefined)

| Type | Project | Role here |
|---|---|---|
| `FreshnessInputs` | F029 `FreshnessKey` | the per-view currency input (built with revisions held equal — research D1) |
| `Key` | F029 `FreshnessKey` | the computed currency fingerprint compared by `matches` |
| `InputCategory` | F029 `FreshnessKey` | the `diff` vocabulary naming what drifted (`CoveredArtifactsCat`/`GeneratorVersionCat`) |
| `ArtifactHash` | F-kernel | a source/output content digest (SHA-256) |
| `GeneratorVersion` | F-kernel | the generator version token |
| `Loader.FileReader` | F014 `Config` | injected manifest/source reader |
| `GateExecution` port / `GateRun` | F051 / F052 | the declared-generator execution port |

## New types — generation manifest (`Declaration` adapter, RefreshCommand)

### `ViewKind`
A closed DU of the declared view kinds, **product-neutral** — the kinds are structural, not product
identities (FR-011). Indicative set (final set is a tasks-phase detail): `GateMetadata | RuleCatalog |
CapabilityDoc | SkillReference | ApiSurfaceDoc | RouteProjection | Baseline | Other of string`.
`Other of string` keeps the surface open without naming products.

### `GenerationEntry`
The declared relationship between one generated view and its sources (spec "Generation manifest entry").
```
GenerationEntry =
  { ViewId        : string          // stable identity (selector target — FR-015)
    Kind          : ViewKind        // FR-011: structural kind, not product identity
    OutputPath    : string          // repo-relative path of the generated view
    Sources       : string list     // declared source path(s), in declared order (FR-002)
    Generator     : string list     // the declared generator command (argv) — run at the edge (D3)
    GeneratorBasis: string }         // how the generator version is sensed (token/command) — FR-002
```
**Validation**: non-empty `ViewId`, `OutputPath`, `Generator`; `Sources` may be empty only if the kind
declares no source (then the view is always "current" — degenerate). Duplicate `ViewId` ⇒ `DeclError`.

### `GenerationManifest`
```
GenerationManifest = { Entries : GenerationEntry list }   // FR-012: empty list is valid ("nothing to refresh")
```

### `DeclError`
`{ Reason : string }` — closed, explained rejection of a malformed `refresh.yml` (mirrors
`ReleaseCommand.Declaration.DeclError`). Parsing is **pure and total**: malformed ⇒ `Error DeclError`,
never an exception (FR-010, FR-016).

`Declaration.parse : string list -> Result<GenerationManifest, DeclError>`

## New types — currency & decision (shared, consumed by Loop + RefreshJson)

### `CurrencyStatus`
The per-view outcome (spec "Currency status"):
```
CurrencyStatus =
  | Current                                   // untouched; recorded matches current
  | Regenerated of drifted: InputCategory list // was stale, brought current this run; what drifted
  | WouldRegenerate of drifted: InputCategory list // --dry-run: stale, would be regenerated (FR-004)
  | StaleUnresolved of reason: string         // stale but could not be brought current (FR-010)
  | NotEvaluated                              // out of scope (FR-015)
```
**Invariant (FR-010)**: a view whose source cannot be resolved is `StaleUnresolved`, **never** `Current`.
`Regenerated`/`WouldRegenerate` are mutually exclusive by run mode (write vs `--dry-run`).

### `ViewDecision`
```
ViewDecision = { Entry : GenerationEntry; Status : CurrencyStatus; Drifted : InputCategory list }
```

### `RefreshOutcome`
The overall category that drives the exit code (spec "Exit decision"; research D5):
```
RefreshOutcome =
  | NothingToRefresh    // exit 0
  | ViewsRegenerated    // exit 5
  | StaleUnresolved'    // exit 1
  | UsageError'         // exit 2  (set at parse)
  | InputUnavailable    // exit 3
  | ToolError           // exit 4
```

### `RefreshDecision`
The whole-run value the projection renders and the summary reports (spec "Refresh summary"):
```
RefreshDecision =
  { Outcome       : RefreshOutcome
    Views         : ViewDecision list   // per-view, in declared order (deterministic)
    RegeneratedCount : int
    CurrentCount     : int
    UnresolvedCount  : int
    NotEvaluatedCount: int }
```
`RefreshJson.ofRefreshDecision : RefreshDecision -> string` projects this (research D7).

## MVU state (RefreshCommand.Loop)

### `RunRequest` (parsed argv)
```
RunRequest =
  { Repo        : string
    DryRun      : bool                  // FR-004
    Scope       : Scope                 // FR-015
    Format      : OutputFormat          // Text | Json | TextAndJson
    RefreshOut  : string option }       // optional refresh.json path (FR-006)
Scope = AllViews | ByKind of ViewKind | ByView of string   // mutually-exclusive selectors (FR-015)
```
`Loop.parse : string list -> Result<RunRequest, UsageError>` — a leading bare `refresh` token is tolerated
(command precedent); unknown flag / missing value / two selectors ⇒ `Error` (exit 2).

### `Model`
```
Model =
  { Request     : RunRequest
    Phase       : Phase                 // Parsed | Loaded | Sensed | Regenerated | Persisted | Done
    Manifest    : GenerationManifest option
    Views       : ViewDecision list
    Decision    : RefreshDecision option
    RefreshDoc  : string option
    Diagnostics : Diagnostic list
    Exit        : RefreshOutcome }
```

### `Msg`
```
Msg =
  | Begin
  | ManifestLoaded of Result<GenerationManifest, DeclError>
  | Sensed of ViewId: string * Result<currentDigests: ArtifactHash list * GeneratorVersion, senseError: string>
  | RecordedRead of ViewId: string * recorded: (ArtifactHash list * GeneratorVersion) option
  | Regenerated' of ViewId: string * Result<outputDigest: ArtifactHash, string>   // write mode only
  | Wrote of Result<unit, string>     // refresh.json / lock write
  | Emitted
```

### `Effect`
```
Effect =
  | LoadManifest of repo: string
  | SenseSource of entry: GenerationEntry          // per-view source digest + generator version (D2)
  | ReadRecorded of viewId: string                 // recorded provenance (D4)
  | RegenerateView of entry: GenerationEntry       // run declared generator (D3) — NOT emitted in --dry-run
  | RecordProvenance of viewId: string * current: (ArtifactHash list * GeneratorVersion * ArtifactHash)
  | WriteArtifact of path: string * content: string
  | EmitSummary of text: string
```
**Pure `update` invariant (FR-013)**: in `--dry-run`, `update` emits **no** `RegenerateView`,
`RecordProvenance`, or view-`WriteArtifact` effect — only the optional `refresh.json` `WriteArtifact`.

### Currency decision (pure, in `update`)
For each in-scope entry: build `recorded` and `current` `FreshnessInputs` (revisions equal — D1);
`stale = not (FreshnessKey.matches recorded current)`; `drifted = FreshnessKey.diff recorded current`.
- not stale ⇒ `Current`
- stale & `--dry-run` ⇒ `WouldRegenerate drifted`
- stale & write & regeneration succeeded ⇒ `Regenerated drifted`
- stale & (source unresolved | regeneration failed) ⇒ `StaleUnresolved reason` (FR-010)
- out of scope ⇒ `NotEvaluated` (FR-015)

### Exit mapping (`Loop.exitCode : RefreshOutcome -> int`)
`NothingToRefresh→0 · StaleUnresolved'→1 · UsageError'→2 · InputUnavailable→3 · ToolError→4 ·
ViewsRegenerated→5` (research D5). Roll-up: any `StaleUnresolved` ⇒ `StaleUnresolved'`; else any
`Regenerated`/`WouldRegenerate` ⇒ `ViewsRegenerated`; else `NothingToRefresh`. A load/sense failure short-
circuits to `InputUnavailable`; a generator/write failure to `ToolError`.

## Edge ports (RefreshCommand.Interpreter)

```
Ports =
  { Files     : Loader.FileReader                                   // F014: read refresh.yml + declared sources
    Sense     : GenerationEntry -> Result<ArtifactHash list * GeneratorVersion, string>  // D2 row-local helper
    ReadProv  : string -> (ArtifactHash list * GeneratorVersion) option                  // D4 recorded read
    Generate  : GenerationEntry -> Result<ArtifactHash, string>     // D3: run declared generator via F051/F052, return output digest
    WriteProv : string -> (ArtifactHash list * GeneratorVersion * ArtifactHash) -> Result<unit, string>  // D4 atomic
    Write     : string -> string -> Result<unit, string>            // atomic temp-then-rename (ReleaseCommand precedent)
    Out       : string -> unit }
realPorts : repo:string -> Ports ; step : Ports -> Loop.Effect -> Loop.Msg ; run : Ports -> Loop.RunRequest -> Loop.Model
```
All ports are fakeable; unit tests inject capturing/faulting fakes over real cores, the end-to-end test uses
`realPorts` against a temp repo with a real (deterministic) generator command.

## Determinism rules (FR-007/SC-004)
- `Views` always in declared manifest order; `refresh.json` keys/arrays in fixed order.
- No timestamp, absolute path, username, or machine-specific content in any persisted output.
- `--json` stdout is the verbatim bytes of the persisted `refresh.json` (one source of truth).
- The recorded-provenance lock is written deterministically (sorted, no clock).
