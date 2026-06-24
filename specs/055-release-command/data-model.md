# Phase 1 Data Model: The `fsgg release` Host Command

This row defines **no new domain facts**. It composes existing typed values (F053 `ReleaseRules`, F054
`ReleaseFactsSensing`, F014 `Config`) and adds only the host-local request/state/declaration/projection
types below. Existing types are referenced, not redefined.

## Reused types (referenced verbatim — not redefined here)

| Type | Source | Role in this command |
|------|--------|----------------------|
| `ReleaseRuleKind` (VersionBump, PackageMetadata, TemplatePins, PublishPlan, TrustedPublishing, Provenance) | F053 `ReleaseRules.Model` | The six families. |
| `ReleaseRule { Kind; Surface; BaseSeverity; Maturity }` | F053 `ReleaseRules.Model` | A declared rule — produced by the `Declaration` adapter. |
| `ReleaseFacts { States: Map<ReleaseRuleKind, FactState> }`, `FactState` (Met/Unmet/Unrecoverable) | F053 `ReleaseRules.Model` | The sensed per-family state. |
| `ReleaseFinding`, `EnforcedReleaseFinding`, `ReleaseDecision { Verdict; Blockers; Warnings; Passing; ExitCodeBasis }` | F053 `ReleaseRules.Model` | The evaluation output (one finding per rule + verdict). |
| `Verdict` (Pass/Fail), `ExitCodeBasis` (Clean/Blocked), `Severity`, `EnforcementDecision` | F024 `Ship`/F023 `Enforcement` (via F053) | Verdict & effective-severity vocabulary. |
| `ReleaseExpectations`, `SourceLayout`, `SensedRelease { Facts; Snapshot }`, `ReleaseSnapshot`, `VersionFact`/`MetadataFact`/`PinsFact`/`PostureFact`, `SensingDiagnostic` | F054 `ReleaseFactsSensing.Model` | Sensing inputs and observed-evidence snapshot. |
| `RepositoryPort`, `realPort`, `senseRelease` | F054 `ReleaseFactsSensing.Interpreter` | The injected sensing edge. |
| `Loader.FileReader`, `fileSystemReader`, `GovernedPath`, `SurfaceId`, `Maturity` | F014 `Config` | Read port + path/identity/maturity tokens for declaration parsing. |

`evaluateRelease : ReleaseRule list -> ReleaseFacts -> ReleaseDecision` (F053) and
`senseRelease : RepositoryPort -> ReleaseExpectations -> SensedRelease` (F054) are the two reused entry
functions this command wires together.

## New host-local types

### `ReleaseDeclaration` (in `ReleaseCommand.Declaration`)

The typed result of parsing `.fsgg/release.yml` — exactly the inputs the cores need, nothing more.

```fsharp
type ReleaseDeclaration =
    { Rules: ReleaseRule list          // F053 declared rules (one per declared family)
      Expectations: ReleaseExpectations // F054 per-family "met" criteria + Surface
      Layout: SourceLayout }           // F054 per-family relative source paths
```

- **Validation rules**: a present-but-malformed `release.yml` ⇒ `Error` (input-unavailable, never partial
  facts); an absent `release.yml` ⇒ `Error` (input-unavailable). A declared rule whose family has no
  expectation is **allowed** — sensing resolves that family to `Unrecoverable` (edge case in spec). Rule
  ordering is normalized to the F053 stable composite key; declaration parsing is deterministic.
- **Product-neutrality**: every value (surface id, version baseline, field names, pins, postures, source
  paths) comes from the file; the adapter hardcodes none (FR-014).

### `DeclError` (in `ReleaseCommand.Declaration`)

A closed, located, explained reason a `release.yml` was rejected (the F014 `Diagnostic` spirit, row-local):

```fsharp
type DeclError =
    { Reason: string }   // actionable, product-neutral; identifies the missing/invalid declaration
```

### `RunRequest` + `OutputFormat` + `UsageError` (in `ReleaseCommand.Loop`)

The parsed invocation (mirrors `ShipCommand.Loop.RunRequest`).

```fsharp
type OutputFormat =
    | Text
    | Json
    | TextAndJson

type RunRequest =
    { Repo: string          // governed repository working directory (--repo)
      Format: OutputFormat  // --format text|json|both  (default text)
      ReleaseOut: string }  // --out path for release.json (default repo-relative "release.json")

type UsageError = { Message: string }   // argv rejection → UsageError' (exit 2), no I/O, no artifact

val parse: argv: string list -> Result<RunRequest, UsageError>
```

### `ExitDecision` (in `ReleaseCommand.Loop`) — five distinguishable classes

