# Phase 1 Data Model: Command-host second extraction pass (#49)

This is a refactor, so the "data model" is the **surface delta**: what public signatures move, appear, or disappear, and which surface baselines must change. No new domain entity is introduced.

## Shared `CommandHost` additions (Phase A — new public surface, baseline updated)

New `val`s added to `src/FS.GG.Governance.CommandHost/CommandHost.fsi`, each replacing N local copies. Signatures are firmed in [contracts/shared-leaves.md](./contracts/shared-leaves.md).

| Symbol | Shape (informal) | Replaces |
|---|---|---|
| `writeAtomic` | `path:string -> content:string -> Result<unit,string>` | 7 identical local copies |
| `realHandoffs` | `readinessDir:string -> <handoff array>` (ordinal-sorted) | 3 host copies + the `Cli` mirror sort |
| `senseEnvironmentReal` | `unit -> EnvironmentClass` (or the existing host shape) | 3 copies (preserve Release's unqualified-`EnvironmentClass` clash-avoidance) |
| `senseBuilderReal` | `unit -> BuilderIdentity` | 3 copies |
| shared step-arm helper | a function `step` calls to handle the `SenseScope`/`LoadCatalog` effect arms | 4 in-body copies |
| `requireValue` (argv guard) | consumes one value token, **errors if it is `--`-prefixed** | the missing guard in 7 parsers + `Cli.requireValue` |

**Invariant preserved**: the public `step` signature in each host `.fsi` (`ports -> effect -> msg`) is **unchanged** — the shared arm-helper lives under `step`, not in its signature. Confirm via SurfaceDrift (baselines for the 9 `step` modules stay green).

## Surface removals / changes (Phase B & C — deliberate Tier-1 deltas)

| Change | Files whose `.fsi` + baseline change | Direction |
|---|---|---|
| Adopt canonical `CommandHost.ExitDecision` (D5) | Route/Ship/Verify/Release/Evidence/CacheEligibility `Loop.fsi` (+ `Cli.fs`) lose their local `ExitDecision`/`exitCode` | surface **shrinks** per host (or, fallback, the dead canonical is deleted from `CommandHost.fsi`) |
| Widen `ArtifactReading.fsi` (D6) | `src/FS.GG.Governance.Cli/ArtifactReading.fsi` gains the minimal `val`s Evidence needs | surface **grows** (Cli) |
| Delete Evidence's ArtifactReading copy (D6) | `EvidenceCommand/Interpreter.fs` shrinks ~325 lines; no `.fsi` change (the copy was internal) | internal only |

**Rule (constitution Tier-1)**: every `.fsi` change above updates its matching surface-drift baseline **in the same commit**. The API-compat gate is expected to report these as intended deltas.

## Behavior-fix touch points (no surface change)

| Fix | Location | Nature |
|---|---|---|
| M-CLI-3 (D2) | all `Loop.fs` option arms + `Cli.requireValue` | reject `--`-prefixed value → `MissingValue` |
| M-CLI-7 (D4) | `EvidenceCommand/Loop.fs` | `--plain` becomes a documented no-op; drop the dead `ExplicitPlain` plumbing if it drives nothing |
| F15 (D7) | `ShipCommand/Loop.fs` `Wrote(Ok)` arm | pass post-update model to `emitEffect` (match Verify) |
| F13 (D8) | `EvidenceCommand/Loop.fs` `update` | prepend `if model.Phase = Done then model, []` |

## Key entities (unchanged, referenced)

- **`ExitDecision`** — the exit classification DU (`Success | Blocked | UsageError' | InputUnavailable | ToolError`) → `exitCode: 0|1|2|3|4`. Canonical in `CommandHost`; currently shadowed per host.
- **Handoff set** — array of readiness handoff directories, ordinal-sorted by `Path.GetFileName`.
- **`RenderMode`** — `Json | Rich | Plain` from `HumanText/RenderMode.selectMode`; `explicitJson` wins first (invariant that makes M-CLI-7 a no-override).
- **`Phase`** (per host) — the MVU model's lifecycle; `Done` is the terminal state the F13 guard keys on.
