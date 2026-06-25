# Implementation Plan: Human-Projection Host Wiring — Per-Command Plain Delegation, Render-Mode Dispatch, and the `watch`/`tui` Surfaces (F27 wiring)

**Branch**: `063-human-projection-host-wiring` | **Date**: 2026-06-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/063-human-projection-host-wiring/spec.md`

## Summary

F27 (`062-human-projections-watch-tui`) landed two presentation libraries — pure `FS.GG.Governance.HumanText`
(`RenderMode` + `ReportView` + one `of*`/`viewOf*` per report object) and the Spectre-owning
`FS.GG.Governance.HumanRender` (`RichRender.emit`, the read-only `Watch` debounce MVU, the read-only `Tui`
navigation MVU) — fully built and green over the real F18–F26 cores, but **wired into no command host**. This row
consumes those libraries at the host edges: each command's human text branch delegates to the matching
`HumanText.of*` over the **same** report object the host resolved; an edge capability-sensing effect feeds the pure
`RenderMode.selectMode` to dispatch `Json`/`Plain`/`Rich`; and new read-only `watch`/`tui` surfaces drive
`HumanRender.Watch.run`/`Tui.run`. **No** report object, verdict, exit-code scheme, or JSON schema/contract changes;
every existing JSON golden stays byte-identical.

**Technical approach, grounded in a host reconnaissance** (research.md D1–D8). The host code is **not** uniform, and
the plan is shaped by what each host actually holds after evaluation:

- **Clean delegation (report object already matches `HumanText.of*`).** `route` holds `RouteResult`, `ship` and
  `verify` hold `Ship.ShipDecision`, and the standalone `CacheEligibilityCommand` already computes a real
  `CacheEligibility.CacheEligibilityReport` internally (`Loop.fs` `CacheEligibility.evaluate candidates store`).
  These four hosts get the real P1 plain delegation now.
- **Forced bounded deferrals (no matching report object yet — documented, scoped).** `release` holds only the F53
  `ReleaseDecision`; `HumanText.ofReleaseReport` needs the **F26 `ReleaseReport`**, whose assembly
  (`ReleaseReport.assemble` over `SensedRelease` + `PackEvidenceSet` + `AttestationSummary`) **is the separate
  deferred F26 release host-wiring thread** the requester explicitly chose not to take now — so release-human
  delegation is folded into that thread, not pre-empted here. The legacy `Cli`'s `explain` produces
  `Kernel.Check.Explanation list` (F03 proof trees) and its `evidence` produces `Cli.Project.ProjectEvidenceReport`
  (Kernel), neither of which is the F19 `RouteExplanation` / F41 `CacheEligibilityReport` that `HumanText` projects;
  and no host currently surfaces an F19 `RouteExplanation` for a human render. These delegations are **out of scope
  here** and listed as bounded follow-ups (research.md D2).

This reshapes the spec's FR-001 ("each of route/ship/verify/release/explain/evidence MUST delegate") into its
**feasible subset now** (route/ship/verify/evidence-standalone) plus **explicit, technically-forced deferrals**
(release ← F26 thread; explain + legacy-`Cli` evidence ← no matching report object). Everything else in the spec —
render-mode dispatch, ANSI discipline, watch, tui, the dependency boundary, the byte-identical JSON anchor — applies
to the wired hosts unchanged.

**Confirmed planning decisions** (full rationale in [research.md](./research.md)):

1. **Delegate the report portion; keep host-operational lines as host output (D1).** Each host's `renderText`
   becomes `HumanText.of<report> … + the host's own operational lines` (e.g. `wrote <path>` confirmations,
   changed-path counts). The report facts come from `HumanText`; the operational lines stay host-emitted and never
   enter the JSON contract (FR-001, FR-003).
2. **Wire only where the report object matches; defer the rest with rationale (D2).** route/ship/verify/evidence
   now; release/explain/legacy-evidence deferred and scoped (above).
3. **JSON path is untouched; two host JSON shapes are preserved exactly (D3).** For `ship`/`verify`/`release` the
   `--json` stdout IS the persisted `*Json` artifact verbatim; for `route`/`cache` `renderJson` is a separate
   host summary distinct from the persisted artifact. The wiring touches **neither** — `Json` mode keeps calling
   the existing path byte-for-byte (FR-002, FR-005, SC-002).
