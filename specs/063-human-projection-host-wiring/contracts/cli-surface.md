# Contract — CLI Surface Changes & JSON Byte-Identity

This row adds new public CLI command/flag vocabulary (Tier 1) and changes **no** JSON contract. It introduces **no
new dependency** (Spectre already pinned 0.57.1, owned by `HumanRender`).

## New CLI vocabulary (additive)

| Surface | Host | Meaning |
|---|---|---|
| `--plain` (and optional `--no-color` synonym) | `route`, `ship`, `verify`, evidence(`CacheEligibilityCommand`), dispatcher | Force explicit-plain (`ColorCapability.ExplicitPlain = true`): no color even on a TTY. Does not change `--json`/`--format` meaning. |
| `--watch` | packed `fsgg` (`RouteCommand`) | Read-only debounced re-render of the route report on working-tree change (drives `HumanRender.Watch.run`). |
| `watch` (subcommand) | dispatcher `fsgg-governance` | Read-only watch over the route/evidence/check triad. Generic spelling: `fsgg watch`. |
| `tui` (subcommand) | dispatcher `fsgg-governance` | Optional read-only navigator over the report's `ReportView`. Generic spelling: `fsgg tui`. |

- `verify` keeps **rejecting** `--json` (per the VerifyCommand feature's own spec); `--plain` is accepted alongside
  its `--format text|json`.
- Existing flags (`--json`, `--format text|json|both|human`, `--mode`, `--profile`, `--repo`, …) keep their current
  meaning.

## Render-mode behavior (all wired hosts)

| Sensed / requested state | Selected mode | Output |
|---|---|---|
| `--json` / `--format json` requested | `Json` | existing JSON contract, ANSI-free, **byte-identical** to pre-wiring golden |
| interactive TTY, color on, no `--plain` | `Rich` | color banner + grouped tables (via `HumanRender.RichRender`) |
| non-TTY / piped / `NO_COLOR` / `--plain` | `Plain` | exact `HumanText.of*` string, no ANSI |

`--json` always overrides and never reaches `RichRender` (SC-004).

## JSON byte-identity anchor (the only contract)

- Every persisted/`--json` artifact stays **byte-identical** for identical repository state: `route.json`,
  `gates.json`, `audit.json` (ship), `verify.json`, `cache-eligibility.json` (+ its unresolved sidecar). The
  `--json` branch of each host is literally unchanged (research.md D3). (SC-002, FR-002.)
- Plain/rich text is **non-contractual** — held only to the F27 smoke-snapshot stability; a wording change updates a
  smoke snapshot and **no** JSON golden (SC-003, SC-008).

## Surface baselines (Tier 1)

- **Re-bless**: `FS.GG.Governance.Cli.surface.txt` (watch/tui subcommands + flag vocabulary); any host whose public
  `.fsi` gains `--plain`/`--watch` parsing surface; `FS.GG.Governance.HumanRender.surface.txt` if `senseCapability`
  is public.
- **Unchanged**: `FS.GG.Governance.HumanText.surface.txt` and the `HumanRender` `RichRender`/`Watch`/`Tui` surfaces
  (consumed, not modified).

## Dispatcher `watch`/`tui` — RESOLVED by composing the F19 `RouteResult` in the dispatcher

The dispatcher's read-only `watch`/`tui` subcommands are **delivered**. Because the kernel-era `Cli` holds the
`Kernel.Route`/`ProjectEvidenceReport` (whose JSON is the byte-identical contract) and not the F19/F41 report
objects `HumanText` projects, the dispatcher **composes a real F19 `RouteResult`** over the repo root by reusing
the RouteCommand pipeline (`Program.composeRouteView` = `RouteCommand.Interpreter.run` with no-op write/output
ports + `Loop.humanView`) and projects it to the shared `ReportView`. These are **new** read-only surfaces with no
JSON contract, so the byte-identity anchor (SC-002) and report-object-identity (SC-001) both hold. `--plain`
(`--no-color`) is added to the dispatcher parser; `requestJson` is unchanged. Proven by `WatchTuiHostWiringTests`
over a real temp git tree + a real-binary `fsgg-governance tui` smoke.

## Deferrals (scoped — NOT in this row; see research.md D2)

| Deferred surface | Reason | Gated on |
|---|---|---|
| legacy `Cli` **one-shot** `route`/`evidence` *human* delegation | the one-shot commands' JSON is the frozen contract built from `Kernel.Route`/`ProjectEvidenceReport`; projecting the F19/F41 object as their human view would make the human a *second source of truth* (SC-001). The F19/F41 human projection ships through the new `watch`/`tui` surfaces instead. | a future contract migration that re-truths the one-shot commands onto F19/F41 |
| `release` human delegation (`ofReleaseReport`) | `ReleaseCommand` holds only F53 `ReleaseDecision`; `ReleaseReport.assemble` needs F54 `SensedRelease` + F26 `PackEvidenceSet` + `AttestationSummary` | the deferred **F26 release host-wiring thread** |
| `explain` human delegation (`ofRouteExplanation`) | `Cli` `explain` = F03 `Check.Explanation list`, not F19 `RouteExplanation`; no host surfaces an F19 explanation for human render | a future host that surfaces an F19 `RouteExplanation` |

## Documentation (FR-012)

CLI docs/README updated for: the three render modes (JSON / plain / rich), `--plain`/`--no-color`/`NO_COLOR`/TTY
behavior, `--watch` / `fsgg watch`, `fsgg tui`, the host-resolution note (`fsgg` vs `fsgg-governance`), and the
statement that plain/rich are non-contractual while JSON is the only contract. The scoped deferrals are recorded so
the next maintainer knows release/explain/legacy-evidence human delegation is intentionally pending.
