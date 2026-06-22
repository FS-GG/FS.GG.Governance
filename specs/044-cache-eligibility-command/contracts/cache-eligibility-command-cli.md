# Contract: `fsgg cache-eligibility` CLI + `Loop`/`Interpreter` Surface

**Feature**: `044-cache-eligibility-command` | **Date**: 2026-06-22

This contract fixes the **observable command interface** (verb, flags, exit codes, stdout/stderr discipline) and
the **public F# surface** of the new `FS.GG.Governance.CacheEligibilityCommand` project. It is the Tier-1
surface guarded by `surface/FS.GG.Governance.CacheEligibilityCommand.surface.txt`.

## C1 — Command verb & flags (D8)

```
fsgg cache-eligibility [--repo <dir>]
                       [--paths <p1> <p2> … | --since <rev>]      # ScopeSelector; default = DefaultRange
                       [--store <file>]                            # read-only reuse store; absent ⇒ empty
                       [--out <file>]                              # cache-eligibility.json path
                       [--format human|json]                      # summary; default human
```

- `--repo` defaults to the current directory (`"."`). The command senses git/filesystem **only** through the
  injected `Snapshot`/`Config`/`FreshnessSensor`/`StoreReader` ports — never directly.
- Scope flags reuse `RouteCommand`'s `ScopeSelector` semantics verbatim (FR-001): `--paths` ⇒ `ExplicitPaths`,
  `--since <rev>` ⇒ `Since`, neither ⇒ `DefaultRange`. `--paths` and `--since` together ⇒ `PathsAndSinceTogether`
  (usage error); an empty `--paths` list ⇒ `EmptyPaths`.
