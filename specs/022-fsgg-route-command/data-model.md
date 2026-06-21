# Phase 1 Data Model: `fsgg route` Host Command

This feature introduces **no new domain value** — the routed change, the gate registry, the findings,
the selected gates, and the two JSON documents are all the existing F014–F021 types, reused verbatim.
What it adds is the **host-edge state machine** that drives those cores: a pure `Model`/`Msg`/`Effect`
trio (the MVU boundary, Principle IV), the injected `Ports` the edge interpreter executes, the parsed
invocation, and the exit decision. This document fixes those types and the observable contracts
(artifact locations, exit codes). The JSON wire shapes are owned by F020/F021 and re-stated only by
reference; see [contracts/fsgg-route-command.md](./contracts/fsgg-route-command.md).

---

## 1. Reused upstream values (read-only, not redefined here)

| Value | Source | Role in this command |
|---|---|---|
| `TypedFacts` | `Config.Model` (F014) | Validated catalog; basis for routing, registry, findings. |
| `Validation = Valid \| Invalid of Diagnostic list` | `Config.Model` (F014) | Catalog load outcome; `Invalid` → InputUnavailable. |
| `RepoSnapshot` (`.Changed`, `.Range`, `.Diagnostics`) | `Snapshot.Model` (F016) | Sensed scope; `.Changed[*].Path` is the candidate set. |
| `RouteReport` | `Routing.Model` (F015) | `route facts candidatePaths`. |
| `FindingReport` | `Findings.Model` (F017) | `findUnknownGovernedPaths facts report`. |
| `GateRegistry` | `Gates.Model` (F018) | `buildRegistry facts`; input to `GatesJson`. |
| `RouteResult` | `Route.Model` (F019) | `select registry report findings`; input to `RouteJson`. |
| `GatesJson.ofGateRegistry`, `GatesJson.schemaVersion` | `GatesJson` (F021) | Whole-catalog document string. |
| `RouteJson.ofRouteResult`, `RouteJson.schemaVersion` | `RouteJson` (F020) | Per-change document string. |

None of these are re-sorted, re-derived, or re-serialized — the command *carries* them through the
composition unchanged (FR-004, FR-005).

---

## 2. New types — the parsed invocation

```
ScopeSelector =
    | ExplicitPaths of GovernedPath list   // --paths p1 p2 …  (bypasses git diff, D4)
    | Since of rev: string                 // --since <rev>    (Snapshot.Since)
    | DefaultRange                         // neither          (Snapshot default base/head)

OutputFormat = Text | Json

RunRequest =
    { Repo: string                         // --repo, default "."
      Scope: ScopeSelector
      Format: OutputFormat                 // --json ⇒ Json, else Text
      GatesOut: string                     // default <repo>/.fsgg/gates.json   (D5)
      RouteOut: string }                   // default <repo>/readiness/route.json (D5)

UsageError =                               // pure-parser rejections (exit 2, D6/D8)
    | UnknownFlag of string
    | MissingValue of flag: string
    | PathsAndSinceTogether
    | EmptyPaths
```

`parse : string list -> Result<RunRequest, UsageError>` is pure and total (D8).

---

## 3. New types — the MVU boundary (`Loop`, pure)

### Model

```
Phase =                                    // where the pipeline has reached
    | Parsed | Sensed' | Loaded' | Selected | Projected | Persisted | Done
    //         ^ primed to avoid collision with the like-named `Msg` cases (Sensed/Loaded)

Diagnostic =                               // host-edge diagnostic (distinct from F014 Diagnostic)
    { Category: ExitDecision               // the failure class this diagnostic implies
      Message: string }                    // actionable, no clock/abs-path/env value

Model =
    { Request: RunRequest
      Phase: Phase
      Candidates: GovernedPath list option // resolved scope (after Sensed')
      Result: RouteResult option           // after Selected (cores run transiently from the carried facts)
      GatesDoc: string option              // after Projected (GatesJson.ofGateRegistry)
      RouteDoc: string option              // after Projected (RouteJson.ofRouteResult)
      Diagnostics: Diagnostic list         // accumulated; non-empty ⇒ non-Success exit
      Exit: ExitDecision }                 // the running/decided exit category
    // No `Facts` field: the validated `TypedFacts` flow straight through the single
    // `Loaded (Valid facts)` transition (route → registry → findings → select → project)
    // and are not retained in the durable Model.
```

### Msg — external results fed back by the interpreter

```
Msg =
    | Begin                                         // kick off (init emits the first Effect)
    | Sensed of Result<RepoSnapshot, string>        // git sensing outcome (Error ⇒ InputUnavailable)
    | Loaded of Validation                          // catalog load+validate outcome
    | Wrote of which: ArtifactKind * Result<unit,string>  // one persisted-file outcome
    | Emitted                                        // summary written to the sink
```

### Effect — I/O the update requests but never performs

```
ArtifactKind = GatesArtifact | RouteArtifact

Effect =
    | SenseScope of ScopeSelector            // interpreter → Snapshot.senseSnapshot (or identity for ExplicitPaths)
    | LoadCatalog of repo: string            // interpreter → Config.Loader (FileReader)
    | WriteArtifact of ArtifactKind * path: string * content: string  // interpreter → ArtifactWriter
    | EmitSummary of text: string            // interpreter → OutputSink
```

### Functions (all pure)

```
init   : RunRequest -> Model * Effect list          // Phase=Parsed; emits SenseScope (or LoadCatalog for ExplicitPaths)
update : Msg -> Model -> Model * Effect list         // the whole composition; TOTAL, never throws
render : Model -> OutputFormat -> string             // the deterministic summary (D7)
exitCode : ExitDecision -> int                       // 0/2/3/4 (D6)
```

