# Contract: CLI surface additions (Tier 1) — modes, flags, commands; JSON unchanged

F27 adds human-projection **modes** and read-only **commands** to the CLI vocabulary. It adds **no** JSON contract
and changes **no** exit-code scheme; every existing JSON golden stays byte-identical (FR-010, SC-002).

## New / changed CLI vocabulary

| Surface | Form | Behavior | Notes |
|---|---|---|---|
| Default human view | (no flag) | Plain text via `HumanText.of*`, or Rich if interactive TTY + color | Non-contractual (FR-003) |
| Explicit plain | `--plain` (alias `--no-color`) | Forces `Plain`, ANSI-free | Degrade trigger (FR-004) |
| JSON contract | `--json` | The existing byte-identical contract; **always wins** | Unchanged (SC-002) |
| Watch | `--watch` flag (standalone exes) / `watch` subcommand (dispatcher) | Debounced read-only re-render of route/evidence/check | New command surface (FR-007) |
| TUI | `tui` subcommand (dispatcher) | Optional read-only navigator | New command surface (FR-009) |
| `NO_COLOR` env | (env) | Forces `Plain` when set | De-facto standard (FR-004) |

> **"check" binding.** "route/evidence/check" = three existing report objects — `route` (`RouteResult`),
> `evidence` (`CacheEligibilityReport`), and `check` = the `verify` gate-check (`Ship.ShipDecision`). No new report
> object is introduced.
>
> **Host binding.** The packed **`fsgg`** is `FS.GG.Governance.RouteCommand` (route-only); the multi-subcommand
> dispatcher is `FS.GG.Governance.Cli`, packed as **`fsgg-governance`**. The `watch`/`tui` **subcommands** are added
> to the dispatcher; the `--plain`/`--watch` **flags** attach to the standalone packed exes (e.g. `fsgg route
> --watch`) via the shared `HumanRender` edge. The spec spelling "`fsgg watch`/`fsgg tui`" is the generic tool name,
> resolving to `fsgg-governance watch/tui` until the future single-tool unification.

## Guarantees

- **JSON is the only contract** — `--json` output is byte-identical to the pre-F27 golden for identical repo state,
  in every terminal/color state; contains no ANSI (SC-002). Watch/TUI emit **no** JSON contract (FR-008, SC-006).
- **No exit-code change** — the human projections do not alter any command's exit-code scheme (FR-010). Watch/TUI
  are read-only views with no verdict of their own.
- **Width/TTY/color discipline** — Rich appears only on an interactive, color-enabled, non-plain TTY; otherwise
  Plain (FR-004, SC-004).
- **Dependency boundary** — the rich/TUI dependency (Spectre.Console) is referenced only by
  `FS.GG.Governance.HumanRender` (SC-007); `Directory.Packages.props` carries it with a NEED/SCOPE/OWNER comment.

## Tier 1 chain owed

`.fsi` for `HumanText` (RenderMode/HumanText/ReportView) and `HumanRender` (RichRender/Watch/Tui); committed
surface baselines for both; the Spectre.Console pin + justification; tests (per the quickstart); and docs for the
new modes/commands. The plain/rich renderings themselves remain non-contractual (smoke-snapshot stability only).