- **Default paths** (mirroring `RouteCommand`'s `readiness/`-dir precedent, D8): when `--out` is omitted,
  `CacheOut = <repo>/readiness/cache-eligibility.json`; when `--store` is omitted,
  `StorePath = <repo>/readiness/evidence-reuse.json`. For `--repo .` the clean relative form is used
  (`readiness/cache-eligibility.json`); any other repo is prefixed so the artifact lands under that repo.
  Defaults are applied in `parse` so `RunRequest`'s path fields are always populated.
- `--out` names `cache-eligibility.json`; the **sidecar** `cache-eligibility.unresolved.json` is written next to
  it (same directory, `…unresolved.json` stem) — `UnresolvedOut` is **derived** from `CacheOut`, never taken as a
  flag.
- `--format` accepts exactly `human` or `json` (default `human`); any other value ⇒ `BadFormat` (usage error).
- `--store` absent on disk ⇒ `EvidenceReuse.empty` (FR-006); a **present but malformed** store ⇒ `ToolError`,
  no artifact written. The read-only store format is fixed in
  [cache-eligibility-artifacts.md §A5](./cache-eligibility-artifacts.md).

## C2 — Exit codes (mirrors `RouteCommand`; information ⇒ 0)

| Code | `ExitDecision` | When |
|---|---|---|
| 0 | `Success` | repo sensed, valid catalog, both artifacts written — **including** all-must-recompute and/or some-unresolved (FR-009) |
| 2 | `UsageError'` | unparseable argv (unknown flag, missing value, bad format) |
| 3 | `InputUnavailable` | not a git repo / scope cannot be sensed / declared catalog absent (missing input, not a defect) |
| 4 | `ToolError` | invalid catalog, malformed present store, unwritable output, or any guarded sensing/IO exception |

A gate that "must recompute" or is "unresolved" is **never** a non-zero exit (it is information, FR-009/SC-006).
On any non-zero exit, **no partial artifact** is left on disk (atomic temp-write-then-rename, FR-010).

## C3 — Stdout / stderr discipline

- **stdout**: exactly the deterministic summary from `render model Format` (human or JSON) on success. No clock,
  no absolute paths, no cwd-dependent content (FR-008).
- **stderr**: structured diagnostics for the failure cases (Constitution VI), naming the missing/malformed input
  distinctly from a tool defect. Diagnostics never appear in the written artifacts.

## C4 — `Loop` public surface (pure)

```fsharp
module FS.GG.Governance.CacheEligibilityCommand.Loop

type ScopeSelector = ExplicitPaths of GovernedPath list | Since of rev: string | DefaultRange
type OutputFormat = Human | Json
type RunRequest = { Repo: string; Scope: ScopeSelector; StorePath: string; CacheOut: string; UnresolvedOut: string; Format: OutputFormat }

// NEW, local (mirrors RouteCommand.Loop.UsageError) — pure-parser rejections, each ⇒ UsageError'/exit 2.
type UsageError = UnknownFlag of string | MissingValue of flag: string | PathsAndSinceTogether | EmptyPaths | BadFormat of value: string

type Phase = Parsed | Sensed' | Loaded' | Selected | Resolved' | Evaluated | Projected | Persisted | Done
type ExitDecision = Success | UsageError' | InputUnavailable | ToolError
type ArtifactKind = CacheArtifact | UnresolvedArtifact

type Effect = …      // SenseScope | LoadCatalog | SenseFreshness | LoadStore | WriteArtifact | EmitSummary  (data-model.md)
type Msg = …         // Begin | Sensed | Loaded | FreshnessSensed | StoreLoaded | Wrote | Emitted
type Model = …       // data-model.md

val parse:    argv: string list -> Result<RunRequest, UsageError>
val init:     request: RunRequest -> Model * Effect list
val update:   msg: Msg -> model: Model -> Model * Effect list      // PURE, TOTAL — no I/O, no clock, never throws
val render:   model: Model -> format: OutputFormat -> string       // deterministic summary
val exitCode: decision: ExitDecision -> int
```

**Laws**: `update` is pure/total (no I/O, no clock, never throws); both artifact strings are computed in
`update` **before** any `WriteArtifact` is emitted; a failure `Msg` short-circuits to `Done` with no further
effects; `render`/`exitCode` are deterministic functions of `Model`/`ExitDecision`.

## C5 — `Interpreter` public surface (edge)

```fsharp
module FS.GG.Governance.CacheEligibilityCommand.Interpreter

type FreshnessSensor = { SenseRuleHash: unit -> RuleHash option
                         SenseGeneratorVersion: unit -> GeneratorVersion option
                         SenseCoveredArtifacts: Gate -> ArtifactHash list option
                         SenseCommandVersion: CommandId -> CommandVersion option }
type StoreReader = string -> Result<ReuseStore option, string>     // Ok None = absent ⇒ empty; Error = malformed present
type Ports = { Files: Loader.FileReader; Git: Snapshot.Ports; Freshness: FreshnessSensor
               Store: StoreReader; Write: string -> string -> Result<unit, string>; Out: string -> unit }

val realPorts: repo: string -> Ports
val run:       ports: Ports -> request: RunRequest -> Model         // drive init→update* to Done; guarded; never throws
```

**Laws**: every `step` is guarded (exception ⇒ `Error`/failure `Msg`); `run` never throws; `realPorts` wires
real `Config`/`Snapshot` ports, a real BCL-crypto `FreshnessSensor`, a real read-only `StoreReader`, the atomic
`Write`, and `Console.Out`. The interpreter assembles `SensedFacts` from `RepoSnapshot.Range` (base/head) + the
`FreshnessSensor` output — it **never fabricates** an unsensed fact (D4/L3).

## C6 — Scope guard (additive-only)

The assembly references only the F022 selection cores (`Config`/`Snapshot`/`Routing`/`Findings`/`Gates`/`Route`)
and the cache cores (`FreshnessResolution`/`CacheEligibility`/`CacheEligibilityJson`/`EvidenceReuse`) plus their
transitive cores. It references **no** `RouteJson`/`GatesJson`/`AuditJson`/`RouteCommand`, takes **no** new
third-party `PackageReference`, and modifies **no** existing `src/`/`surface/` file (SC-007/SC-008) — asserted by
`SurfaceDriftTests`.
