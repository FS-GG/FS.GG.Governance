# Quickstart: Validate Headless Render-Width Test Determinism

Validation guide for spec 091. Proves the WidthResilience tests are deterministic, the assertion reflects
the real folding contract, and the publish gate covers them again. See [contracts/width-resilience.md](./contracts/width-resilience.md)
for the precise invariants and [data-model.md](./data-model.md) for the entities referenced below.

## Prerequisites

- .NET SDK `10.0.x`
- Repo cloned; working dir = repo root
- Files in play: `tests/FS.GG.Governance.Cli.Tests/RenderSupport.fs`,
  `tests/FS.GG.Governance.Cli.Tests/WidthResilienceTests.fs`, `.github/workflows/publish.yml`

## Scenario 1 — Width tests pass locally (US2 / SC-002)

```bash
dotnet test tests/FS.GG.Governance.Cli.Tests/FS.GG.Governance.Cli.Tests.fsproj \
  -c Release --filter "FullyQualifiedName~WidthResilience"
```

**Expected**: all WidthResilience cases pass (full matrix 200/80/40/20/10 + safe-default). No skips, no
`[<Ignore>]`.

## Scenario 2 — Identical result under a headless / invariant-locale shell (US2 / SC-001)

Reproduce the CI-like environment locally:

```bash
DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 LC_ALL=C TERM=dumb \
  dotnet test tests/FS.GG.Governance.Cli.Tests/FS.GG.Governance.Cli.Tests.fsproj \
  -c Release --filter "FullyQualifiedName~WidthResilience"
```

**Expected**: identical pass result to Scenario 1 — the pinned console capabilities make wrapping
independent of inferred host capabilities (C2). The real check is the headless GitHub Actions run on the
same commit going green where it fails today.

## Scenario 3 — Full suite stays green (US1 / FR-007)

```bash
dotnet test tests/FS.GG.Governance.Cli.Tests/FS.GG.Governance.Cli.Tests.fsproj -c Release
```

**Expected**: entire `Cli.Tests` suite passes, including WidthResilience and the other Spectre tests
(RichRender / DegradeToPlain / Tui) — run with **no** exclusion filter.

## Scenario 4 — Publish gate no longer excludes WidthResilience (US1 / SC-003)

```bash
grep -n "WidthResilience" .github/workflows/publish.yml
```

**Expected**: no `FullyQualifiedName!~WidthResilience` filter on the `cli-tests` `dotnet test` step
(the only remaining mention, if any, is the historical note in the comment). The step runs the project
with no per-test exclusion.

## Scenario 5 — The gate demonstrably protects the behavior (US3 / SC-005)

Temporarily break the assertion or the renderer so a line overflows the bound (e.g. lower the bound in
`WidthResilienceTests.fs` to `width - 1`, or feed a longer unbreakable token), then run Scenario 3.

**Expected**: the suite goes red → the publish `cli-tests` gate would fail and block publish. Revert the
deliberate break afterward.

## Scenario 6 — Folding contract is honest, not weakened (US3 / SC-004)

Inspect `WidthResilienceTests.fs`:
- The per-line bound is `max(width, longestUnbreakableToken)` derived from the rendered content (C1),
  not a bare constant and not "output non-empty".
- Widths 10 and 20 are still in the matrix.

**Expected**: at fit-widths the bound equals `width` (same as before); at width 10/20 a line may reach
the token boundary but never beyond.

## Done check

- [ ] Scenarios 1–6 pass / inspect clean
- [ ] WidthResilience green in headless GitHub Actions on the same commit (the canonical SC-001 evidence)
- [ ] Published tool unchanged; no new version cut (FR-006)
- [ ] Issue #32 closed; Coordination board item → **Done** with the determinism approach noted (FR-008/SC-006)
