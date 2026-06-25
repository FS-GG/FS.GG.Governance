# Contract — Capability Sensing (host-edge effect)

**New public surface** in `FS.GG.Governance.HumanRender` (the sole Spectre owner). Keeps every host free of a direct
console/Spectre reference (FR-011, SC-007).

## Signature

```
// HumanRender.Capability
val senseCapability : explicitPlain:bool -> RenderMode.ColorCapability
```

- `explicitPlain` — the host-parsed `--plain` (or `--no-color`) flag.
- Returns `ColorCapability = { IsTty; NoColorEnv; ExplicitPlain; Width }`:
  - `IsTty` — stdout is an interactive terminal (`not Console.IsOutputRedirected`).
  - `NoColorEnv` — the `NO_COLOR` environment variable is present (any value).
  - `ExplicitPlain` — the `explicitPlain` argument.
  - `Width` — the terminal width if known (`Some n`), else `None` (consumed by `RichRender`, safe default 80).

## Behavior

- **Effect, not pure** — it reads process/console/environment state, so it is invoked **only** at the interpreter
  edge of a host (Constitution IV, research.md D4). The pure decision remains `HumanText.selectMode`, unchanged.
- Deterministic given identical environment/terminal state.

## Guarantees

- **C1**: `selectMode explicitJson (senseCapability explicitPlain)` selects `Json` iff `explicitJson`; else `Rich`
  iff `IsTty && not NoColorEnv && not ExplicitPlain`; else `Plain` (FR-004).
- **C2**: No host calls `Console`/`Spectre` width or TTY APIs directly — they call `senseCapability` (FR-011).
- **C3**: `Width = None` (unknown) never throws; `RichRender` falls back to `defaultWidth` (80) (FR-006, SC-004).
