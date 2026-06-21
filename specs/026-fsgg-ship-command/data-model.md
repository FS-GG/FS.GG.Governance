# Phase 1 Data Model: `fsgg ship` Host Command

The command introduces **no new domain entity** — verdict, exit-code basis, the three-way partition, and
each item's six-field enforcement detail are the F024 `ShipDecision` / F023 `EnforcementDecision`
vocabulary, consumed verbatim. What this row defines is the **host-edge MVU state** (the `Loop` types) and
the **edge ports** (the `Interpreter` types). Both mirror F022 `RouteCommand`; the deltas are called out.
Field-level `.fsi` contracts live in [contracts/Loop.fsi](./contracts/Loop.fsi) and
[contracts/Interpreter.fsi](./contracts/Interpreter.fsi); this document is the prose model.

---

## 1. Reused upstream values (not redefined here)

| Value | Owner | Role in `ship` |
|---|---|---|
| `GovernedPath` | F014 `Config.Model` | candidate changed paths (scope) |
| `Validation` (`Valid facts` / `Invalid diags`) | F014 `Config` | catalog load+validate result |
| `RepoSnapshot` / `ChangedPath` | F016 `Snapshot.Model` | sensed scope |
| `RouteResult` | F019 `Route.Model` | the routed selection fed to the rollup |
| `RunMode` / `Profile` / `Recognized<'T>` | F023 `Enforcement` | the chosen levers + total string recognition |
| `ShipDecision` / `Verdict` / `ExitCodeBasis` / `EnforcedItem` | F024 `Ship.Model` | the whole-change verdict |
| `AuditJson.ofShipDecision` / `schemaVersion` | F025 `AuditJson` | the persisted document bytes |

`ship` re-derives, re-sorts, re-classifies, and re-serializes **none** of these.

---

## 2. `RunRequest` — the normalized invocation (spec "Ship run request")

The pure result of `parse`. Mirrors F022 with the **two new levers** and a **single output override**.

| Field | Type | Notes |
|---|---|---|
| `Repo` | `string` | repository root; default `"."` (D9) |
| `Scope` | `ScopeSelector` | `ExplicitPaths` / `Since rev` / `DefaultRange` (D4, F022 reuse) |
| `Mode` | `RunMode` (F023) | the run mode; default `Gate` when `--mode` omitted (D5) |
| `Profile` | `Profile` (F023) | the profile; default `Standard` when `--profile` omitted (D5) |
| `Format` | `OutputFormat` | `Text` (default) / `Json` (D8) |
| `AuditOut` | `string` | default `<repo>/readiness/audit.json`; override `--audit-out` (D7) |

`ScopeSelector` and `OutputFormat` are unchanged from F022. **Delta vs F022 `RunRequest`**: `Mode`/`Profile`
added; the two output paths (`GatesOut`/`RouteOut`) collapse to one `AuditOut`.

### `UsageError` (pure-parser rejections → exit 2, no artifact)

| Case | Cause |
|---|---|
| `UnknownFlag of string` | unrecognized flag token |
| `MissingValue of flag: string` | a value-taking flag with no value |
| `PathsAndSinceTogether` | both `--paths` and `--since` (D4) |
| `EmptyPaths` | `--paths` with no following path |
| `UnrecognizedMode of string` | `--mode` value not recognized by `Enforcement.recognizeMode` (D5) **— new** |
| `UnrecognizedProfile of string` | `--profile` value not recognized by `Enforcement.recognizeProfile` (D5) **— new** |

Lever recognition happens **inside `parse`** (D5): the matcher captures the raw `--mode`/`--profile`
strings, then `parse` calls the F023 recognizers and maps `Unrecognized s` to the matching `UsageError`.
This keeps an unrecognized lever a usage error decided *before any port is built* (no artifact, SC-004).

---

## 3. MVU state — `Model`, `Phase`, `Msg`, `Effect`, `ExitDecision`

### `ExitDecision` — the process-level outcome category (D6) **— the row's new lever**

```
Success           // 0 — Pass / Clean, audit written
Blocked           // 1 — Fail / Blocked, audit written  ← NEW vs F022 (route had no blocking code)
UsageError'       // 2 — bad invocation (incl. unrecognized lever)
InputUnavailable  // 3 — not a repo / unresolved rev / missing-or-invalid catalog
ToolError         // 4 — unwritable output, or any reified unexpected failure
```

`exitCode` maps these to `0 / 1 / 2 / 3 / 4`. `Blocked = 1` is reserved solely for a blocked merge verdict
and is distinct from every tool-failure code `2/3/4` (FR-009, SC-004).

### `ArtifactKind`

Only `AuditArtifact` (one document). **Delta vs F022**: F022 had `GatesArtifact | RouteArtifact`.

### `Effect` — I/O requested by `update`, executed by the interpreter