```fsharp
type ExitDecision =
    | Success           // 0  release passed (ReleaseDecision.ExitCodeBasis = Clean)
    | Blocked           // 1  release blocked (ExitCodeBasis = Blocked) — distinct from all failures
    | UsageError'       // 2  bad argv
    | InputUnavailable  // 3  absent/invalid release.yml, or absent governing inputs the host can't proceed past
    | ToolError         // 4  genuine tool/IO defect (e.g. unwritable output path)

val exitCode: ExitDecision -> int   // 0/1/2/3/4
```

### MVU `Model` / `Msg` / `Effect` (in `ReleaseCommand.Loop`)

The pure boundary (Constitution IV), mirroring `ShipCommand.Loop`.

- **`Effect`** (requested by `update`, executed by the interpreter):
  - `LoadDeclaration of repo: string` — read `.fsgg/release.yml` via `Files` and parse to `ReleaseDeclaration`.
  - `SenseRelease of layout: SourceLayout * expectations: ReleaseExpectations` — build `realPort repo layout`, run `senseRelease`.
  - `WriteArtifact of path: string * content: string` — atomic write of `release.json`.
  - `EmitSummary of text: string` — stdout human summary.
- **`Msg`** (interpreter results fed back): `Begin`, `DeclarationLoaded of Result<ReleaseDeclaration, DeclError>`,
  `Sensed of SensedRelease`, `Wrote of Result<unit, string>`, `Emitted`.
- **`Model`**: carries the `RunRequest`, a `Phase`, the loaded `ReleaseDeclaration`, the `SensedRelease`,
  the computed `ReleaseDecision` (`Release.evaluateRelease` is called purely inside `update` once sensing
  returns), the resolved `ExitDecision`, and an ordered `Diagnostics` list. `update` is pure; evaluation
  is pure; only the interpreter performs I/O.

```fsharp
val init: RunRequest -> Model * Effect list
val update: Msg -> Model -> Model * Effect list
val render: Model -> OutputFormat -> string
```

### `Ports` (in `ReleaseCommand.Interpreter`) — the injected edge

```fsharp
type ArtifactWriter = string -> string -> Result<unit, string>   // atomic temp-then-rename
type OutputSink = string -> unit

type Ports =
    { Files: Loader.FileReader                              // F014 read port (bound to repo .fsgg)
      Sense: SourceLayout -> ReleaseExpectations -> SensedRelease  // wraps realPort+senseRelease
      Write: ArtifactWriter
      Out: OutputSink }

val realPorts: repo: string -> Ports
val step: Ports -> Loop.Effect -> Loop.Msg
val run: Ports -> Loop.RunRequest -> Loop.Model
```

### `ReleaseJson` projection surface (in `FS.GG.Governance.ReleaseJson`)

```fsharp
val schemaVersion: string                                  // fixed literal, e.g. "fsgg.release/v1"
val ofRelease: decision: ReleaseDecision -> sensed: SensedRelease -> string
```

Pure, total, deterministic, emit-only. Output document (fixed field order): `schemaVersion`, `verdict`,
`exitCodeBasis`, then `rules` (each: `kind`, `surface`, `factState`, `outcome`, `baseSeverity`,
`effectiveSeverity`, `reason`), then `evidence` (per-family snapshot: version observed/baseline; metadata
present/missing; pins resolved/expected/drifted; publishPlan/trustedPublishing/provenance
observed/required/missing; and ordinal-sorted `diagnostics`). See `contracts/release.schema.md`.

## State transitions (host workflow)

```
parse(argv) ── Error ──▶ UsageError' (exit 2, no I/O)
   │ Ok request
   ▼ init ⇒ Effect LoadDeclaration
DeclarationLoaded(Error)  ──▶ InputUnavailable (exit 3, actionable diagnostic, no artifact)
DeclarationLoaded(Ok decl) ⇒ Effect SenseRelease(decl.Layout, decl.Expectations)
   ▼
Sensed(sensedRelease) ⇒ (pure) decision = Release.evaluateRelease decl.Rules sensed.Facts
                      ⇒ ExitDecision from decision.ExitCodeBasis (Clean→Success | Blocked→Blocked)
                      ⇒ Effect EmitSummary (always) [+ WriteArtifact release.json if Format requests JSON]
   ▼
Wrote(Error) ──▶ ToolError (exit 4, no partial artifact left behind)
Wrote(Ok) / Emitted ⇒ Done ⇒ exitCode(ExitDecision)
```

Every successful run yields a complete six-family verdict (FR-013/SC-006) — including the
all-`Unrecoverable` case, which is `Blocked`, never a crash or a fabricated pass.
