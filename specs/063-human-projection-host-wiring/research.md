# Phase 0 Research — Human-Projection Host Wiring (F27 wiring)

This row consumes the already-built F27 libraries (`FS.GG.Governance.HumanText`, `FS.GG.Governance.HumanRender`) at
the command-host edges. The decisive Phase 0 work was a **host reconnaissance** establishing what each host
actually holds after evaluation and how it renders today — because the host code is not uniform, and the spec
(written before the reconnaissance) assumed a clean six-command delegation that the code does not support
uniformly. The decisions below record the reconciliation and the wiring approach.

## Host reconnaissance (the facts the decisions rest on)

| Host (project) | Report object held after eval | `--json`/`--format` today | `renderJson` vs persisted artifact | "wrote …" line in text? | `HumanText.of*` match |
|---|---|---|---|---|---|
| `route` (`RouteCommand`, packed `fsgg`) | `RouteResult` (`model.Result`) | `--json` bool → `OutputFormat.Text\|Json` | **separate** summary (route.json persisted via RouteJson to `RouteOut`) | yes | ✅ `ofRouteResult` |
| `ship` (`ShipCommand`) | `Ship.ShipDecision` (`model.Decision`) | `--json` bool (+ `--mode`/`--profile`) | `renderJson` **IS** persisted `audit.json` verbatim | yes | ✅ `ofShipDecision` |
| `verify` (`VerifyCommand`) | `Ship.ShipDecision` (`model.Decision`) | `--format text\|json` (`--json` **rejected** per VerifyCommand's own spec; `--profile`) | `renderJson` **IS** persisted `verify.json` verbatim | yes | ✅ `ofVerifyDecision` |
| `release` (`ReleaseCommand`) | F53 `ReleaseDecision` (`model.Decision`) | `--format text\|json\|both` | `renderJson` **IS** persisted `release.json` verbatim | **no** | ❌ wants F26 `ReleaseReport` |
| evidence (`CacheEligibilityCommand`) | `FreshnessResolutionReport` in `Model`; **but computes `CacheEligibility.evaluate candidates store : CacheEligibilityReport` internally** | `--format human\|json` | **separate** summary (cache-eligibility.json persisted via CacheEligibilityJson) | yes | ✅ `ofCacheEligibilityReport` (the report is in hand) |
| `explain` (`Cli` dispatcher) | `Kernel.Check.Explanation list` (F03 proof trees) | `--json`/`--format` → `Text\|Json` | manual JSON | n/a | ❌ wants F19 `RouteExplanation` |
| evidence (`Cli` dispatcher) | `Cli.Project.ProjectEvidenceReport` (Kernel) | as above | manual JSON | n/a | ❌ wants F41 `CacheEligibilityReport` |

Source: `RouteCommand/Loop.fs:706/759/790`, `ShipCommand/Loop.fs:765/810-815`, `VerifyCommand/Loop.fs:785/840`,
`ReleaseCommand/Loop.fs:247/275/284` + `Loop.fsi:98`, `CacheEligibilityCommand/Loop.fs:309/471/510` +
`Loop.fsi:123`, `Cli/Cli.fs:168-174/434-443/491-506/667-681`, `ReleaseReport/Report.fsi:69` (`assemble` needs
`decision + SensedRelease + PackEvidenceSet + Preconditions + AttestationSummary + ExitCodeBasis`).

## D1 — Delegate the report portion; keep host-operational lines as host output

- **Decision**: Each wired host's text render becomes the matching `HumanText.of*` projection of the **same** report
  object it resolved, **concatenated with** the host's own operational lines (e.g. `wrote <path> (<schema>)`
  confirmations, changed-path counts). The `HumanText` projection supplies the report facts (verdict, gates,
  blockers, exit status); the operational lines stay host-emitted and remain distinct from the report projection.
- **Rationale**: Four of five console renderers emit "wrote …" lines that are **not** part of the report object
  (`release` is the exception). These are genuine host context (what files were written, how many paths changed),
  not report facts, so they belong to the host, not `HumanText`. Delegating only the report portion preserves the
  single-source-of-truth property (the report facts now come from the shared projection) while keeping the host's
  operational narration. The operational lines never enter the JSON contract (FR-001, FR-003).
- **Alternatives considered**: (a) Move "wrote …" lines into `HumanText` — rejected: they are not report facts and
  would pollute the pure projection with host I/O context. (b) Drop the "wrote …" lines — rejected: a behavior
  regression operators rely on. (c) Have `HumanText` take an extra "operational lines" parameter — rejected:
  needless surface change to a frozen F27 library; host concatenation is simpler.

## D2 — Wire only where the report object matches; defer the rest with rationale (the core reconciliation)

