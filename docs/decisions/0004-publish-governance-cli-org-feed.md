# ADR 0004 — Publish the consumer-bearing Governance CLI to the org feed

**Status**: Accepted (verdict: **PUBLISH**) · **Date**: 2026-06-29 · **Feature**:
`specs/089-publish-governance-cli`

**Resolves**: the constitution's deferred `TODO(PACKAGE_IDENTITY)` ("ratify in a
decision record when the first package is published") for `FS.GG.Governance.Cli`,
and the cross-repo request FS-GG/FS.GG.Governance#28 (a downstream product needs an
installable Governance CLI that actually enforces a produced `governance-handoff.json`).

## Context

The spec-081 SDD→Governance handoff consumer (`FS.GG.Governance.Adapters.SddHandoff`)
exists and is unit-tested, and is folded into `RouteCommand`/`ShipCommand`/
`VerifyCommand`. But **no `FS.GG.Governance.Cli` is on the org GitHub Packages feed**
(`gh api orgs/FS-GG/packages/nuget/FS.GG.Governance.Cli` → 404), and this repo had **no
publish path** — `gate.yml` only restores/builds/guards on the read side. The only
installable predecessors (`1.0.0` @ 2026-06-18, `0.1.1` @ 2026-06-25 on the local dev
feed) predate the consumer, so a downstream `route --mode gate` against a failing
handoff exited `0` (green-by-omission), blocking FS.GG.Templates#25.

### Prerequisite discovered during implementation (scope correction)

The feature was planned as **release-only** ("the consumer already exists and is wired
… a packed tool already carries it"). Implementation found this premise **false for the
CLI `route` command**: the one-shot `route` runs through the Host MVU
(`Program.runHost` → `ArtifactReading.loadSnapshot`), which read **only** SpecKit +
DesignSystem facts and **never located `readiness/<id>/governance-handoff.json`**.
`RouteCommand.Interpreter` (which does consume handoffs) was invoked by the CLI only for
the read-only `watch`/`tui` surfaces, and its `exitCode` has no `GovernedBlocking` path.
A freshly-built CLI therefore exited `0` on a failing handoff — the very green-by-omission
this feature exists to prevent.

So a **net-new F# wiring step** was added (approved as a deliberate scope expansion):
- `ProjectSnapshot` gained `Handoffs`, populated at the I/O edge (`ArtifactReading.loadSnapshot`
  locates `readiness/<id>/governance-handoff.json`, mirroring `RouteCommand.Interpreter.realHandoffs`,
  total/safe → `[]`).
- `Cli.resultForHost` folds the handoff through the proven `Adapters.SddHandoff.Consumer.consume`
  (the same consumer Ship/RouteCommand use — **not** re-modelled as `CheckRule`s) and the `route`
  exit becomes `GovernedBlocking` when the F07 route blocks **or** a consumed handoff gate is
  block-capable (`BlockOnShip`) **at `--mode gate`**. Light modes (`sandbox`/`inner`) never block,
  mirroring the F07 rule that blocking gates enforce only at `Gate`. The handoff gates are rendered
  on the route payload (text + JSON) for attribution.

This wiring is exercised end-to-end by the enforcement smoke (below); it added no new public F# API
beyond the `RoutePayload` payload shape and the `ProjectSnapshot.Handoffs` field (curated in the
`.fsi`s, Principle II).

## Decision

**PUBLISH** `FS.GG.Governance.Cli` to the org GitHub Packages feed
(`https://nuget.pkg.github.com/FS-GG/index.json`) as version **`1.1.0`**, via a new
repo-owned workflow `.github/workflows/publish.yml`, gated by a real-evidence
green-by-omission smoke. This is the first published `FS.GG.Governance.Cli` package and
the ratification point for `TODO(PACKAGE_IDENTITY)`.

### The publishing contract (single source: `specs/089-publish-governance-cli/contracts/publish-workflow.md`)

- **Triggers**: `release: published`, `push: tags v*`, `workflow_dispatch` (with an
  optional `version` input — omit for a pack-only dry run).
- **Version source**: the EVALUATED CLI fsproj `<Version>` (`dotnet msbuild -getProperty:Version`),
  never a hardcoded pin. A `v<semver>` tag MUST equal it; mismatch fails the run.
- **Scope**: packs/pushes ONLY `FS.GG.Governance.Cli` (its `PackAsTool` closure bundles
  `Adapters.SddHandoff.dll`). The other ~70 packable projects are out of scope (H4/088-adjacent).
- **Least privilege**: `packages: write` on the `publish` job only; run-scoped `GITHUB_TOKEN`,
  never a PAT. A repository guard (`github.repository == 'FS-GG/FS.GG.Governance'`) means forks
  never publish.
- **Gates (ordered)**: `resolve-version` → `cli-tests` → `enforcement-smoke` → `publish`. The push
  has `needs: enforcement-smoke`.
- **Idempotent / fail-safe**: `dotnet nuget push … --skip-duplicate` (version immutability edge);
  auth/credential failure, version mismatch, or a failing smoke stop before any push — never a
  partial or mislabeled artifact (FR-007).

### Why `1.1.0`

Strictly greater than every predecessor (`1.0.0`, `0.1.1`) per FR-004. A **minor** bump (not patch):
relative to the `1.0.0` build, the tool gains an externally-observable capability — a produced
`governance-handoff.json` now drives the `route` verdict — which is additive feature behavior, not a fix.

### The green-by-omission guard (`tests/cli-publish-smoke/run.sh`, cli-enforcement.md)

REAL evidence, not a unit test: pack the actual tool → `dotnet tool install` into a throwaway
tool-path → run the INSTALLED `fsgg-governance route --mode gate` against committed fixtures and
assert **failing → exit 2**, **passing → exit 0**, **failing + light → exit 0**, plus a structural
backstop that `Adapters.SddHandoff.dll` is in `tools/**`. Verified locally on `1.1.0`:

```
[smoke] backstop OK: SddHandoff.dll present in tools/**
[smoke] OK (failing handoff + gate BLOCKS): exit 2
[smoke] OK (passing handoff + gate PASSES): exit 0
[smoke] OK (failing handoff + light NO-BLOCK): exit 0
SMOKE PASS
```

A consumer-less / unwired build returns `0` on the failing fixture (confirmed against the pre-wiring
CLI) and FAILS this assertion, so it can never reach the push job under the consumer-bearing version.

### Consumer-side coherence (not a contract bump)

This publish records the **consumer-side** verification of `governance-handoff@1.0.0`. The
`governance-handoff` **contract** entry in `FS-GG/.github registry/dependencies.yml`
(`version: "1.0.0"`, `owner: sdd`) is **unchanged**; a `coherence:` entry is appended instead
(FR-006). Acceptance is downstream-observable: issue #28 is resolved only once the feed serves
`1.1.0` AND the FS.GG.Templates#25 probe flips from SKIP to asserting (research D7).

## Alternatives considered

- **A reusable org publish workflow** — deferred: none exists yet; repo-owned mirrors the SDD precedent.
- **Publishing all ~70 packable projects** — rejected: out of scope (H4/088-adjacent).
- **Manual `dotnet nuget push` from a workstation** — rejected: not repeatable/auditable (FR-010).
- **Re-modelling handoff gates as `CheckRule`s in the Host catalog** (the prerequisite) — rejected:
  the handoff is a parallel gate stream everywhere else (Ship/RouteCommand). The CLI folds the same
  `Consumer.consume` rather than forcing handoff semantics into fences/rules.
- **An org ADR instead of a repo-local one** — deferred: this is a Governance release decision at the
  right altitude; the registry coherence entry + #28 carry the cross-repo layer.
