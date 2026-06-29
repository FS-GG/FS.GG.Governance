# Phase 0 Research: Headless Render-Width Test Determinism

## R1 — Why do the width tests diverge between local and headless CI?

**Decision**: The divergence is caused by Spectre.Console inferring different *profile capabilities*
(Unicode / output encoding / legacy-console) from the host, not by renderer randomness.

**Evidence (from the codebase)**:
- `RichRender.emitRich` (`src/FS.GG.Governance.HumanRender/RichRender.fs`) renders a `Table` with
  `TableBorder.Rounded` — Unicode box-drawing glyphs (`╭ ─ ╮ │ ╰ ╯`). Cells contain indented labels and
  detail strings, some of which hold unbreakable path-glob tokens (`src/**`, `GovernedPath "src/**"` →
  rendered text), ~17–21 chars.
- The test helper `plainConsole` (`tests/FS.GG.Governance.Cli.Tests/RenderSupport.fs`) pins **only**
  `settings.Ansi = No`, `settings.ColorSystem = NoColors`, `settings.Out`, and `console.Profile.Width`.
  It does **not** pin `console.Profile.Capabilities.Unicode`, the output `Encoding`, or `Legacy`.
- Spectre derives those unpinned capabilities from the environment at `AnsiConsole.Create`. A headless
  Actions runner (different default encoding / `TERM` / Unicode detection) measures glyph and token
  widths differently, so the `Table` column-fit / wrap math differs. At width 10/20 the unbreakable
  token cannot be split, so the line extends to the token boundary — and the strict
  `Expect.isLessThanOrEqual line.Length width` assertion fails in CI but not locally.

**Rationale**: This matches the spec's stated assumption (root cause = environment-inferred capabilities)
and is the only difference between the two consoles' configuration. Pinning the remaining capabilities
removes the host as a variable.

**Alternatives considered**:
- *Renderer non-determinism* — rejected; Spectre wrapping is deterministic given a fully-specified profile.
- *Locale/globalization as the cause* — covered as an edge case (FR-001) but secondary; the profile
  capabilities dominate. Pinning still includes locale-independence by construction (no culture-sensitive
  measurement once Unicode/encoding are fixed).

## R2 — How to pin a fully deterministic Spectre console (0.57.1)?

**Decision**: Build the test console with every wrap-affecting capability pinned:
- `settings.Ansi <- AnsiSupport.No`
- `settings.ColorSystem <- ColorSystemSupport.NoColors`
- `settings.Out <- AnsiConsoleOutput(stringWriter)` over a `StringWriter` whose encoding is fixed (UTF-8)
- after `AnsiConsole.Create`, pin the profile:
  - `console.Profile.Width <- width`
  - `console.Profile.Capabilities.Unicode <- true` (Rounded border + glob tokens measured consistently)
  - `console.Profile.Capabilities.Legacy <- false`
  - (encoding determinism handled via the `StringWriter`/`AnsiConsoleOutput` so no console-codepage inference)

**Rationale**: Spectre's wrapping reads from `Profile.Width` and `Profile.Capabilities`; fixing all of
them makes the emitted layout a pure function of (content, width). `Capabilities` is mutable on the
created profile in 0.57.1, so this is set post-`Create` (mirroring how `Width` is already set today).

**Alternatives considered**:
- *Pin via environment variables in CI only* — rejected; it would make local and CI configs differ and
  is exactly the host-conditional behavior FR-001/SC-002 forbid. The console must be self-pinned in code.
- *Switch the table to an ASCII border (`TableBorder.Ascii`)* — rejected; that changes the product
  renderer's output (out of scope, FR-006) rather than the test's console. Pinning Unicode=true keeps the
  product render path exercised as shipped.

**Verification needed during implementation**: confirm the exact mutable members on
`Profile.Capabilities` in Spectre 0.57.1 (`Unicode`, `Legacy`, `Ansi`, `ColorSystem`, `Interactive`,
`Links`) at the FSI/compile step; set the ones that affect measurement/wrapping.

## R3 — What is the renderer's *real* folding contract to assert?

**Decision**: The invariant is **"every emitted line fits the width, except a line MAY extend up to the
width of the longest unbreakable token in the rendered content when that token exceeds the forced width;
lines never exceed that token boundary (no runaway/corrupted overflow)."**

Concretely, the assertion bound becomes `max(width, longestUnbreakableTokenLength)` rather than a bare
`width`, computed from the same content the renderer is given (so it is derived, not hardcoded). For
widths where everything fits (200/80/40) the bound collapses to `width` — identical to today's check.

**Rationale**: Spectre folds on word/segment boundaries, not mid-token. Asserting a strictly-`<= width`
invariant at width 10 against a 17-char token asserts a guarantee the renderer never made — fragile by
construction (FR-003, US3). Bounding by the token boundary keeps the check meaningful (still catches
doubled/garbled/runaway lines, which would exceed even the token bound) without being a host-specific
patch.

**Alternatives considered**:
- *Keep strict `<= width` and rely on pinning alone* — the spec allows pinning to be sufficient, but
  US3/FR-003 require the assertion to reflect the contract regardless, as durability hardening. Adopted
  both levers.
- *Weaken to "output non-empty"* — rejected; explicitly forbidden by SC-004 (coverage must stay
  meaningful).
- *Drop widths 10/20* — rejected; FR-004/SC-004 require the narrow cases stay.

## R4 — How to remove the publish-gate exclusion safely?

**Decision**: In `.github/workflows/publish.yml`, the `cli-tests` job step changes from
`dotnet test … --filter "FullyQualifiedName!~WidthResilience"` to `dotnet test …` (no filter), and the
preceding comment is updated to record that the determinism fix landed (referencing this spec / #32)
instead of describing the exclusion. The locked-restore step, `enforcement-smoke` job, and publish
sequencing are untouched (FR-005, and the spec's workflow-scope assumption).

**Rationale**: The exclusion was the only delta from full-suite coverage; removing the filter restores
US1. Confining the edit to the one `run:` line keeps the change reviewable and within scope.

**Alternatives considered**:
- *Also re-run the suite in `gate.yml`* — out of scope; `gate.yml` only builds and the item targets the
  publish gate specifically. Not changed.

## R5 — Classification, versioning, and housekeeping

**Decision**: Tier 2 (internal change). No new published `FS.GG.Governance.Cli` version, no `.fsi` or
surface-baseline change, no registry/contract change. On completion, close issue
**FS-GG/FS.GG.Governance#32** and move its Coordination board item to **Done**, noting the determinism
approach (capability pinning + folding-contract assertion + filter removal).

**Rationale**: Matches FR-006/FR-008 and the spec's assumptions; consistent with how #34/090 tracked
closure. Board/issue housekeeping is in scope per the spec.

**Alternatives considered**: Publishing a new version — rejected; nothing in the shipped artifact changes.