4. **Capability sensing is an edge effect; `selectMode` stays pure and unchanged (D4).** A new host-edge helper in
   `HumanRender` senses `IsTty`/`NO_COLOR`/`--plain`/`Width` into `RenderMode.ColorCapability`; the existing pure
   `selectMode` decides `Json`/`Plain`/`Rich` (`Json` always wins). No pure function senses (FR-004).
5. **A uniform `--plain` flag layered onto each host's existing format vocabulary (D5).** Hosts differ today
   (`route`/`ship` `--json` bool; `verify` `--format text|json`; `release` `--format text|json|both`; `cache`
   `--format human|json`). `--plain` is added as an explicit-plain signal across them without changing the existing
   `--json`/`--format` semantics; `verify`'s existing deliberate `--json` rejection (from the VerifyCommand feature's
  own spec) is preserved (FR-005, FR-012).
6. **`watch`/`tui` live on the dispatcher; `--watch`/`--plain` also on the packed exes (D6).** The read-only
   `watch`/`tui` subcommands are added to `FS.GG.Governance.Cli` (packed `fsgg-governance`); the `--watch` flag also
   attaches to the packed `fsgg` (`RouteCommand`) through the shared `HumanRender` edge. The watch `reRender`
   callback re-runs the **existing** route/evidence/verify-check evaluation and re-projects — read-only, no contract
   write (FR-007, FR-009).
7. **`HumanRender` stays the sole Spectre owner; hosts reach it only through `HumanRender` (D7).** Each wired host
   gains a `HumanText` reference (plain projection) and, where it renders rich/watch/tui, a `HumanRender` reference;
   **no host adds a direct Spectre reference** (FR-011, SC-007). Spectre is already centrally pinned (0.57.1) — no
   new dependency.
8. **Close F27's `[PARTIAL]` watch end-to-end settle (D8).** F27 left the real-`FileSystemWatcher` end-to-end
   settle test as `[PARTIAL]`; this row adds it as part of the watch host wiring (SC-005).

## Technical Context

**Language/Version**: F# on .NET `net10.0` (`Directory.Build.props`: `TargetFramework=net10.0`,
`TreatWarningsAsErrors=true`, `Nullable=enable`, `GenerateDocumentationFile=true`, `LangVersion=latest`).

**Primary Dependencies**: **No new dependency.** Spectre.Console is already pinned centrally
(`Directory.Packages.props` `0.57.1`) and owned exclusively by `FS.GG.Governance.HumanRender` (the F27 boundary).
The wired hosts add **ProjectReferences** to the already-built `FS.GG.Governance.HumanText` (for `RenderMode` +
`HumanText.of*`) and, where they render rich/watch/tui, `FS.GG.Governance.HumanRender` — never a direct Spectre
reference. The report cores each host already references are reused verbatim (`Route`, `Ship`, `CacheEligibility`,
`Gates`/`GateRun`, the `*Json` libraries).

**Storage**: None new. Hosts keep writing their existing artifacts via the existing `*Json` `WriteArtifact`
effects; the wiring changes only the **summary/human** render branch and adds read-only `watch`/`tui` that write
**no** new artifact (FR-009). The watch path reads the working tree to re-run the existing evaluation.