- **Decision**: Land the real plain delegation now for the hosts whose held report object already matches a
  `HumanText.of*`: **`route` (`RouteResult`), `ship`/`verify` (`ShipDecision`), and the standalone evidence host
  `CacheEligibilityCommand`** (which already computes a `CacheEligibilityReport` internally). **Defer** the rest as
  explicit, bounded follow-ups:
  - **`release` → DEFERRED, gated on the F26 thread.** `HumanText.ofReleaseReport` needs the F26 `ReleaseReport`,
    whose `assemble` requires `SensedRelease` (F54), `PackEvidenceSet` (F26 — real `pack` runs), and
    `AttestationSummary` (F26). The `ReleaseCommand` holds only the F53 `ReleaseDecision`. **Building the
    `ReleaseReport` IS the separate deferred F26 release host-wiring thread** the requester explicitly chose not to
    take in this row. Pre-empting it here would duplicate/contradict that thread. So release-human delegation is
    folded into the F26 wiring when it lands.
  - **`explain` (legacy `Cli`) → DEFERRED, no matching report object.** The `Cli`'s `explain` produces
    `Kernel.Check.Explanation list` (F03 per-rule proof trees), not the F19 `RouteExplain.RouteExplanation` that
    `HumanText.ofRouteExplanation` projects. No current host surfaces an F19 `RouteExplanation` for a human render
    (F19 is used inside route-cost explanation). Wiring would require routing an F19 report through a host — out of
    scope here.
  - **`evidence` (legacy `Cli`) → DEFERRED, older report object.** The `Cli`'s `evidence` produces
    `Cli.Project.ProjectEvidenceReport` (Kernel evidence), not the F41 `CacheEligibilityReport`. The **standalone**
    `CacheEligibilityCommand` is the correct, feasible home for evidence-human delegation (it has the F41 report in
    hand); the legacy `Cli` evidence path keeps its current rendering.
- **Rationale**: This is the honest, deliverable scope. The spec's FR-001 named all six commands as MUST, but the
  code shows three of them have no matching report object yet — two for a forced upstream reason (release ← F26)
  and the legacy `Cli` paths because they predate the F19/F41 report objects. Constitution Development Workflow
  requires intentional deferrals to be explicit and bounded; this decision makes them so. The wired subset still
  delivers F27's headline value ("operator UX improves without a second source of truth") for the four most-used
  commands.
- **Alternatives considered**: (a) Force all six by constructing the missing report objects in this row — rejected:
  for `release` it duplicates the F26 thread; for explain/legacy-evidence it invents report routing the F19/F41
  cores don't yet feed. (b) Re-target `HumanText.ofRouteExplanation`/`ofCacheEligibilityReport` to the legacy
  Kernel types — rejected: changes the frozen F27 library and conflates two different report contracts. (c) Narrow
  the spec instead of the plan — the plan is the correct place to record a code-driven scope reconciliation; the
  spec's intent (disciplined human projections over the report objects) is unchanged.

## D3 — JSON path untouched; both host JSON shapes preserved exactly

- **Decision**: The wiring touches **only** the text/human render branch. `Json` mode keeps calling each host's
  existing JSON path byte-for-byte. Two shapes exist and both are preserved: for `ship`/`verify`/`release` the
  `--json` stdout **is** the persisted `*Json` artifact verbatim; for `route`/`cache` `renderJson` is a **separate**
  host summary distinct from the persisted artifact. Neither is altered.
- **Rationale**: JSON is the only contract (spec FR-002, SC-002). The reconnaissance shows the two shapes are
  intentional and load-bearing (e.g. ShipCommand's comment: "`--json` stdout equals the persisted file
  byte-for-byte"). Keeping the `Json` branch literally unchanged is the cleanest guarantee of byte-identity.
- **Alternatives considered**: Unify the two JSON shapes — rejected: out of scope and would break goldens.

## D4 — Capability sensing is an edge effect; `selectMode` stays pure and unchanged

- **Decision**: Add a host-edge helper `senseCapability` (in `HumanRender`, the sole Spectre owner) that reads
  `IsTty` / `NO_COLOR` / explicit `--plain` / terminal `Width` into a `RenderMode.ColorCapability`. The existing
  pure `RenderMode.selectMode (explicitJson) capability` (F27, unchanged) decides `Json`/`Plain`/`Rich` — `Json`
  always wins; `Rich` iff `IsTty ∧ ¬NoColorEnv ∧ ¬ExplicitPlain`; else `Plain`.
- **Rationale**: Constitution IV — sensing is I/O and belongs at the interpreter edge; the decision stays pure and
  exhaustively testable (F27 already covers `selectMode`). Placing `senseCapability` in `HumanRender` keeps every
  host free of a direct Spectre/console reference (D7). `Width` is sensed but is not part of the mode decision (it
  is consumed at render time by `RichRender`, safe default 80).
- **Alternatives considered**: (a) Sense inside each host — rejected: duplicates logic and risks a host touching
  console internals. (b) Extend `selectMode` to sense — rejected: makes a pure function impure; violates D6 of F27.

## D5 — A uniform `--plain` flag layered onto each host's existing format vocabulary

- **Decision**: Add `--plain` as an explicit-plain signal to each wired host, mapped to
  `ColorCapability.ExplicitPlain = true`, **without** changing the host's existing `--json`/`--format` semantics.
  Preserve `verify`'s deliberate `--json` rejection (from the VerifyCommand feature's own spec) — `--plain` is
  accepted there alongside `--format text|json`. `--json`/`--format json`/`--format human` keep selecting the existing JSON/text paths;
  `--plain` only forces `Plain` over `Rich` when a human (non-JSON) mode is selected.
