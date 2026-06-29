# Research: Publish the Consumer-Bearing Governance CLI to the Org Feed

Phase 0 decisions. Each resolves an unknown from the Technical Context. Format: Decision / Rationale / Alternatives considered. Grounded in the in-repo CI (`gate.yml`, `nuget.config`, the CLI fsproj) and the org precedent (`FS-GG/FS.GG.SDD/.github/workflows/release.yml`, `FS-GG/.github` `registry/dependencies.yml`, `FS-GG/FS.GG.Templates` composition test).

---

## D1 — Published version: `1.1.0`

**Decision**: Raise `src/FS.GG.Governance.Cli/FS.GG.Governance.Cli.fsproj` `<Version>` from `0.1.1` to **`1.1.0`** and publish that.

**Rationale**: FR-004 requires a version strictly greater than every predecessor build. Predecessors on the local dev feed are `1.0.0` (2026-06-18) and `0.1.1` (2026-06-25) — note the current source version `0.1.1` is *lower* than the stray `1.0.0`, so simply publishing `0.1.1` (or a `0.1.x` bump) would not be greater than `1.0.0` and would be ambiguous against it. `1.1.0` is unambiguously greater than both. A **minor** bump (not patch) is the honest SemVer signal: relative to the `1.0.0` build, the tool gains an externally-observable capability — a produced `governance-handoff.json` now drives the `route`/`ship`/`verify` verdict (the spec-081 consumer is reachable) — which is additive feature behavior, not a fix.

**Alternatives considered**:
- `0.1.2` / `0.2.0` — rejected: not greater than the `1.0.0` predecessor; violates FR-004 and leaves the consumer-bearing build ambiguous against the older, higher-numbered `1.0.0`.
- `1.0.1` (patch) — rejected: understates the change; the consumer-enforcement is new user-observable behavior vs `1.0.0`, which is a minor, not a patch.
- `2.0.0` (major) — rejected: no breaking change to the CLI's surface or behavior; a major bump would falsely signal a break to the registry range.

---

## D2 — Publish mechanism: a repo-owned `publish.yml` mirroring SDD's `release.yml`, scoped to the CLI