**Testing**: Expecto 10.2.3 + Expecto.FsCheck / FsCheck 2.16.6 (repo standard). Per wired host, its existing
`.Tests` project is **extended** with (a) a no-`--json` parity test asserting the human output contains the
`HumanText.of*` projection of the resolved report object with no ANSI escapes, and (b) a JSON byte-identity golden
proving the persisted/`--json` contract is unchanged. `FS.GG.Governance.Cli.Tests` (already extended for the F27
edges) gains the render-mode dispatch tests (TTY→Rich, non-TTY/`NO_COLOR`/`--plain`→Plain, `--json`→Json no-rich),
the `watch`/`tui` host wiring tests, and the new **real-`FileSystemWatcher` end-to-end settle** test over a temp
tree (closing F27's `[PARTIAL]`). The pure `Watch.update`/`Tui.update` and `RenderMode.selectMode` are reused from
F27 (already covered). A dependency-boundary check over the wired hosts' `.fsproj` asserts no direct Spectre
reference. Synthetic terminal/event inputs carry `Synthetic` in the test name and are disclosed at the use site
(Constitution V).

**Target Platform**: Cross-platform .NET CLI executables (Linux/macOS/Windows). Rich/watch/tui are
interactive-terminal features that degrade cleanly on non-TTY everywhere.

**Project Type**: Host-edge wiring — no new library, no new pure core. Extends ≤5 existing command-host MVU edges
plus the `Cli` dispatcher; consumes two already-built libraries; single-solution F# layout.

**Performance Goals**: Not a hot path. The delegation is one `HumanText.of*` pass over an already-computed report.
The render dispatch builds the Spectre tree once. The watch loop is event-driven and debounced (F27's 200 ms
window) so a burst of edits costs one re-render.

**Constraints**: Every persisted/`--json` JSON contract stays **byte-identical** for identical repository state
(FR-002, SC-002). Color/ANSI appear **only** in `Rich` (interactive) output and **never** in `Json`,
non-TTY/piped, `NO_COLOR`, or `--plain` output (FR-005, SC-003); `--json` always overrides and never reaches the
rich renderer (SC-004). The Spectre dependency stays confined to `HumanRender` (FR-011, SC-007). `watch`/`tui` are
read-only — no verdict, rule, exit-code, or contract change (FR-009, SC-006). Safe failure preserved: a
missing/malformed input (one-shot) or a transiently-unreadable tree (watch) surfaces a clear input signal, no
crash, no fabricated report, superseded by the next settled re-render (FR-010, Constitution VI).

**Scale/Scope**: Extends **4 hosts now** (route/ship/verify + standalone cache-eligibility plain delegation) + the
`Cli` dispatcher (`watch`/`tui` subcommands, render-mode dispatch) + the packed `fsgg` (`--watch`/`--plain`); a new
host-edge capability-sensing helper in `HumanRender`; the real-`FileSystemWatcher` settle test. **Deferred (scoped
follow-ups):** release-human delegation (← F26 thread), explain + legacy-`Cli` evidence delegation (no matching
report object). **No** new library, report object, verdict, exit-code, JSON schema, or dependency. P1 = the four
plain delegations + JSON byte-identity; P2 = render-mode dispatch + rich + watch; P3 = tui.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation** — PASS. The consumed libraries' surfaces (`HumanText.of*`,
  `RenderMode.selectMode`, `RichRender.emit`, `Watch.run`, `Tui.run`) already exist as `.fsi` and are exercised by
  F27 tests. New host surface — the `watch`/`tui` subcommands and the `--plain`/`--watch` flags — is drafted in each
  affected host's curated `.fsi` and exercised through the loaded host surface (parse/dispatch) before the `.fs`
  body, then surface-baselined.
- **II. Visibility Lives in `.fsi`** — PASS. Each extended host already ships a curated `.fsi`; the new
  subcommand/flag vocabulary and any new edge helper are declared there, `.fs` bodies carry no access modifiers.
  Surface baselines re-blessed for the `Cli` (and any host whose public surface changes); `HumanText`/`HumanRender`
  baselines are unchanged (consumed, not modified). A new `HumanRender` capability-sensing helper, if public, is
  added to its `.fsi` + baseline.
- **III. Idiomatic Simplicity** — PASS. Plain pattern matches on `RenderMode`, pipelines, exhaustive dispatch; no
  SRTP/reflection/type-providers/custom CEs. The one local mutation already present (the watch interpreter's
  change-time ref, F27) is reused as-is with its disclosed reason. The new dependency count is **zero** (Spectre
  already pinned + owned by `HumanRender`).
- **IV. Elmish/MVU Is the Boundary** — PASS. Each host is already an MVU `Loop`/`Interpreter`/`Program`. The
  delegation changes a pure `render` branch only; capability sensing, terminal writes, file sensing, the debounce
  timer, and the tui key loop are `Effect`s executed at the interpreter edge (F27's `Watch`/`Tui`
  `update`/`run` split is reused). Pure transition coverage for watch/tui debounce/navigation comes from F27; this
  row adds the interpreter-edge (real-`FileSystemWatcher`) and host-dispatch tests.
