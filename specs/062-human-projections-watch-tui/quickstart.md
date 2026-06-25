# Quickstart — Human Projections (F27) validation

Runnable validation scenarios proving the feature end-to-end. Prerequisites: the repo builds
(`dotnet build FS.GG.Governance.sln`) and tests run (`dotnet test`). Details of types/signatures live in
[data-model.md](./data-model.md) and [contracts/](./contracts/); this is a run/verify guide.

## Build & test

```bash
dotnet build FS.GG.Governance.sln
dotnet test FS.GG.Governance.sln                       # all projects, incl. HumanText.Tests + Cli.Tests
BLESS_SURFACE=1   dotnet test tests/FS.GG.Governance.HumanText.Tests     # (re)bless surface baselines
BLESS_SNAPSHOT=1  dotnet test tests/FS.GG.Governance.HumanText.Tests     # (re)bless plain-text smoke snapshots
```

## Scenario 1 — Plain text and JSON are two projections of one report object (US1, P1) 🎯

```bash
fsgg route --plain   ./fixtures/blocked-tree     # human plain text: verdict, gates, blockers, exit status
fsgg route --json    ./fixtures/blocked-tree      # the byte-identical JSON contract
```

**Expected:** the plain text reports the same verdict, blockers, and exit status as the JSON (report-object
parity); the plain text contains **no** ANSI escapes; the JSON is byte-identical to the pre-F27 golden; rendering
the plain view twice is identical and matches its committed smoke snapshot. Repeat for `explain`, `evidence`,
`verify`, `ship`, `release` (SC-001, SC-002, SC-003).

## Scenario 2 — Rich rendering, and clean degrade (US2, P2)

```bash
fsgg route ./fixtures/blocked-tree                # interactive TTY ⇒ color banner + grouped tables
fsgg route ./fixtures/blocked-tree | cat           # piped (non-TTY) ⇒ ANSI-free plain text
NO_COLOR=1 fsgg route ./fixtures/blocked-tree       # color disabled ⇒ ANSI-free plain text
fsgg route --plain ./fixtures/blocked-tree          # explicit plain ⇒ ANSI-free plain text
COLUMNS=40 fsgg route ./fixtures/blocked-tree        # narrow width ⇒ reflow/truncate, no corrupted layout
fsgg route --json ./fixtures/blocked-tree | grep -c $'\e\['   # ⇒ 0 (JSON never has ANSI, any terminal state)
```

**Expected:** color/tables only on the interactive TTY; every non-TTY / `NO_COLOR` / `--plain` path is ANSI-free
plain text byte-equal to `HumanText`; narrow width never corrupts; `--json` is ANSI-free and byte-identical
regardless of terminal state (SC-004, SC-002). Driven deterministically in tests via Spectre's `TestConsole` at
fixed widths.

## Scenario 3 — Watch re-renders, debounced and read-only (US3, P2)

```bash
fsgg route --watch ./fixtures/work-tree            # re-renders on change; Ctrl-C to exit
# in another shell: touch many files at once (a multi-file save)
```

**Expected:** a tracked-file change re-runs the existing route/evidence/check evaluation and re-renders; a burst of
changes within the debounce window produces **one** re-render after it settles (not one per file); the session
changes no verdict and emits no new contract; a mid-edit unreadable tree surfaces a clear input signal and is
superseded by the next settled re-render — never a crash (SC-005, SC-006, FR-012). The one-re-render-per-burst
property is proved by a pure-`update` test (`watch-mvu.md`).

## Scenario 4 — Optional read-only TUI (US4, P3)

```bash
fsgg tui ./fixtures/blocked-tree                   # navigate gates/blockers/evidence; q to quit
```

**Expected:** the navigable content is a projection of the **same** report object the plain/JSON views use
(report-object parity); navigating changes no verdict, runs no new gate, and emits no contract (SC-006). Tested via
the pure navigation `update` + a Spectre `TestConsole`.

## Scenario 5 — Non-contractual text, JSON frozen (SC-008)

1. Reword a line in `HumanText.ofRouteResult`.
2. `dotnet test` ⇒ only the route smoke snapshot fails; re-bless with `BLESS_SNAPSHOT=1`.
3. Confirm **every** JSON golden stays byte-identical and **no** verdict/exit code changed.

**Expected:** a deliberate plain-text wording change updates the smoke snapshot only — no JSON schema bump, no
verdict/exit-code change (SC-008).

## Scenario 6 — Dependency boundary (SC-007)

```bash
dotnet test tests/FS.GG.Governance.HumanText.Tests --filter DependencyBoundary
```

**Expected:** no pure core, no `*Json`, and no `HumanText` project references the rich-rendering package; only
`FS.GG.Governance.HumanRender` does (SC-007).