| Case | Meaning |
|---|---|
| `SenseScope of ScopeSelector` | resolve candidate paths via git (skipped for `ExplicitPaths`) |
| `LoadCatalog of repo: string` | read+validate the `.fsgg` catalog |
| `WriteArtifact of kind: ArtifactKind * path: string * content: string` | persist `audit.json` |
| `EmitSummary of text: string` | write the rendered summary to stdout |

### `Msg` — interpreter results fed back into `update`

`Begin` | `Sensed of Result<RepoSnapshot,string>` | `Loaded of Validation` | `Wrote of ArtifactKind *
Result<unit,string>` | `Emitted`. Unchanged in shape from F022 (one `Wrote` instead of two acks).

### `Phase` — pipeline progress

`Parsed → Sensed' → Loaded' → Rolled → Persisted → Done`. **Delta vs F022**: F022's
`Selected/Projected` collapse here into a single `Rolled` (select → rollup → project are all pure and
happen in one `Loaded(Valid)` step, ending with the audit document computed).

### `Model` — the durable workflow state

| Field | Type | Notes |
|---|---|---|
| `Request` | `RunRequest` | the normalized invocation |
| `Phase` | `Phase` | progress |
| `Candidates` | `GovernedPath list option` | resolved scope (set by `init` for `ExplicitPaths`, else by `Sensed`) |
| `Decision` | `ShipDecision option` | the F024 rollup — carried so `render` and the terminal exit mapping can read it |
| `AuditDoc` | `string option` | the F025 projection string, computed before the write (D10) |
| `Diagnostics` | `Diagnostic list` | actionable, no clock/abs-path/env (FR-006) |
| `Exit` | `ExitDecision` | the decided outcome |

**Delta vs F022 `Model`**: F022's `Result: RouteResult option` + `GatesDoc`/`RouteDoc` become
`Decision: ShipDecision option` + a single `AuditDoc`. The intermediate `RouteResult` is a local in
`update`, not a model field (it is only needed to feed `Ship.rollup`).

### `Diagnostic`

`{ Category: ExitDecision; Message: string }` — unchanged from F022.

---

## 4. The pure composition (`update` on `Loaded(Valid facts)`)

The single new-vs-F022 step is the **rollup + project** appended after `Route.select`:

```
candidates = model.Candidates |> Option.defaultValue []
report     = Routing.route facts candidates
registry   = Gates.buildRegistry facts
findings   = Findings.findUnknownGovernedPaths facts report
result     = Route.select registry report findings
decision   = Ship.rollup result model.Request.Mode model.Request.Profile   // ← F024, new
auditDoc   = AuditJson.ofShipDecision decision                              // ← F025, new
-> Phase = Rolled, Decision = Some decision, AuditDoc = Some auditDoc,
   [ WriteArtifact(AuditArtifact, model.Request.AuditOut, auditDoc) ]
```

On the single `Wrote(_, Ok())`: `Phase = Persisted`, emit `EmitSummary(render model model.Request.Format)`.
On `Emitted`: `Phase = Done`, and **`Exit` is mapped from the decision's `ExitCodeBasis`** —
`Clean → Success`, `Blocked → Blocked`. (Everything up to and including the write succeeds the same way
for a pass or a fail; only the terminal exit category differs — the verdict is information until the very
end.) A `Wrote(_, Error r)` → `fail ToolError` (exit 4, never `Blocked`). Sensing/catalog failures
short-circuit to `Done` with the mapped tool-failure category before any write (D10).

---

## 5. Edge ports (`Interpreter.Ports`) — identical to F022 (D3)

```
{ Files: Config.Loader.FileReader     // catalog reads (reused)
  Git:   Snapshot.Ports               // git sensing (reused)
  Write: ArtifactWriter               // path -> content -> Result<unit,string> (reused; temp+rename)
  Out:   OutputSink }                 // string -> unit (reused; Console.Out)
```

`realPorts repo` builds the real four. `step ports effect` executes one effect, catching every port
`Error` and thrown exception and reifying it to the matching `Msg` (never throws). `run ports request`
threads `init`→`step`→`update` to `Done` and returns the terminal `Model`.

---

## 6. State / transition summary

```
parse argv ──Ok──▶ RunRequest ──init──▶ (ExplicitPaths ⇒ LoadCatalog | else ⇒ SenseScope)
   │ Error UsageError (exit 2, no ports built, no artifact)
SenseScope ──Sensed(Ok snap)──▶ Candidates set ──▶ LoadCatalog
           ──Sensed(Error)──▶ Done / InputUnavailable (3)
LoadCatalog ──Loaded(Invalid)──▶ Done / InputUnavailable (3)
            ──Loaded(Valid facts)──▶ route→select→rollup→project ──▶ WriteArtifact(audit)
WriteArtifact ──Wrote(Error)──▶ Done / ToolError (4)
              ──Wrote(Ok)──▶ EmitSummary
EmitSummary ──Emitted──▶ Done / (Clean⇒Success 0 | Blocked⇒Blocked 1)
```