- **V. Test Evidence Is Mandatory** — PASS. Per-host parity + JSON byte-identity tests fail-before/pass-after
  against the **real** report objects (F18–F26 cores never mocked); the watch end-to-end settle runs a real
  `FileSystemWatcher` over a temp tree; the rich/degrade dispatch uses a real Spectre `TestConsole`. Synthetic
  terminal/event inputs are disclosed and `Synthetic`-named.
- **VI. Observability and Safe Failure** — PASS. The wiring preserves each host's input-vs-defect distinction: a
  missing/malformed input in a one-shot render and a transiently-unreadable tree in a watch re-render both surface a
  clear input signal (`WatchSignal.InputUnreadable` for watch), no swallowed error, no crash, no fabricated report,
  superseded by the next settled re-render (FR-010). A blocked verdict renders as blocked (the `HumanText`
  projection already guarantees this).

**Change Classification: Tier 1 (contracted change)** — adds new public CLI command/flag vocabulary
(`--plain`, `--watch`, the `watch`/`tui` subcommands) and changes affected hosts' public `.fsi`/surface baselines.
It introduces **no new dependency** (Spectre already pinned/owned by `HumanRender`), **no** new report object,
verdict, exit-code, or JSON schema/contract — so the migration surface is limited to the new CLI modes/commands;
every existing JSON golden stays byte-identical (documented in `contracts/cli-surface.md`). The full chain applies:
spec, plan, host `.fsi` updates, re-blessed surface baselines, test evidence, and docs.

**Result: PASS — no violations. Complexity Tracking is empty.** (No new dependency; the deferrals are explicit,
bounded follow-ups under the Development Workflow rule, not violations.)

## Project Structure

### Documentation (this feature)

```text
specs/063-human-projection-host-wiring/
├── plan.md                          # This file (/speckit-plan output)
├── research.md                      # Phase 0 — D1..D8 (incl. the report-object reconciliation)
├── data-model.md                    # Phase 1 — render-mode dispatch state, capability sensing, watch/tui host glue
├── quickstart.md                    # Phase 1 — per-story validation scenarios
├── contracts/                       # Phase 1
│   ├── render-dispatch.md           #   per-host: report → HumanText.of* + selectMode → Json|Plain|Rich
│   ├── capability-sensing.md        #   the edge effect filling ColorCapability (TTY/NO_COLOR/--plain/Width)
│   ├── watch-host.md                #   watch subcommand + --watch flag driving HumanRender.Watch.run (read-only)
│   ├── tui-host.md                  #   tui subcommand driving HumanRender.Tui.run (read-only)
│   └── cli-surface.md               #   new flags/commands; JSON byte-identical; deferrals scoped
├── checklists/
│   └── requirements.md              # (already present — spec quality checklist)
└── tasks.md                         # Phase 2 (/speckit-tasks — NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
src/
├── FS.GG.Governance.HumanRender/                       # EXTEND — add the host-edge capability-sensing helper
│   └── Capability.fsi / Capability.fs                  #   senseCapability: edge Effect → RenderMode.ColorCapability
│                                                       #     (TTY/NO_COLOR/--plain/Width); selectMode stays in HumanText
├── FS.GG.Governance.RouteCommand/                      # EXTEND (packed fsgg) — route plain delegation + dispatch
│   ├── Loop.fs(i)                                      #   renderText -> HumanText.ofRouteResult + "wrote" lines;
│   │                                                   #     selectMode dispatch; --plain; route.json byte-identical
│   ├── Interpreter.fs                                  #   Rich/Watch effects via HumanRender at the edge
│   └── FS.GG.Governance.RouteCommand.fsproj            #   + ProjectReference HumanText, HumanRender
├── FS.GG.Governance.ShipCommand/                       # EXTEND — ship plain delegation; audit.json byte-identical
│   └── Loop.fs(i) / Interpreter.fs / *.fsproj
├── FS.GG.Governance.VerifyCommand/                     # EXTEND — verify plain delegation; verify.json byte-identical
│   └── Loop.fs(i) / Interpreter.fs / *.fsproj          #     (preserve --json rejection; --format text|json + --plain)
├── FS.GG.Governance.CacheEligibilityCommand/           # EXTEND — evidence plain delegation over the CacheEligibilityReport
│   └── Loop.fs(i)                                      #     it already computes; cache-eligibility.json byte-identical
└── FS.GG.Governance.Cli/                               # EXTEND (packed fsgg-governance) — render-mode dispatch +
    ├── Cli.fs(i)                                       #     watch/tui subcommands; route/evidence rich/watch via HumanRender
    └── FS.GG.Governance.Cli.fsproj                     #   + ProjectReference HumanText, HumanRender (NO direct Spectre)

# DEFERRED (scoped follow-ups — NOT in this row):
#   FS.GG.Governance.ReleaseCommand  — release-human delegation gated on the F26 ReleaseReport assembly thread
#   FS.GG.Governance.Cli explain     — F03 Check.Explanation list ≠ F19 RouteExplanation (no matching report object)
#   FS.GG.Governance.Cli evidence    — older ProjectEvidenceReport ≠ F41 CacheEligibilityReport

tests/
├── FS.GG.Governance.RouteCommand.Tests/                # EXTEND — no-`--json` parity (HumanText, ANSI-free) + route.json golden
├── FS.GG.Governance.ShipCommand.Tests/                 # EXTEND — parity + audit.json byte-identical
├── FS.GG.Governance.VerifyCommand.Tests/               # EXTEND — parity + verify.json byte-identical
├── FS.GG.Governance.CacheEligibilityCommand.Tests/     # EXTEND — parity + cache-eligibility.json byte-identical
└── FS.GG.Governance.Cli.Tests/                         # EXTEND — render-mode dispatch (Rich/Plain/Json), watch/tui
                                                        #   host wiring, real-FileSystemWatcher end-to-end settle,
                                                        #   wired-host dependency-boundary (no direct Spectre)

surface/
├── FS.GG.Governance.Cli.surface.txt                    # RE-BLESS — watch/tui subcommands + --plain/--watch vocabulary
├── FS.GG.Governance.RouteCommand.surface.txt           # RE-BLESS if --plain/--watch change its public surface
├── FS.GG.Governance.HumanRender.surface.txt            # RE-BLESS if senseCapability is public
└── (Ship/Verify/CacheEligibility host baselines)       # RE-BLESS if --plain changes their public surface

Directory.Packages.props                                # UNCHANGED — Spectre.Console already pinned (0.57.1)
FS.GG.Governance.sln                                    # UNCHANGED — no new project
```