- **Rationale**: The hosts' flag vocabularies differ (`route`/`ship` `--json` bool; `verify` `--format text|json`;
  `release` `--format text|json|both`; `cache` `--format human|json`). A single new `--plain` token that means
  "explicit plain, no color" composes cleanly with all of them and matches `NO_COLOR`'s effect. It is additive
  public CLI vocabulary (Tier 1) but changes no existing flag's meaning.
- **Alternatives considered**: (a) A `--rich` opt-in instead of auto-sensing — rejected: F27's `selectMode` already
  auto-selects `Rich` on an interactive TTY; `--plain` is the documented escape hatch (FR-004). (b) `--no-color`
  alias — acceptable as a documented synonym for `--plain`/`NO_COLOR`; recorded in `cli-surface.md`, optional.

## D6 — `watch`/`tui` on the dispatcher; `--watch`/`--plain` also on the packed exes

- **Decision**: Add read-only `watch` and `tui` **subcommands** to `FS.GG.Governance.Cli` (packed
  `fsgg-governance`), beside `route`/`explain`/`contract`/`evidence`. Attach the `--watch` **flag** to the packed
  `fsgg` (`RouteCommand`) so `fsgg route --watch` works. Both drive `HumanRender.Watch.run root mode clock reRender
  shouldStop`, where the host supplies: a `clock` thunk, a **read-only** `reRender : root -> RenderMode ->
  WatchSignal` that re-runs the **existing** route/evidence/verify-check evaluation and re-projects via the D4
  dispatch (writing **no** new contract), and a `shouldStop` predicate. `tui` drives `HumanRender.Tui.run view
  readKey draw` over the `ReportView` projected from the same report object.
- **Rationale**: Matches the F27 plan's host-resolution decision and the interpreter-edge signatures the
  reconnaissance confirmed (`Watch.run`/`Tui.run` take exactly these callbacks). The watch triad is the existing
  `route` (`RouteResult`) / `evidence` (`CacheEligibilityReport`) / `verify`-check (`ShipDecision`) — no new "check"
  report object. Read-only is guaranteed because `reRender` calls only the existing evaluation + projection and
  emits no `WriteArtifact` (FR-009, SC-006).
- **Alternatives considered**: (a) A standalone `fsgg-watch` exe — rejected: duplicates the dispatcher and the
  shared `HumanRender` edge. (b) Watch every command — scoped to the route/evidence/check triad per spec FR-007.

## D7 — `HumanRender` stays the sole Spectre owner

- **Decision**: Each wired host adds a ProjectReference to `HumanText` (for the plain projection + `RenderMode`) and,
  where it renders rich/watch/tui, to `HumanRender`. **No host adds a direct `Spectre.Console` PackageReference.**
  The capability-sensing helper and all console writes live in `HumanRender`.
- **Rationale**: Preserves the F27 FR-013/SC-007 boundary (a dependency-boundary test already enforces that only
  `HumanRender` references Spectre; this row extends that assertion over the wired hosts). Spectre is already pinned
  centrally at 0.57.1 — **no new dependency** is introduced.
- **Alternatives considered**: Let a host reference Spectre for a one-off width read — rejected: breaks the single
  owner boundary; `senseCapability` in `HumanRender` covers it.

## D8 — Close F27's `[PARTIAL]` watch end-to-end settle

- **Decision**: Add the real-`FileSystemWatcher` end-to-end settle test (F27 left it `[PARTIAL]` — pure `update`
  covered, interpreter `run` not yet exercised end-to-end). Drive `Watch.run` over a temp tree, mutate a tracked
  file, and assert exactly one settled `reRender` invocation reflecting the new state, then a clean stop.
- **Rationale**: The watch host wiring is the first real consumer of `Watch.run`; its interpreter edge must have
  real evidence (Constitution V) and SC-005 requires the end-to-end settle, not just the pure debounce.
- **Alternatives considered**: Rely on the pure `update` tests only — rejected: leaves the `FileSystemWatcher` edge
  unexercised, exactly the gap F27 flagged.

## Resolved unknowns

- **Report-object availability per host** — RESOLVED by the reconnaissance (D2): route/ship/verify/evidence-standalone
  match; release/explain/legacy-evidence do not (deferred).
- **Where capability sensing lives** — RESOLVED (D4/D7): `senseCapability` in `HumanRender`; `selectMode` unchanged.
- **How `--plain` composes with heterogeneous flag vocabularies** — RESOLVED (D5): additive token, no existing-flag
  meaning change, `verify` `--json` rejection preserved.
- **`Watch.run`/`Tui.run` host obligations** — RESOLVED (D6): clock + read-only reRender + shouldStop; readKey +
  draw. Confirmed against the F27 `.fsi` signatures.
- **New dependency?** — RESOLVED: none. Spectre already pinned and owned by `HumanRender`.
