# Quickstart — Validating Human-Projection Host Wiring (F27 wiring)

Per-story runnable validation. Build/test from repo root. JSON goldens must stay byte-identical throughout.

## Prerequisites

```bash
dotnet build FS.GG.Governance.sln
```

The F27 libraries (`HumanText`, `HumanRender`) are already built and green; this row only wires them into hosts, so
a clean build of the affected host + test projects is the baseline.

## Scenario 1 — Plain delegation is the same report object as JSON (US1, MVP)

For each wired host (`route`, `ship`, `verify`, evidence via `CacheEligibilityCommand`):

```bash
# human (no --json): output contains the HumanText projection of the resolved report object, no ANSI escapes
dotnet test tests/FS.GG.Governance.RouteCommand.Tests   # parity: HumanText.ofRouteResult present, ANSI-free
dotnet test tests/FS.GG.Governance.ShipCommand.Tests    # parity: HumanText.ofShipDecision present
dotnet test tests/FS.GG.Governance.VerifyCommand.Tests  # parity: HumanText.ofVerifyDecision present
dotnet test tests/FS.GG.Governance.CacheEligibilityCommand.Tests  # parity: HumanText.ofCacheEligibilityReport
```

**Expected**: each host's no-`--json` run contains the matching `HumanText.of*` projection (verbatim) of the
**same** report value the JSON path projects (SC-001), with no `ESC[` escapes (SC-003); the host's `wrote <path>`
operational lines remain present and distinct from the report projection (FR-003).

## Scenario 2 — JSON byte-identity (the only contract) (US1)

```bash
# every persisted/--json artifact byte-identical to the pre-wiring golden
dotnet test --filter "FullyQualifiedName~JsonGolden"   # route.json/gates.json/audit.json/verify.json/cache-eligibility.json
```

**Expected**: all JSON goldens byte-identical for identical repository state (SC-002). A deliberate plain-text
wording change updates only a smoke snapshot, no JSON golden (SC-008).

## Scenario 3 — Render-mode dispatch: rich on TTY, plain otherwise, JSON always wins (US2)

```bash
dotnet test tests/FS.GG.Governance.Cli.Tests --filter "FullyQualifiedName~RenderModeDispatch"
```

**Expected** (over a Spectre `TestConsole` + sensed `ColorCapability`):
- interactive TTY, color on, no `--plain` ⇒ `Rich` (color banner + grouped tables) (SC-003).
- non-TTY / `NO_COLOR` / `--plain` ⇒ `Plain` = exact `HumanText.of*`, no ANSI (SC-003).
- `--json` in any terminal state ⇒ `Json`, rich renderer never invoked, output ANSI-free + byte-identical (SC-004).
- narrow/unknown width ⇒ clean reflow/truncate, default 80 on unknown (FR-006).

## Scenario 4 — Watch: debounced, read-only, end-to-end settle (US3)

```bash
dotnet test tests/FS.GG.Governance.Cli.Tests --filter "FullyQualifiedName~Watch"
```

**Expected**:
- pure debounce (F27): a synthetic burst within the window ⇒ one `ReRender` (SC-005).
- **end-to-end settle (new, closes F27 `[PARTIAL]`)**: a real `FileSystemWatcher` over a temp tree, on a
  tracked-file change, invokes `reRender` once after the window settles reflecting the new state (SC-005).
- read-only: only `SenseChanges`/`ScheduleDebounce`/`ReRender`; no verdict/rule/exit-code/contract change (SC-006).
- safe failure: an unreadable mid-edit tree ⇒ `InputUnreadable`, no crash, superseded by next settle (FR-010).

## Scenario 5 — TUI: parity + read-only (US4)

```bash
dotnet test tests/FS.GG.Governance.Cli.Tests --filter "FullyQualifiedName~Tui"
```

**Expected**: `Tui.init(view).View` is the `ReportView` projected from the same report object the other views use
(SC-006); navigation over recorded keys changes only `Path`/`Expanded`; no verdict/gate/contract change (SC-006).

## Scenario 6 — Dependency boundary (no host references Spectre directly) (SC-007)

```bash
dotnet test tests/FS.GG.Governance.Cli.Tests --filter "FullyQualifiedName~DependencyBoundary"
```

**Expected**: parsing the wired hosts' `.fsproj` shows each references `HumanText` (+ `HumanRender` where it renders
rich/watch/tui) and **none** references `Spectre.Console` directly; only `HumanRender` does (FR-011, SC-007).

## Scenario 7 — Full-suite green gate (SC-008)

```bash
dotnet build FS.GG.Governance.sln && dotnet test
```

**Expected**: whole solution green; every pre-wiring JSON golden byte-identical; F27 smoke snapshots stable.

## Out of scope (scoped deferrals — see contracts/cli-surface.md)

- `release` human delegation (gated on the F26 `ReleaseReport` assembly thread).
- `explain` + legacy-`Cli` `evidence` delegation (no matching F19/F41 report object yet).