**Structure Decision**: Consume, don't create — and wire at the edge. No new library or pure core is added; the
F27 `HumanText`/`HumanRender` surfaces are consumed at each host's MVU interpreter edge. The only genuinely new
code is a thin host-edge capability-sensing helper (placed in `HumanRender`, the sole Spectre owner, so no host
senses or references Spectre directly), per-host delegation of the text render branch, and the dispatcher's
`watch`/`tui` subcommands. The report-object reconciliation (research.md D2) bounds the row to the hosts whose held
report object already matches `HumanText.of*`; the rest are scoped, technically-forced deferrals. The FR-011/SC-007
boundary stays checkable by a single project-reference assertion over the **wired** hosts: each references
`HumanText` (+ `HumanRender` where it renders rich/watch/tui) and **no** host references Spectre directly.

**Host resolution for `watch`/`tui` and `--watch`/`--plain`** (unchanged from F27's plan). The packed `fsgg` is
`FS.GG.Governance.RouteCommand` (route-only); the multi-subcommand dispatcher is `FS.GG.Governance.Cli`, packed as
`fsgg-governance`. The new read-only `watch`/`tui` **subcommands** are added to the dispatcher, beside
`route`/`explain`/`contract`/`evidence`; the `--watch`/`--plain` **flags** also attach to the packed standalone
`fsgg` through the shared `HumanRender` edge, so `fsgg route --watch`/`--plain` work without duplicating
presentation logic. The generic spelling "`fsgg watch`/`fsgg tui`" resolves to `fsgg-governance watch/tui` until a
future single-tool unification.

## Complexity Tracking

> No Constitution Check violations. **No new dependency** (Spectre already pinned and owned by `HumanRender`). The
> release/explain/legacy-evidence deferrals are explicit, bounded follow-ups under the Development Workflow rule
> ("any intentional deferral MUST be explicit in the spec or plan and scoped as a bounded follow-up"), each gated on
> a missing report object (release ← the F26 `ReleaseReport` assembly thread; explain/legacy-evidence ← no host
> surfaces the F19/F41 object). This section is intentionally empty.
