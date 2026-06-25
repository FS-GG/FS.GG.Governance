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

## Deferrals (scoped — NOT in this row; see research.md D2)

| Deferred surface | Reason | Gated on |
|---|---|---|
| `release` human delegation (`ofReleaseReport`) | `ReleaseCommand` holds only F53 `ReleaseDecision`; `ReleaseReport.assemble` needs F54 `SensedRelease` + F26 `PackEvidenceSet` + `AttestationSummary` | the deferred **F26 release host-wiring thread** |
| `explain` human delegation (`ofRouteExplanation`) | `Cli` `explain` = F03 `Check.Explanation list`, not F19 `RouteExplanation`; no host surfaces an F19 explanation for human render | a future host that surfaces an F19 `RouteExplanation` |
| legacy `Cli` `evidence` delegation | `Cli` evidence = older `ProjectEvidenceReport`, not F41 `CacheEligibilityReport` (the standalone `CacheEligibilityCommand` IS wired) | routing the F41 report through the dispatcher |

## Documentation (FR-012)

CLI docs/README updated for: the three render modes (JSON / plain / rich), `--plain`/`--no-color`/`NO_COLOR`/TTY
behavior, `--watch` / `fsgg watch`, `fsgg tui`, the host-resolution note (`fsgg` vs `fsgg-governance`), and the
statement that plain/rich are non-contractual while JSON is the only contract. The scoped deferrals are recorded so
the next maintainer knows release/explain/legacy-evidence human delegation is intentionally pending.
