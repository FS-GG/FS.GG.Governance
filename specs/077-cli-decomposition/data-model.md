# Data Model: CLI render / IO decomposition (Phase E)

This is a **structural decomposition**, so the "entities" are the four *concerns*
being separated and the *public surfaces* of the new modules — no runtime data type is
added, removed, or changed. Every existing type (`RunRequest`, `CommandResult`,
`ProjectSnapshot`, `RecordedReview`, `Failure`, …) keeps its current shape and home in
`Cli.fs` / `Kernel` / `Host`.

## Concern entities

| Concern | Purity | New home | Input → Output | Validation / invariants |
|---------|--------|----------|----------------|--------------------------|
| **Rendering** | Pure | `CliRender` | `CommandResult` → `string` (text or JSON) | No FS/process access; byte-identical to prior `module Cli` output incl. the `fsgg-governance.cli.v1` envelope (FR-002). |
| **Artifact-reading** | Impure (edge) | `ArtifactReading` | repo `root` / `RunRequest` → supplied facts + `ProjectChange` + artifact list (`ProjectSnapshot`) | Same path resolution, sorted `### <rel>` directory concat, and `missing <path>`/`InputUnavailable` degrade paths (FR-003, edge cases). |
| **Review-store** | Impure (edge) | `ReviewStore` | `RunRequest` × `ProjectSnapshot` × key/review → `Result<RecordedReview option,…>` / `Result<unit,…>` | Same store-root resolution, key sanitization, verdict (de)serialization, and `review-store-unavailable` fixture short-circuit (FR-004). |
| **Thin orchestration** | Impure (edge) | `Program` (`runHost`/`main`) | ports + MVU + effect interpretation | No inline file-I/O bodies; delegates reads/persistence to the two edge modules (FR-005). |

## Module public surfaces (curated `.fsi`)

See `contracts/` for the full sketches. Summary of cross-module entry points:

- **`CliRender`** — `renderParseError`, `renderText`, `renderJson`, `render`
  (relocated from `Cli.fsi`). All sub-writers hidden.
- **`ArtifactReading`** — `optionsFor`, `readArtifact`, `loadSnapshot`. All path/parse
  helpers hidden.
- **`ReviewStore`** — `loadReview`, `saveReview`. All store helpers hidden.

## Surface deltas (Tier 1, additive — FR-011)

`surface/FS.GG.Governance.Cli.surface.txt` and the in-test `generatedSurface` literal
are **name-level** (module/type names only, no member vals). The decomposition:

- **Adds** (3 lines, after `module Cli`): `module CliRender`, `module ArtifactReading`,
  `module ReviewStore`.
- **Removes / renames**: none at name granularity. The four render `val`s move from
  `module Cli` to `module CliRender`, but `module Cli` still exists, so no baseline line
  changes (research D3). `Cli.fsi` loses the four render `val` declarations; this is a
  member-level relocation the name-level baseline does not track.

## Call-graph after the split

```
main ──┬─ Cli.parse ─────────────► (watch/tui edge: composeRouteView → HumanRender, unchanged)
       └─ Cli.run ports ─► Cli.update (MVU, unchanged)
                              │  ports:
                              │   LoadSnapshot = ArtifactReading.loadSnapshot
                              │   RunHost      = runHost
                              │   WriteOutput  = writeOutput
runHost (Program) ─► interpret Host effects:
   ReadArtifact   → ArtifactReading.readArtifact
   LoadReview     → ReviewStore.loadReview      (+ budget folds, in Program)
   DispatchReview → budget folds only           (review-dispatch-failed fixture stays here)
   RecordVerdict  → ReviewStore.saveReview
   (options)      → ArtifactReading.optionsFor
writeOutput (Program) ─► CliRender.render ─► File/Console
```

Dependency direction is strictly downward within the project
(`Cli → CliRender → ArtifactReading → ReviewStore → Program`); the graph stays acyclic
and no lower-layer project references the CLI (FR-010, existing scope-guard test).