**Decision**: Add `.github/workflows/publish.yml`, modeled on the org-canonical `FS-GG/FS.GG.SDD/.github/workflows/release.yml`:
- **Triggers**: `release: types: [published]`, `push: tags: ['v*']`, and `workflow_dispatch` (manual/dry-run).
- **Version source**: read from the fsproj via `dotnet msbuild -getProperty:Version` (no hardcoded tag value); a `v<semver>` tag must match the CLI fsproj `<Version>`.
- **Permissions**: `packages: write` on the publish job only; run-scoped `${{ secrets.GITHUB_TOKEN }}` (no PAT).
- **Push**: `dotnet pack src/FS.GG.Governance.Cli/...` then `dotnet nuget push <pkg> --source https://nuget.pkg.github.com/FS-GG/index.json --api-key ${{ secrets.GITHUB_TOKEN }} --skip-duplicate`.
- **Scope**: packs and pushes **only the CLI tool package** (its dependency closure bundles `Adapters.SddHandoff.dll`). Pre-publish gate: locked restore + `FS.GG.Governance.Cli.Tests` (mirrors SDD's `cli-tests` job) + the D3 enforcement smoke.

**Rationale**: Reuses the exact pattern the org already operates and documents (SDD `specs/044-publish-cli-tool/contracts/release-workflow.md`), so the publish is least-privilege, idempotent (`--skip-duplicate`), and version-coherent (FR-004/005/007/010). Repo-owned (not a reusable workflow) because no publish-specific reusable workflow exists in `FS-GG/.github` yet (only the `contract-coherence.yml` *read* gate).

**Alternatives considered**:
- A reusable org publish workflow — rejected/deferred: none exists; authoring one is broader than this item. Follow-up if/when multiple repos need it.
- Publishing all ~70 packable `FS.GG.Governance.*` packages here — rejected: out of scope (that is the H4/088-adjacent org-feed-baseline work). This item needs only the reachable enforcement surface — the CLI tool — whose package bundles its dependencies for `dotnet tool install`.
- Manual local `dotnet nuget push` from a workstation — rejected: not repeatable/auditable (FR-010); a workflow is the durable path.

---

## D3 — Green-by-omission guard: a real-evidence enforcement smoke test gating the push

**Decision**: Before pushing, the workflow packs the CLI, `dotnet tool install`s it into a temp dir, and runs the installed `fsgg-governance route --root <fixture> --mode gate` against two committed product fixtures:
- `failing-handoff/` → assert the process **blocks**: exit `2` (`GovernedBlocking`, per `Cli.fs:341`).
- `passing-handoff/` → assert exit `0` (`Success`).
- light/non-strict mode over the failing fixture → assert no block (exit `0`).

The push step runs only if the smoke passes. As a cheap structural backstop, also assert `FS.GG.Governance.Adapters.SddHandoff.dll` is present in the packed tool's `tools/**` payload.

**Rationale**: This is the FR-008 guard that makes "consumer-less build cannot be published under the consumer-bearing version" enforceable, and it is exactly the strict-blocks/light-passes contract the Templates#25 probe asserts (so a published artifact that passes the smoke will flip that probe from SKIP to assert — SC-003). It is real evidence (Principle V): the actual packed tool, run end-to-end against real handoff JSON, asserting the real exit-code contract — no mocks. It fails-before (the predecessor `1.0.0`/`0.1.1` builds would not block and would be caught) / passes-after.

**Alternatives considered**:
- Static-only check (just assert the dll is in the package) — rejected as sole guard: presence of the assembly does not prove the route path actually consumes the handoff; the behavioral exit-code assertion is the honest signal (kept the static check only as a cheap backstop).
- Trusting the unit tests alone — rejected: unit tests exercise the source, not the *packed-and-installed tool*; the whole failure mode is "source has it, the published tool doesn't," which only an install-and-run smoke catches.

---

## D4 — Coherence record: append a `coherence:` entry to the org registry (no contract bump)

**Decision**: In a cross-repo PR to `FS-GG/.github`, append a `coherence:` entry to `registry/dependencies.yml` recording the consumer-side verification of `governance-handoff@1.0.0`, with fields matching the existing schema: `id`, `coherent`, `owner: governance`, `summary`, `resolved_by` (the publish PR/commit + tag), `impact`, `tracking` (FS.GG.Governance#28). Leave the `governance-handoff` contract entry (`version: "1.0.0"`, `owner: sdd`, `consumers: [governance]`) **unchanged**. The human projection `docs/registry/compatibility.md` auto-syncs via the org `contract-change` flow — no manual edit there.

**Rationale**: FR-006 requires recording this as a consumer-side coherence verification, not a contract surface change. The registry is the source of *contracts/coherence*; the existing `coherence:` list (e.g. `fs-gg-ui-version`) is the precedent shape. No `version`/`range`/`surface` of `governance-handoff` changes, so it is not a `contract-change` to the contract itself — only a coherence assertion that the consumer side is now exercisable.

**Alternatives considered**:
- Bumping `governance-handoff` to `1.0.1`/`1.1.0` — rejected: the contract surface is unchanged (consumer-only verification); a version bump would falsely imply a producer-side change and ripple to SDD.
- Editing `docs/registry/compatibility.md` directly — rejected: it is a generated projection; the org flow regenerates it.

---

## D5 — Decision record: local `docs/decisions/0004`, ratifying the first publish

**Decision**: Record the first publish of `FS.GG.Governance.Cli` to the org feed in a **local** decision record `docs/decisions/0004-publish-governance-cli-org-feed.md` (the repo's existing series: 0001 logging, 0002 handoff contract, 0003 gaterunhost). Capture: the publishing contract (triggers, version derivation, feed, least-privilege token), the `1.1.0` choice, the green-by-omission guard, and that this satisfies the constitution's `TODO(PACKAGE_IDENTITY)` ("ratify in a decision record when the first package is published").

**Rationale**: Mirrors the SDD precedent (which documented its release contract repo-locally, not as an org ADR) and directly discharges the constitution's deferred ratification TODO. Repo-local is the right altitude: this is a Governance release decision, not a multi-repo architectural choice.

**Alternatives considered**:
- An org ADR `FS-GG/.github/docs/adr/0008-*` — rejected as the primary home (heavier; org ADRs are for cross-repo architecture per the coordination skill). May be added later if publishing becomes an org-wide pattern; the registry coherence entry + issue #28 already carry the cross-repo layer.
- No record — rejected: the constitution explicitly defers package-identity ratification to "when the first package is published," which is now.

---

## D6 — Drift-locked files stay untouched

**Decision**: No edits to `Directory.Build.props`, `Directory.Packages.props`, or `.config/dotnet-tools.json`. The publish workflow is a new repo-owned file; any release-only tool use is job-scoped (`dotnet tool install` in the workflow, not the manifest). The fsproj `<Version>` is repo-owned and free to change.

**Rationale**: `gate.yml` Job 2 byte-identity drift-checks those three files against the org source of truth (`FS-GG/.github` `dist/dotnet/`); any hand-edit reddens CI (ADR-0006 / spec 085). The CLI already declares `PackAsTool`/`IsPackable=true` in its own fsproj, so no shared-config change is needed to pack it.

**Alternatives considered**: Adding the publish tool to `.config/dotnet-tools.json` — rejected: drift-locked; job-scoped install is the sanctioned path (same pattern the 088 api-compat job uses for `Microsoft.DotNet.ApiCompat.Tool`).

---

## D7 — Resolution/acceptance is downstream-observable, not self-asserted

**Decision**: Treat the item as resolved only when (a) the org feed returns `FS.GG.Governance.Cli` `1.1.0` (no longer 404), (b) the D3 smoke passed in the publishing run, and (c) the FS.GG.Templates#25 composition stage flips from SKIP to asserting the strict-blocks/light-passes matrix and passes. Then respond on + close FS.GG.Governance#28 and move its Coordination board item to **Done** (FR-009).

**Rationale**: The whole point of the item is that a "done" signal sat atop a non-exercisable consumer. Acceptance must be the downstream probe accepting the published CLI (SC-003/006), not a local claim.

**Alternatives considered**: Closing #28 on publish alone — rejected: publish without the probe flipping would repeat the original dishonest-"done" failure mode.

---

## D8 — Risk: confirm the packed tool truly bundles the consumer assembly

**Decision**: The D3 smoke is the authoritative check, but additionally inspect the CLI `packages.lock.json` entry `"FS.GG.Governance.Adapters.SddHandoff": "[1.0.0, )"` during implementation to confirm the consumer resolves as the **locally-built project assembly** bundled into the tool, not as an unresolved/old NuGet package reference. If it resolves to a stale published package, correct the reference so the tool carries the current consumer.

**Rationale**: The CLI fsproj reaches the consumer transitively via `RouteCommand` (a `ProjectReference`), yet the lockfile shows a package-style entry — worth confirming under CentralPackageTransitivePinning that the bundled DLL is the current source build. The install-and-run smoke (D3) would catch a stale/missing consumer behaviorally regardless; this is a belt-and-suspenders source check.

**Alternatives considered**: Ignoring the lockfile entry and trusting the smoke — acceptable as a fallback, but an explicit source check is cheap and removes ambiguity for the maintainer.
