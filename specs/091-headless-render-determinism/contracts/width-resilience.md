# Contract: Width-Resilience Test Determinism

This feature exposes no new product/public API. The contracts it must honor are (a) the renderer's
folding behavior the test asserts, and (b) the CI publish-gate invocation. Both are verifiable.

## C1 — Folding-contract assertion (the test invariant)

**Given** the rich-render surface (`RichRender.emit RenderMode.Rich`) writing into a fully-pinned
deterministic console at forced width `w`, **for every emitted line**:

```
line.Length <= max(w, longestUnbreakableToken)
```

where `longestUnbreakableToken` is derived from the rendered content (not hardcoded), and the renderer
folds on word/segment boundaries — never mid-token.

- When all content fits (`w` ≥ longest token, e.g. 200/80/40): bound collapses to `w` — identical to the
  pre-fix assertion.
- When `w` < longest token (e.g. 10 vs a ~17-char glob): a line MAY reach the token boundary; anything
  beyond it is a contract violation (runaway/corrupted layout) and MUST fail.

**Must reject**: a line longer than the bound (doubled/garbled/overflowing layout).
**Must not**: weaken to "output non-empty", skip widths, or branch on host.

## C2 — Deterministic console capabilities (the determinism precondition)

The console used by the width suite MUST pin all wrap-affecting capabilities to fixed values so the
emitted layout is a pure function of (content, width), identical on local and headless CI:

`Ansi=No`, `ColorSystem=NoColors`, output encoding=UTF-8 (via the writer), `Profile.Width=w`,
`Capabilities.Unicode=true`, `Capabilities.Legacy=false`.

**Verification**: same commit, same test, same result locally and in headless GitHub Actions across the
full width matrix (SC-001, SC-002).

## C3 — Publish `cli-tests` gate invocation (the CI contract)

The publish workflow's `cli-tests` step MUST run the `Cli.Tests` project with **no** WidthResilience
exclusion filter:

```
dotnet test tests/FS.GG.Governance.Cli.Tests/FS.GG.Governance.Cli.Tests.fsproj -c Release --no-restore
```

(no `--filter "FullyQualifiedName!~WidthResilience"`).

**Verification**:
- The step contains no `FullyQualifiedName!~WidthResilience` (or equivalent) filter (SC-003).
- A deliberately-introduced width regression turns the gate red and blocks publish (SC-005).
- The locked-restore step, `enforcement-smoke` job, and publish ordering are unchanged.

## Unaffected contracts (regression guard)

- Published `FS.GG.Governance.Cli` enforcement behavior (route/ship/verify verdicts) — UNCHANGED; no new
  version (FR-006).
- Other Spectre tests (RichRender / DegradeToPlain / Tui) — still pass in CI (FR-007).
- No `.fsi`, surface-baseline, config-schema, or cross-repo contract change.