**`update` is the composition** (FR-004): on `Sensed Ok` it sets `Candidates` and emits `LoadCatalog`;
on `Loaded (Valid facts)` it runs the pure cores in-process —
`Routing.route` → `Gates.buildRegistry` → `Findings.findUnknownGovernedPaths` → `Route.select` — fills
`Result`/`GatesDoc`/`RouteDoc` via the F020/F021 projections (the `facts` are consumed in this one
transition and not retained), and emits the two `WriteArtifact` effects (both strings computed *before*
either write, D9). On both `Wrote Ok` it emits `EmitSummary (render …)`; on `Emitted` it reaches `Done`
with `Exit = Success`. Any `Error`/`Invalid` sets the matching `Diagnostic` + `Exit` and emits no
further work (FR-010, FR-013).

---

## 4. New types — the edge (`Interpreter`)

```
ArtifactWriter = path: string -> content: string -> Result<unit, string>   // NEW write port (D3/D9)
OutputSink     = string -> unit                                            // NEW stdout port (D7)

Ports =
    { Files: Config.Loader.FileReader        // REUSED — catalog reads (F014)
      Git:   Snapshot.Interpreter.Ports      // REUSED — git sensing (F016)
      Write: ArtifactWriter                  // NEW
      Out:   OutputSink }                    // NEW

realPorts : repo: string -> Ports            // binds Config.Loader.fileSystemReader, Snapshot.realPorts,
                                             //   atomic temp+rename writer, Console.Out
step : Ports -> Effect -> Msg                // execute ONE effect → its result Msg (never throws)
run  : Ports -> RunRequest -> Model          // init → drive update to Done; returns terminal Model
```

`run` is the interpreter loop: it threads `init`'s effects through `step`, feeds each result `Msg` back
into `update`, and stops at `Done`. It NEVER throws — `step` catches every port `Error` and exception and
reifies it to the matching `Msg` (the `Snapshot.senseSnapshot` / `Host.Interpreter` discipline).

---

## 5. ExitDecision — the process-level outcome (D6)

```
ExitDecision = Success | UsageError' | InputUnavailable | ToolError
//                       ^ primed to avoid collision with the `UsageError` parser-rejection DU (§2)
```

| Decision | Code | Triggers |
|---|---|---|
| `Success` | 0 | repo sensed, catalog valid, gates selected, both artifacts written. |
| `UsageError'` | 2 | `parse` rejected the argv (unknown flag, missing value, `--paths`+`--since`, empty `--paths`). |
| `InputUnavailable` | 3 | not a git repo / git unavailable; `--since` rev unresolved; catalog missing or `Invalid`. |
| `ToolError` | 4 | output unwritable after a valid route; any unexpected reified failure. |

Deliberately **no** `GovernedBlocking` (FR-008): selecting many gates / many findings is `Success`
(FR-009, FR-011).

---

## 6. Observable artifact contracts

| Artifact | Default path | Content | Owner |
|---|---|---|---|
| `gates.json` | `<repo>/.fsgg/gates.json` (override `--gates-out`) | `GatesJson.ofGateRegistry registry` verbatim | F021 |
| `route.json` | `<repo>/readiness/route.json` (override `--route-out`) | `RouteJson.ofRouteResult result` verbatim | F020 |
| summary | stdout | `render model format` (text default, JSON on `--json`) | this feature (D7) |

The two files are **byte-for-byte** the F020/F021 projections of the same typed inputs (FR-005, FR-006,
SC-001) — this feature adds no field and mutates no byte. Determinism and the exclusion sweep
(no verdict/severity/profile/mode/enforcement/cache/blockers/timestamp/abs-path/env) are inherited from
those projections (SC-005); this feature's only obligation is to *not inject* such a value, which it
cannot, because it serializes nothing of its own.

---

## 7. Determinism & totality properties

- **Pure `update`/`parse`/`render`**: no I/O, no git, no clock, no environment; identical `Msg`/`Model`
  in ⇒ identical `Model`/`Effect`s out (FR-013).
- **Total interpreter**: `step`/`run` never throw; every failure is a `Msg`/`Diagnostic`/`ExitDecision`
  (FR-010, FR-013, SC-004).
- **Byte-stable artifacts**: inherited from F020/F021; twice-run over fixed inputs ⇒ identical files and
  identical `--json` stdout (FR-006, SC-002).
- **No partial write**: both document strings computed before any write; each write is temp+atomic-rename;
  input/usage failures write nothing (FR-010, SC-004, D9).
- **Information, not failure**: any selected-gate count and any finding count ⇒ `Success` (FR-009,
  FR-011, SC-006), including the empty-change and empty-catalog edge cases.

## 8. State transitions (the happy path)

```
init(req) ─SenseScope/LoadCatalog→ [Parsed]
  ─Sensed Ok→        [Sensed']    (Candidates set)            ─LoadCatalog→
  ─Loaded Valid→     [Loaded']    (cores run from carried facts) ─(in-process route/registry/findings/select)→
                     [Selected]   (Result set)                ─(project)→
                     [Projected]  (GatesDoc, RouteDoc set)    ─WriteArtifact×2→
  ─Wrote Ok ×2→      [Persisted]                              ─EmitSummary→
  ─Emitted→          [Done]       (Exit = Success)
```

Any `Sensed Error` / `Loaded Invalid` / `Wrote Error` short-circuits to `Done` with the mapped
`ExitDecision` and no further effects. (The `Msg` cases `Sensed`/`Loaded` are unprimed; the `Phase`
markers `Sensed'`/`Loaded'` are primed to avoid the name collision — see §3.)
